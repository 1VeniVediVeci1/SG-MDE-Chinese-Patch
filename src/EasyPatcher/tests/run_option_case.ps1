param(
    [Parameter(Mandatory=$true)][string]$ExePath,
    [Parameter(Mandatory=$true)][string]$WorkingDirectory,
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [Parameter(Mandatory=$true)][bool]$Translation,
    [Parameter(Mandatory=$true)][bool]$Ui
)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class EasyPatcherWin32 {
  public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr data);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc cb, IntPtr data);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int max);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, string lParam);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
"@

function Window-Info([IntPtr]$Handle, [EasyPatcherWin32+RECT]$RootRect) {
    $text = [Text.StringBuilder]::new(8192)
    [void][EasyPatcherWin32]::GetWindowText($Handle, $text, $text.Capacity)
    $class = [Text.StringBuilder]::new(256)
    [void][EasyPatcherWin32]::GetClassName($Handle, $class, $class.Capacity)
    $rect = [EasyPatcherWin32+RECT]::new()
    [void][EasyPatcherWin32]::GetWindowRect($Handle, [ref]$rect)
    [pscustomobject]@{
        Handle=$Handle; Text=$text.ToString(); Class=$class.ToString();
        X=$rect.Left-$RootRect.Left; Y=$rect.Top-$RootRect.Top;
        Width=$rect.Right-$rect.Left; Height=$rect.Bottom-$rect.Top
    }
}

function Child-Infos([IntPtr]$Parent, [EasyPatcherWin32+RECT]$RootRect) {
    $handles = [Collections.Generic.List[IntPtr]]::new()
    $cb = [EasyPatcherWin32+EnumWindowsProc]{ param([IntPtr]$h,[IntPtr]$d) $handles.Add($h); return $true }
    [void][EasyPatcherWin32]::EnumChildWindows($Parent, $cb, [IntPtr]::Zero)
    @($handles | ForEach-Object { Window-Info $_ $RootRect })
}

function Find-Dialogs([int]$ProcessId) {
    $handles = [Collections.Generic.List[IntPtr]]::new()
    $cb = [EasyPatcherWin32+EnumWindowsProc]{
        param([IntPtr]$h,[IntPtr]$d)
        [uint32]$windowPid=0; [void][EasyPatcherWin32]::GetWindowThreadProcessId($h,[ref]$windowPid)
        if ($windowPid -eq $ProcessId -and [EasyPatcherWin32]::IsWindowVisible($h)) {
            $class=[Text.StringBuilder]::new(64); [void][EasyPatcherWin32]::GetClassName($h,$class,64)
            if ($class.ToString() -eq '#32770') { $handles.Add($h) }
        }
        return $true
    }
    [void][EasyPatcherWin32]::EnumWindows($cb,[IntPtr]::Zero)
    @($handles)
}

$process = Start-Process -FilePath $ExePath -WorkingDirectory $WorkingDirectory -PassThru
try {
    $deadline = (Get-Date).AddSeconds(20)
    do { $process.Refresh(); Start-Sleep -Milliseconds 200 } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw 'main_window_timeout' }

    $rootRect=[EasyPatcherWin32+RECT]::new()
    [void][EasyPatcherWin32]::GetWindowRect($process.MainWindowHandle,[ref]$rootRect)
    $children=Child-Infos $process.MainWindowHandle $rootRect

    $pathBox=$children | Where-Object { $_.Class -like '*EDIT*' -and $_.Y -ge 25 -and $_.Y -le 60 } | Select-Object -First 1
    $checkboxes=@($children | Where-Object { $_.Class -like '*BUTTON*' -and $_.Y -ge 55 -and $_.Y -le 90 } | Sort-Object X)
    $buttons=@($children | Where-Object { $_.Class -like '*BUTTON*' -and $_.Y -ge 85 -and $_.Y -le 125 } | Sort-Object X)
    if ($null -eq $pathBox -or $checkboxes.Count -ne 2 -or $buttons.Count -lt 3) {
        throw ("control_geometry_mismatch path={0} checks={1} buttons={2}" -f ($null -ne $pathBox),$checkboxes.Count,$buttons.Count)
    }

    [void][EasyPatcherWin32]::SendMessage($pathBox.Handle,0x000C,[IntPtr]::Zero,$GameRoot)
    if (-not $Translation) {
        [void][EasyPatcherWin32]::SendMessage($checkboxes[0].Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
    }
    if (-not $Ui) {
        [void][EasyPatcherWin32]::SendMessage($checkboxes[1].Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
    }
    Write-Output ("CONTROL_ACTION translation={0}@({1},{2}) ui={3}@({4},{5}) apply=({6},{7})" -f $Translation,$checkboxes[0].X,$checkboxes[0].Y,$Ui,$checkboxes[1].X,$checkboxes[1].Y,$buttons[0].X,$buttons[0].Y)
    [void][EasyPatcherWin32]::SendMessage($buttons[0].Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)

    $dialog=[IntPtr]::Zero
    $deadline=(Get-Date).AddMinutes(4)
    do {
        $dialogs=Find-Dialogs $process.Id
        if ($dialogs.Count -gt 0) { $dialog=$dialogs[0]; break }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    if ($dialog -eq [IntPtr]::Zero) { throw 'completion_dialog_timeout' }

    $dialogRect=[EasyPatcherWin32+RECT]::new(); [void][EasyPatcherWin32]::GetWindowRect($dialog,[ref]$dialogRect)
    $dialogButtons=@(Child-Infos $dialog $dialogRect | Where-Object { $_.Class -like '*BUTTON*' })
    if ($dialogButtons.Count -eq 0) { throw 'dialog_button_not_found' }
    [void][EasyPatcherWin32]::SendMessage($dialogButtons[0].Handle,0x00F5,[IntPtr]::Zero,[IntPtr]::Zero)
    Start-Sleep -Milliseconds 300

    $children=Child-Infos $process.MainWindowHandle $rootRect
    $logBox=$children | Where-Object { $_.Class -like '*EDIT*' -and $_.Y -gt 100 } | Select-Object -First 1
    if ($null -ne $logBox) { Write-Output $logBox.Text }
    Write-Output ("OPTION_CASE_GUI_COMPLETED translation={0} ui={1}" -f $Translation,$Ui)
}
finally {
    if (-not $process.HasExited) {
        [void][EasyPatcherWin32]::SendMessage($process.MainWindowHandle,0x0010,[IntPtr]::Zero,[IntPtr]::Zero)
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
}
