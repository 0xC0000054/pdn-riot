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
using System.Threading;
using System.Windows.Forms;

namespace SaveForWebRIOT
{
    internal sealed class RIOTExportConfigDialog : EffectConfigDialog
    {
        private Label infoLabel;
        private Thread riotWorkerThread;
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
                return (DialogResult)Invoke(new Action<string>((string error) => MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Error)),
                                            message);
            }
            else
            {
                return MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (File.Exists(RiotProxyPath))
            {
                riotWorkerThread = new Thread(LaunchRiot);
                riotWorkerThread.Start();
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

        private void LaunchRiot()
        {
            Exception exception = null;

            try
            {
                string tempImageFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                using (Bitmap source = EffectSourceSurface.CreateAliasedBitmap())
                {
                    source.Save(tempImageFileName, ImageFormat.Png);
                }

                // Add quotes around the image path in case it contains spaces.
                ProcessStartInfo startInfo = new ProcessStartInfo(RiotProxyPath, "\"" + tempImageFileName + "\"");

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

                switch (exitCode)
                {
                    case 0:
                        // No error.
                        break;
                    case 1:
                        exception = new IOException(Resources.WICLoadFailed);
                        break;
                    case 2:
                        exception = new OutOfMemoryException(Resources.OutOfMemory);
                        break;
                    case 3:
                        exception = new DllNotFoundException(Resources.RIOTDllMissing);
                        break;
                    case 4:
                        exception = new EntryPointNotFoundException(Resources.RIOTEntrypointNotFound);
                        break;
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            BeginInvoke(new Action<Exception>(RiotThreadFinished), exception);
        }

        private void RiotThreadFinished(Exception exception)
        {
            riotWorkerThread.Join();

            if (exception != null)
            {
                ShowErrorMessage(exception.Message);
            }

            CloseForm();
        }
    }
}
