using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Mages.Package;
using Mages.Script;
using fastJSON;

namespace EasyPatcher;

public class MainForm : Form
{
	public const string PATCH_DIR = "berd/";

	private IContainer components;

	private Button button_patch;

	private TextBox textBox_path;

	private TextBox textBox_log;

	private Button button_save;

	private Label label1;

	private Button button_select;

	private FolderBrowserDialog folderBrowserDialog1;

	private LinkLabel linkLabel_version;

	private PictureBox pictureBox_main;

	private Button button_delete_bak;

	public MainForm()
	{
		InitializeComponent();
		((Control)linkLabel_version).Text = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(2);
		Dictionary<string, object> dictionary = JSON.ToObject<Dictionary<string, object>>(File.ReadAllText("berd/meta.json"));
		((Control)this).Text = (string)(((Control)this).Text + (" - " + (dynamic)dictionary["name"]));
		((Control)textBox_path).Text = (dynamic)dictionary["default_path"];
		pictureBox_main.ImageLocation = Path.GetFullPath("berd/" + (dynamic)dictionary["image"]);
		((Control)textBox_log).Text = (dictionary["notice"] as string).Replace("\n", Environment.NewLine);
	}

	public void Log(string data)
	{
		((Control)this).Invoke((Delegate)(Action)delegate
		{
			((TextBoxBase)textBox_log).AppendText(DateTime.Now.ToString() + " " + data + Environment.NewLine);
		});
	}

