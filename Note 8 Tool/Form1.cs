﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Reflection.Emit;
using System.Xml;
using Microsoft.VisualBasic.CompilerServices;

namespace Note_8_Tool
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public static bool isBusy = false;
        DataTable ptable=new DataTable();
        #region ADB
        private string ADBProcess(string cmd)
        {
            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = "Data\\miadb.exe",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = cmd
            };
            Process a = new Process();
            a.StartInfo = info;
            a.Start();
            return a.StandardOutput.ReadToEnd();
        }
        private bool HasADBDevice()
        {            
            bool flag = false;
            string p = ADBProcess("devices");
            if (p.Length > 29)
            {
                using (StringReader s = new StringReader(p))
                {
                    string line;
                    while (s.Peek() != -1)
                    {
                        line = s.ReadLine();

                        if (line.StartsWith("List") || line.StartsWith("\r\n") || line.Trim() == "")
                            continue;

                        if (line.IndexOf('\t') != -1)
                        {
                            line = line.Substring(0, line.IndexOf('\t'));
                            flag = true;
                        }
                    }
                }
            }
            return flag;
        }
        private bool ADBPush(string source,string denstination)
        {
            return !ADBProcess("push \"" + source + "\" " + denstination).Contains("failed to copy");
        }
        private bool ADBPull(string source,string denstination)
        {
            return ADBProcess("pull " + source + " \"" + denstination+"\"").Contains("bytes in");
        }
        #endregion
        #region Fastboot 
        private string FastbootProcess(string cmd)
        {
            ProcessStartInfo info = new ProcessStartInfo()
            {
                FileName = "Data\\fastboot.exe",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = cmd
            };
            Process a = new Process();
            a.StartInfo = info;
            a.Start();
            return a.StandardOutput.ReadToEnd();
        }
        private bool FastbootConnected()
        {
            return FastbootProcess("devices").Contains("fastboot");
        }
        private bool FastbootUnlock()
        {
            return FastbootProcess("flashing unlock").Contains("OKAY");
        }
        private bool FBtoEDL()
        {
            return FastbootProcess("oem edl").Contains("OKAY"); 
        }
        #endregion
        #region SFK
        private void SFK(string filename, string source, string target)
        {
            ProcessStartInfo info1 = new ProcessStartInfo();
            info1.UseShellExecute = false;
            info1.CreateNoWindow = true;
            info1.FileName = @"Data\sfk.exe";
            info1.Arguments = "replace " + "\"" + filename + "\"" + " -binary " + "\"/" + source + "/" + target + "/" + "\"" + " -dump -yes";
            info1.RedirectStandardOutput = true;
            Process process1 = new Process();
            process1.StartInfo = info1;
            Process process = process1;
            process.OutputDataReceived += SFKOutput;
            process.Start();
            process.BeginOutputReadLine();
        }
        private void SFKOutput(object sendingProcess, DataReceivedEventArgs outline)
        {
            if (!string.IsNullOrEmpty(outline.Data))
            {                
                if (outline.Data.Contains("change at offset"))
                {
                    string[] p = outline.Data.Split(':');
                    logs("\n" + p[2], Color.DarkGreen);
                }
                if (outline.Data.Contains("1 files checked"))
                {
                    logs("\nSecurity Patched", Color.DarkGreen);
                }
            }
        }
        #endregion
        #region ReadBlock
        private void ReadBlockMap(string file)
        {
            FileInfo fileInfo = new FileInfo(file);
            using (XmlReader xmlReader = XmlReader.Create(file))
            {
                while (xmlReader.Read())
                {                    
                    bool flag = xmlReader.IsStartElement() && Operators.CompareString(xmlReader.Name, "sideload", false) == 0;
                    if (flag)
                    {
                        string text = xmlReader["filename"];
                        string text2 = xmlReader["block"];                        
                        try
                        {
                            bool flag2 = !string.IsNullOrEmpty(text2);
                            if (flag2)
                            {
                                ptable.Rows.Add(new object[]
                                {
                            text,
                            text2                           
                                });                                
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                        }
                    }
                }
            }
        }
        #endregion
        #region Functions
        private void BLU()
        {
            logs("Checking Devices : ", Color.Black);
            if (HasADBDevice())
            {
                logs("Connected", Color.DarkGreen);
                logs("\nReading Partition Database : ", Color.Black);
                ReadBlockMap(Application.StartupPath + "\\Data\\BlockMap.xml");                
                for(int i = 0; i < ptable.Rows.Count; i++)
                {
                    logs("\nWriting File : ", Color.Black);
                    if (ADBPush(Application.StartupPath+"\\Data\\Files\\"+ptable.Rows[i][0].ToString(), ptable.Rows[i][1].ToString()))
                    {
                        logs("OKAY", Color.DarkGreen);
                    }
                    else
                    {
                        logs("FAIL!", Color.Red);
                    }
                    int v = (i / ptable.Rows.Count) * 100;
                    progressBar1.Value = v;
                    Invoke(new MethodInvoker(delegate () { richTextBox1.ScrollToCaret(); }));
                }
            }
            else
            {
                logs("Not Found", Color.Red);
            }
            isBusy = false;
            Invoke(new MethodInvoker(delegate () { richTextBox1.ScrollToCaret(); }));
        }
        #endregion
        private void logs(string text,Color color)
        {
            Invoke(new MethodInvoker(delegate () { richTextBox1.AppendText(text, color); }));
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            logs("Boot device to recovery\nSelect Mi Assistant Mode and Connect\nTurn On OEM Unlock for Bootloader Unlock", Color.Black);
            ptable.Columns.Add("Filename");
            ptable.Columns.Add("Block");
            progressBar1.Maximum = 100;
            progressBar1.Minimum = 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!isBusy)
            {
                DialogResult dr= MessageBox.Show("Have You Turned On OEM Unlock in Developer Options", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    richTextBox1.Clear();
                    isBusy = true;
                    logs("Bootloader Unlock\n\n", Color.RoyalBlue);
                    Thread b = new Thread(BLU);
                    b.IsBackground = true;
                    b.Start();
                }
            }
            else
            {
                MessageBox.Show("Thread is locked");
            }
        }
    }
    public static class RichTextBoxExtension
    {
        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }
    }
}