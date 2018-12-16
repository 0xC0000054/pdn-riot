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

using PaintDotNet;
using PaintDotNet.Effects;
using SaveForWebRIOT.Properties;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SaveForWebRIOT
{
    internal sealed class RIOTExportConfigDialog : EffectConfigDialog
    {
        private static readonly string RiotProxyPath = Path.Combine(Path.GetDirectoryName(typeof(RIOTExportConfigDialog).Assembly.Location), "RIOTProxy.exe");

        public RIOTExportConfigDialog()
        {
        }

        protected override void InitialInitToken()
        {
            this.theEffectToken = new RIOTExportConfigToken();
        }

        protected override void InitDialogFromToken(EffectConfigToken effectTokenCopy)
        {
        }

        protected override void InitTokenFromDialog()
        {
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            //
            // RIOTExportConfigDialog
            //
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.ClientSize = new Size(282, 253);
            this.Location = new Point(0, 0);
            this.Name = "RIOTExportConfigDialog";
            this.Opacity = 0D;
            this.ResumeLayout(false);
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, RIOTExportEffect.StaticName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.Visible = false;

            if (File.Exists(RiotProxyPath))
            {
                try
                {
                    string tempImageFileName =  Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                    using (Bitmap source = this.EffectSourceSurface.CreateAliasedBitmap())
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
                                ShowErrorMessage(Resources.RIOTLoadFailed);
                                break;
                            default:
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
            }
            else
            {
                ShowErrorMessage(Resources.RIOTProxyNotFound);
            }

            this.DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