	public void Oops(string e)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		Log(e);
		MessageBox.Show(e, "致命错误", (MessageBoxButtons)0, (MessageBoxIcon)16);
	}

	public bool patchSCX(Dictionary<string, MPKEntry> mpk, string charset, Dictionary<string, dynamic> scx)
	{
		Log("[SCX] 正在应用 SCX 补丁...");
		foreach (KeyValuePair<string, object> item in scx)
		{
			if (!mpk.ContainsKey(item.Key))
			{
				Oops("[SCX] 无法找到文件 " + item.Key);
				return false;
			}
			Log("[SCX] 正在对 " + item.Key + " 应用补丁...");
			using MemoryStream memoryStream = new MemoryStream();
			using SCXReader sCXReader = new SCXReader(mpk[item.Key].Data, charset);
			using SCXWriter sCXWriter = new SCXWriter(memoryStream, charset);
			StringBuilder stringBuilder = new StringBuilder();
			if ((!SCX.ApplyPatch((dynamic)item.Value, sCXReader, sCXWriter, stringBuilder)))
			{
				Log(stringBuilder.ToString());
				Oops("[SCX] 补丁应用失败");
				return false;
			}
			mpk[item.Key].SetData(memoryStream.ToArray());
		}
		return true;
	}

	public bool patchFile(Dictionary<string, MPKEntry> mpk, Dictionary<string, dynamic> data)
	{
		Log("[FILE] 正在应用文件补丁...");
		foreach (KeyValuePair<string, object> datum in data)
		{
			if (!mpk.ContainsKey(datum.Key))
			{
				Log("[FILE] 无法找到文件 " + datum.Key);
				continue;
			}
			Log("[FILE] 正在替换文件 " + datum.Key + " ...");
			using MemoryStream stream = new MemoryStream(Convert.FromBase64String((dynamic)datum.Value));
			using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
			using MemoryStream memoryStream = new MemoryStream();
			gZipStream.CopyTo(memoryStream);
			mpk[datum.Key].SetData(memoryStream.ToArray());
		}
		return true;
	}

	private void textBox_path_DragOver(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
		{
			e.Effect = (DragDropEffects)1;
		}
	}

	private void textBox_path_DragDrop(object sender, DragEventArgs e)
	{
		TextBox val = (TextBox)((sender is TextBox) ? sender : null);
		if (e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			((Control)val).Text = (e.Data.GetData(DataFormats.FileDrop) as string[])[0];
		}
		else if (e.Data.GetDataPresent(DataFormats.Text))
		{
			((Control)val).Text = e.Data.GetData(DataFormats.Text) as string;
		}
	}

	private void button_patch_Click(object sender, EventArgs e)
	{
		((TextBoxBase)textBox_log).Clear();
		((Control)button_patch).Enabled = false;
		ThreadPool.QueueUserWorkItem(delegate
		{
			//IL_0548: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Log("[BERD] 正在应用全部补丁...");
				string text = Path.Combine(((Control)textBox_path).Text, "USRDIR");
				Log("[BERD] 正在寻找 USRDIR...");
				if (!Directory.Exists(text))
				{
					Oops("[BERD] USRDIR 不存在, 请检查你的目录设置.");
					((Control)this).Invoke((Delegate)(Action)delegate
					{
						((Control)button_patch).Enabled = true;
					});
					return;
				}
				string text2 = text + ".bak";
				if (!Directory.Exists(text2))
				{
					Log("[BERD] 备份文件夹不存在, 创建中...");
					Directory.CreateDirectory(text2);
				}
				foreach (Dictionary<string, object> item in from p in Directory.GetFiles("berd/", "*.json")
					select JSON.ToObject<Dictionary<string, object>>(File.ReadAllText(p)))
				{
					if (item.ContainsKey("file"))
					{
						string text3 = (dynamic)item["file"];
						if (!File.Exists(Path.Combine(text2, text3)))
						{
							Log("[BERD] 正在备份 " + text3 + "...");
							File.Copy(Path.Combine(text, text3), Path.Combine(text2, text3));
						}
						MPK mPK = null;
						Log("[MPK] 正在加载 " + text3 + "...");
						using (BinaryReader reader = new BinaryReader(File.OpenRead(Path.Combine(text2, text3))))
						{
							mPK = MPK.ReadFile(reader);
						}
						Dictionary<string, MPKEntry> dictionary = mPK.Entries.ToDictionary((MPKEntry k) => k.Name, (MPKEntry v) => v);
						object obj = item["type"];
						if (obj != null)
						{
							switch (obj as string)
							{
							case "scx":
								if ((!patchSCX(dictionary, (dynamic)item["charset_preset"] + (dynamic)item["charset"], (dynamic)item["data"])))
								{
									return;
								}
								goto IL_04c0;
							case "file":
								{
									if ((!patchFile(dictionary, (dynamic)item["data"])))
									{
										return;
									}
									goto IL_04c0;
								}
								IL_04c0:
								Log("[MPK] 正在打包 " + text3 + "...");
								using (BinaryWriter binaryWriter = new BinaryWriter(File.Open(Path.Combine(text, text3), FileMode.Create)))
								{
									mPK.Write(binaryWriter);
									Log("[MPK] 打包成功: " + binaryWriter.BaseStream.Position);
								}
								continue;
							}
						}
						Oops("未知补丁类型");
						((Control)this).Invoke((Delegate)(Action)delegate
						{
							((Control)button_patch).Enabled = true;
						});
						return;
					}
				}
				MessageBox.Show("补丁应用完成, 请检查游戏是否能正常运行", "提示", (MessageBoxButtons)0, (MessageBoxIcon)64);
				Log("[FENGberd] 操作完成, 请检查游戏是否能正常运行");
			}
			catch (Exception ex)
			{
				Oops(ex.ToString());
				Log("[FENGberd] 发生致命错误");
			}
			((Control)this).Invoke((Delegate)(Action)delegate
			{
				((Control)button_patch).Enabled = true;
			});
		});
	}

	private void button_delete_bak_Click(object sender, EventArgs e)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Invalid comparison between Unknown and I4
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string text = Path.Combine(((Control)textBox_path).Text, "USRDIR");
			if (!Directory.Exists(text))
			{
				Oops("USRDIR 不存在, 请检查你的目录设置.");
				return;
			}
			string path = text + ".bak";
			if (!Directory.Exists(path))
			{
				Oops("未找到备份文件夹.");
			}
			else if ((int)MessageBox.Show("确认要删除备份文件夹吗?\n删除后要撤销补丁必须重新验证游戏完整性\n并且可能对未来的补丁覆盖造成影响", "操作确认", (MessageBoxButtons)1, (MessageBoxIcon)48) == 1)
			{
				Directory.Delete(path, recursive: true);
				MessageBox.Show("备份文件夹已删除", "提示", (MessageBoxButtons)0, (MessageBoxIcon)64);
			}
		}
		catch (Exception ex)
		{
			Oops(ex.ToString());
		}
	}

	private void button_save_Click(object sender, EventArgs e)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Invalid comparison between Unknown and I4
		SaveFileDialog val = new SaveFileDialog
		{
			Filter = "日志文件(*.log)|*.log",
			DefaultExt = "log",
			CheckPathExists = true
		};
		if ((int)((CommonDialog)val).ShowDialog() == 1)
		{
			using (StreamWriter streamWriter = new StreamWriter(val.OpenFile()))
			{
				streamWriter.Write(((Control)textBox_log).Text);
			}
		}
	}

	private void button_select_Click(object sender, EventArgs e)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Invalid comparison between Unknown and I4
		if ((int)((CommonDialog)folderBrowserDialog1).ShowDialog() == 1)
		{
			((Control)textBox_path).Text = folderBrowserDialog1.SelectedPath;
		}
	}

	private void linkLabel_version_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start("https://github.com/fengberd/MagesTools");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Expected O, but got Unknown
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Expected O, but got Unknown
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Expected O, but got Unknown
		button_patch = new Button();
		textBox_path = new TextBox();
		textBox_log = new TextBox();
		button_save = new Button();
		label1 = new Label();
		button_select = new Button();
		folderBrowserDialog1 = new FolderBrowserDialog();
		linkLabel_version = new LinkLabel();
		pictureBox_main = new PictureBox();
		button_delete_bak = new Button();
		((ISupportInitialize)pictureBox_main).BeginInit();
		((Control)this).SuspendLayout();
		((Control)button_patch).Font = new Font("宋体", 9f);
		((Control)button_patch).Location = new Point(266, 39);
		((Control)button_patch).Name = "button_patch";
		((Control)button_patch).Size = new Size(75, 23);
		((Control)button_patch).TabIndex = 2;
		((Control)button_patch).Text = "应用补丁";
		((ButtonBase)button_patch).UseVisualStyleBackColor = true;
		((Control)button_patch).Click += button_patch_Click;
		((Control)textBox_path).AllowDrop = true;
		((Control)textBox_path).Location = new Point(77, 12);
		((Control)textBox_path).Name = "textBox_path";
		((Control)textBox_path).Size = new Size(388, 21);
		((Control)textBox_path).TabIndex = 0;
		((Control)textBox_path).DragDrop += new DragEventHandler(textBox_path_DragDrop);
		((Control)textBox_path).DragOver += new DragEventHandler(textBox_path_DragOver);
		((Control)textBox_log).BackColor = Color.Black;
		((Control)textBox_log).Font = new Font("Consolas", 10f);
		((Control)textBox_log).ForeColor = Color.Silver;
		((Control)textBox_log).Location = new Point(12, 68);
		((TextBoxBase)textBox_log).Multiline = true;
		((Control)textBox_log).Name = "textBox_log";
		textBox_log.ScrollBars = (ScrollBars)2;
		((Control)textBox_log).Size = new Size(491, 230);
		((Control)textBox_log).TabIndex = 4;
		((Control)button_save).Location = new Point(428, 39);
		((Control)button_save).Name = "button_save";
		((Control)button_save).Size = new Size(75, 23);
		((Control)button_save).TabIndex = 3;
		((Control)button_save).Text = "保存日志";
		((ButtonBase)button_save).UseVisualStyleBackColor = true;
		((Control)button_save).Click += button_save_Click;
		((Control)label1).AutoSize = true;
		((Control)label1).Location = new Point(12, 15);
		((Control)label1).Name = "label1";
		((Control)label1).Size = new Size(59, 12);
		((Control)label1).TabIndex = 4;
		((Control)label1).Text = "游戏路径:";
		((Control)button_select).Location = new Point(471, 12);
		((Control)button_select).Name = "button_select";
		((Control)button_select).Size = new Size(32, 21);
		((Control)button_select).TabIndex = 1;
		((Control)button_select).Text = "...";
		((ButtonBase)button_select).UseVisualStyleBackColor = true;
		((Control)button_select).Click += button_select_Click;
		folderBrowserDialog1.Description = "选择游戏路径";
		folderBrowserDialog1.ShowNewFolderButton = false;
		((Control)linkLabel_version).AutoSize = true;
		((Control)linkLabel_version).Location = new Point(12, 44);
		((Control)linkLabel_version).Name = "linkLabel_version";
		((Control)linkLabel_version).Size = new Size(47, 12);
		((Control)linkLabel_version).TabIndex = 5;
		linkLabel_version.TabStop = true;
		((Control)linkLabel_version).Text = "Version";
		linkLabel_version.LinkClicked += new LinkLabelLinkClickedEventHandler(linkLabel_version_LinkClicked);
		((Control)pictureBox_main).Location = new Point(509, 12);
		((Control)pictureBox_main).Name = "pictureBox_main";
		((Control)pictureBox_main).Size = new Size(368, 286);
		pictureBox_main.TabIndex = 6;
		pictureBox_main.TabStop = false;
		((Control)button_delete_bak).Location = new Point(347, 39);
		((Control)button_delete_bak).Name = "button_delete_bak";
		((Control)button_delete_bak).Size = new Size(75, 23);
		((Control)button_delete_bak).TabIndex = 7;
		((Control)button_delete_bak).Text = "删除备份";
		((ButtonBase)button_delete_bak).UseVisualStyleBackColor = true;
		((Control)button_delete_bak).Click += button_delete_bak_Click;
		((ContainerControl)this).AutoScaleDimensions = new SizeF(6f, 12f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)1;
		((Form)this).ClientSize = new Size(889, 310);
		((Control)this).Controls.Add((Control)(object)button_delete_bak);
		((Control)this).Controls.Add((Control)(object)pictureBox_main);
		((Control)this).Controls.Add((Control)(object)linkLabel_version);
		((Control)this).Controls.Add((Control)(object)button_select);
		((Control)this).Controls.Add((Control)(object)label1);
		((Control)this).Controls.Add((Control)(object)button_save);
		((Control)this).Controls.Add((Control)(object)textBox_log);
		((Control)this).Controls.Add((Control)(object)textBox_path);
		((Control)this).Controls.Add((Control)(object)button_patch);
		((Form)this).FormBorderStyle = (FormBorderStyle)1;
		((Form)this).MaximizeBox = false;
		((Form)this).MinimizeBox = false;
		((Control)this).Name = "MainForm";
		((Control)this).Text = "Mages 全自动补丁工具";
		((ISupportInitialize)pictureBox_main).EndInit();
		((Control)this).ResumeLayout(false);
		((Control)this).PerformLayout();
	}
}
