param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [Parameter(Mandatory=$true)][string]$WorkingDirectory,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [string]$GameRoot,
    [switch]$InspectOnly,
    [switch]$AllowLegacy
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ApplyAllWin32 {
 public delegate bool Callback(IntPtr h, IntPtr data);
 [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] public static extern bool EnumWindows(Callback cb,IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h,Callback cb,IntPtr data);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder t,int n);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,StringBuilder l);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
}
"@
[void][ApplyAllWin32]::SetProcessDPIAware()
function Text-Of([IntPtr]$Handle) {
 $t=[Text.StringBuilder]::new(131072)
 [void][ApplyAllWin32]::SendMessage($Handle,0x000D,[IntPtr]$t.Capacity,$t)
 $t.ToString()
}
function Children([IntPtr]$Handle) {
 $all=[Collections.Generic.List[IntPtr]]::new()
 $cb=[ApplyAllWin32+Callback]{param([IntPtr]$h,[IntPtr]$d) $all.Add($h);return $true}
 [void][ApplyAllWin32]::EnumChildWindows($Handle,$cb,[IntPtr]::Zero)
 foreach($h in $all){
  $c=[Text.StringBuilder]::new(256);[void][ApplyAllWin32]::GetClassName($h,$c,256)
  $r=[ApplyAllWin32+RECT]::new();[void][ApplyAllWin32]::GetWindowRect($h,[ref]$r)
  [pscustomobject]@{Handle=$h;Class=$c.ToString();Text=(Text-Of $h);X=$r.Left;Y=$r.Top;Width=$r.Right-$r.Left;Height=$r.Bottom-$r.Top}
 }
}
function Dialogs([int]$ProcessId) {
 $all=[Collections.Generic.List[IntPtr]]::new()
 $cb=[ApplyAllWin32+Callback]{param([IntPtr]$h,[IntPtr]$d)
  [uint32]$id=0;[void][ApplyAllWin32]::GetWindowThreadProcessId($h,[ref]$id)
  if($id -eq $ProcessId -and [ApplyAllWin32]::IsWindowVisible($h)){
   $c=[Text.StringBuilder]::new(128);[void][ApplyAllWin32]::GetClassName($h,$c,128)
   if($c.ToString() -eq '#32770'){$all.Add($h)}
  };return $true
 }
 [void][ApplyAllWin32]::EnumWindows($cb,[IntPtr]::Zero);@($all)
}
function Capture([IntPtr]$Handle,[string]$Name) {
 $r=[ApplyAllWin32+RECT]::new();[void][ApplyAllWin32]::GetWindowRect($Handle,[ref]$r)
 $b=[Drawing.Bitmap]::new($r.Right-$r.Left,$r.Bottom-$r.Top);$g=[Drawing.Graphics]::FromImage($b);$dc=$g.GetHdc()
 try {if(-not [ApplyAllWin32]::PrintWindow($Handle,$dc,2)){throw 'PrintWindow failed'}}finally{$g.ReleaseHdc($dc);$g.Dispose()}
 try {$b.Save((Join-Path $EvidenceDirectory $Name),[Drawing.Imaging.ImageFormat]::Png)}finally{$b.Dispose()}
}
[void][IO.Directory]::CreateDirectory($EvidenceDirectory)
if(-not $InspectOnly){
 if(-not $GameRoot -or -not (Test-Path (Join-Path $GameRoot '.apply-all-test-fixture'))){throw 'Refusing to patch a non-test game directory'}
}
$process=Start-Process -FilePath $ExePath -WorkingDirectory $WorkingDirectory -PassThru
try {
 $deadline=(Get-Date).AddSeconds(25)
 do {$process.Refresh();if($process.HasExited){throw 'Patcher exited during startup'};Start-Sleep -Milliseconds 200}while($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
 if($process.MainWindowHandle -eq 0){throw 'main_window_timeout'}
 [void]$process.WaitForInputIdle(10000)
 $children=@(Children $process.MainWindowHandle)
 $choices=@($children|Where-Object {$_.Class -like '*BUTTON*' -and $_.Text -match '汉化|UI 修改'})
 $apply=@($children|Where-Object {$_.Class -like '*BUTTON*' -and $_.Text -eq '应用补丁'})
 $record=[ordered]@{pid=$process.Id;exe=$ExePath;working_directory=$WorkingDirectory;choice_count=$choices.Count;children=$children}
 $record|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 (Join-Path $EvidenceDirectory 'initial-ui.json')
 Capture $process.MainWindowHandle 'initial-ui.png'
 if(-not $AllowLegacy -and ($choices.Count -ne 0 -or $apply.Count -ne 1)){
  throw ("unified_ui_contract_failed choices={0} apply_buttons={1}" -f $choices.Count,$apply.Count)
 }
 if($InspectOnly){Write-Output ('UI_INSPECT_PASS '+($record|ConvertTo-Json -Depth 6 -Compress));return}
 $pathBox=@($children|Where-Object {$_.Class -like '*EDIT*'}|Sort-Object Y)[0]
 [void][ApplyAllWin32]::SendMessage($pathBox.Handle,0x000C,[IntPtr]::Zero,$GameRoot)
 if((Text-Of $pathBox.Handle) -ne $GameRoot){throw 'Target path was not accepted'}
 [void][ApplyAllWin32]::SendMessage($apply[0].Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
 $dialog=[IntPtr]::Zero;$deadline=(Get-Date).AddMinutes(4)
 do {
  $found=@(Dialogs $process.Id)
  if($found.Count -gt 0){$dialog=$found[0];break}
  if($process.HasExited){throw 'Patcher exited while applying'}
  Start-Sleep -Milliseconds 250
 }while((Get-Date) -lt $deadline)
 if($dialog -eq [IntPtr]::Zero){throw 'completion_dialog_timeout'}
 $dialogChildren=@(Children $dialog);$dialogText=($dialogChildren|ForEach-Object {$_.Text}) -join "`n"
 $dialogText|Set-Content -Encoding UTF8 (Join-Path $EvidenceDirectory 'completion-dialog.txt')
 if($dialogText -notmatch '补丁应用完成'){throw ('Unexpected dialog: '+$dialogText)}
 Capture $dialog 'completion-dialog.png'
 $ok=@($dialogChildren|Where-Object {$_.Class -like '*BUTTON*'})[0]
 [void][ApplyAllWin32]::SendMessage($ok.Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
 $deadline=(Get-Date).AddSeconds(5);$log=''
 do {
  $edit=@(Children $process.MainWindowHandle|Where-Object {$_.Class -like '*EDIT*'}|Sort-Object Y)[-1]
  $log=$edit.Text
  if($log -match '\[FENGberd\] 操作完成'){break}
  Start-Sleep -Milliseconds 100
 }while((Get-Date) -lt $deadline)
 $log|Set-Content -Encoding UTF8 (Join-Path $EvidenceDirectory 'apply.log')
 if($log -notmatch '\[FENGberd\] 操作完成' -or $log -match '跳过|无法找到|失败|致命错误'){throw 'Patch log did not report complete unfiltered success'}
 Capture $process.MainWindowHandle 'completed-ui.png'
 Write-Output ('APPLY_ALL_GUI_PASS '+($record|ConvertTo-Json -Depth 6 -Compress))
} finally {
 if(-not $process.HasExited){
  [void][ApplyAllWin32]::SendMessage($process.MainWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
  if(-not $process.WaitForExit(5000)){$process.Kill();$process.WaitForExit()}
 }
}
