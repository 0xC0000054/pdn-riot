/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet.Effects;
using SaveForWebRIOT.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SaveForWebRIOT
{
    internal sealed class RIOTExportConfigDialog : EffectConfigDialog
    {
        private Label infoLabel;
        private static readonly string RiotProxyPath = Path.Combine(Path.GetDirectoryName(typeof(RIOTExportConfigDialog).Assembly.Location), "RIOTProxy.exe");

        public RIOTExportConfigDialog()
        {
            InitializeComponent();
            Text = RIOTExportEffect.StaticName;
        }

        protected override void InitialInitToken()
        {
            theEffectToken = new RIOTExportConfigToken();
        }

        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
        }

        protected override void InitTokenFromDialog()
        {
        }

        private void InitializeComponent()
        {
            this.infoLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // infoLabel
            // 
            this.infoLabel.AutoSize = true;
            this.infoLabel.Location = new System.Drawing.Point(13, 13);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Size = new System.Drawing.Size(183, 13);
            this.infoLabel.TabIndex = 1;
            this.infoLabel.Text = "RIOT will open in a separate window.";
            // 
            // RIOTExportConfigDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.ClientSize = new System.Drawing.Size(368, 82);
            this.ControlBox = false;
            this.Controls.Add(this.infoLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Location = new System.Drawing.Point(0, 0);
            this.Name = "RIOTExportConfigDialog";
            this.ShowIcon = false;
            this.Controls.SetChildIndex(this.infoLabel, 0);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private DialogResult ShowErrorMessage(string message)
        {
            if (InvokeRequired)
            {
                return (DialogResult)Invoke(new Action<string>((string error) => MessageBox.Show(error, Text, MessageBoxButtons.OK, MessageBoxIcon.Error)),
                                            message);
            }
            else
            {
                return MessageBox.Show(message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (File.Exists(RiotProxyPath))
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(LaunchRIOT));
            }
            else
            {
                if (ShowErrorMessage(Resources.RIOTProxyNotFound) == DialogResult.OK)
                {
                    CloseForm();
                }
            }
        }

        private void CloseForm()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void LaunchRIOT(object state)
        {
            try
            {
                string tempImageFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                using (Bitmap source = EffectSourceSurface.CreateAliasedBitmap())
                {
                    source.Save(tempImageFileName, ImageFormat.Png);
                }

                // Add quotes around the image path in case it contains spaces.
                ProcessStartInfo startInfo = new ProcessStartInfo(RiotProxyPath, "\"" + tempImageFileName + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                int exitCode;
                using (Process proc = Process.Start(startInfo))
                {
                    proc.WaitForExit();
                    exitCode = proc.ExitCode;
                }
                try
                {
                    File.Delete(tempImageFileName);
                }
                catch (IOException)
                {
                }

                if (exitCode != 0)
                {
                    switch (exitCode)
                    {
                        case 1:
                            ShowErrorMessage(Resources.WICLoadFailed);
                            break;
                        case 2:
                            ShowErrorMessage(Resources.OutOfMemory);
                            break;
                        case 3:
                            ShowErrorMessage(Resources.RIOTDllMissing);
                            break;
                        case 4:
                            ShowErrorMessage(Resources.RIOTEntrypointNotFound);
                            break;
                    }
                }
            }
            catch (ExternalException ex)
            {
                ShowErrorMessage(ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                ShowErrorMessage(ex.Message);
            }

            BeginInvoke(new Action(CloseForm));
        }
    }
}
