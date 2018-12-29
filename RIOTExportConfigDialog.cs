/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018 Nicholas Hayes
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
        private static readonly string RiotProxyPath = Path.Combine(Path.GetDirectoryName(typeof(RIOTExportConfigDialog).Assembly.Location), "RIOTProxy.exe");

        public RIOTExportConfigDialog()
        {
            InitializeComponent();
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
            SuspendLayout();
            //
            // RIOTExportConfigDialog
            //
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(282, 253);
            FormBorderStyle = FormBorderStyle.None;
            Location = new Point(0, 0);
            Name = "RIOTExportConfigDialog";
            Opacity = 0D;
            ResumeLayout(false);
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

            Visible = false;

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
