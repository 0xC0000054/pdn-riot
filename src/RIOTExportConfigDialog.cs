﻿/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021, 2022 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.Effects;
using SaveForWebRIOT.Interop;
using SaveForWebRIOT.Properties;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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

        private static unsafe bool SurfaceHasTransparency(Surface surface)
        {
            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* row = surface.GetRowAddressUnchecked(y);
                ColorBgra* rowEnd = row + surface.Width;

                while (row < rowEnd)
                {
                    if (row->A != 255)
                    {
                        return true;
                    }

                    row++;
                }
            }

            return false;
        }

        private void CloseForm()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private unsafe SafeMemoryMappedFileHandle CreateMemoryMappedDIB(string name, out uint fileMappingSize)
        {
            Surface surface = Effect.EnvironmentParameters.SourceSurface;

            int width = surface.Width;
            int height = surface.Height;

            int dibBitsPerPixel = SurfaceHasTransparency(surface) ? 32 : 24;

            int bmiHeaderSize = Marshal.SizeOf(typeof(NativeStructs.BITMAPINFOHEADER));
            int dibStride = (((width * dibBitsPerPixel) + 31) & ~31) / 8;
            long dibImageDataSize = dibStride * height;

            // 24-bit and 32-bit DIB files do not have a color palette.
            long dibSize = bmiHeaderSize + dibImageDataSize;

            if (dibSize > uint.MaxValue)
            {
                throw new IOException(Resources.ImageLargerThan4GB);
            }

            fileMappingSize = (uint)dibSize;

            SafeMemoryMappedFileHandle handle = null;
            SafeMemoryMappedFileHandle temp = null;

            try
            {
                temp = SafeNativeMethods.CreateFileMappingW(new IntPtr(NativeConstants.INVALID_HANDLE_VALUE),
                                                            IntPtr.Zero,
                                                            NativeConstants.PAGE_READWRITE,
                                                            0,
                                                            (uint)dibSize,
                                                            name);
                if (temp.IsInvalid)
                {
                    throw new Win32Exception();
                }

                using (SafeMemoryMappedFileView view = SafeNativeMethods.MapViewOfFile(temp,
                                                                                       NativeConstants.FILE_MAP_WRITE,
                                                                                       0,
                                                                                       0,
                                                                                       new UIntPtr((ulong)dibSize)))
                {
                    if (view.IsInvalid)
                    {
                        throw new Win32Exception();
                    }

                    void* baseAddress = view.DangerousGetHandle().ToPointer();

                    NativeStructs.BITMAPINFOHEADER* bmiHeader = (NativeStructs.BITMAPINFOHEADER*)baseAddress;
                    bmiHeader->biSize = (uint)bmiHeaderSize;
                    bmiHeader->biWidth = width;
                    bmiHeader->biHeight = height;
                    bmiHeader->biPlanes = 1;
                    bmiHeader->biBitCount = (ushort)dibBitsPerPixel;
                    bmiHeader->biCompression = NativeConstants.BI_RGB;
                    bmiHeader->biSizeImage = 0;
                    bmiHeader->biXPelsPerMeter = 0;
                    bmiHeader->biYPelsPerMeter = 0;
                    bmiHeader->biClrUsed = 0;
                    bmiHeader->biClrImportant = 0;

                    int lastBitmapRow = height - 1;
                    int dibBytesPerPixel = dibBitsPerPixel / 8;

                    byte* dibScan0 = (byte*)baseAddress + bmiHeaderSize;

                    for (int y = 0; y < height; y++)
                    {
                        // Access the surface in the order needed for a bottom-up DIB.
                        ColorBgra* src = surface.GetRowAddressUnchecked(lastBitmapRow - y);
                        byte* dst = dibScan0 + (y * dibStride);

                        for (int x = 0; x < width; x++)
                        {
                            switch (dibBitsPerPixel)
                            {
                                case 24:
                                    dst[0] = src->B;
                                    dst[1] = src->G;
                                    dst[2] = src->R;
                                    break;
                                case 32:
                                    dst[0] = src->B;
                                    dst[1] = src->G;
                                    dst[2] = src->R;
                                    dst[3] = src->A;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unsupported { nameof(dibBitsPerPixel) } value: { dibBitsPerPixel }.");
                            }

                            src++;
                            dst += dibBytesPerPixel;
                        }
                    }
                }

                handle = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
            }

            return handle;
        }

        private void LaunchRiot()
        {
            Exception exception = null;

            try
            {
                string fileMappingName = "pdn_" + Guid.NewGuid().ToString();
                int exitCode;

                SafeMemoryMappedFileHandle fileMappingHandle = null;

                try
                {
                    fileMappingHandle = CreateMemoryMappedDIB(fileMappingName, out uint fileMappingSize);

                    string arguments = fileMappingName + " " + fileMappingSize.ToString(CultureInfo.InvariantCulture);

                    ProcessStartInfo startInfo = new ProcessStartInfo(RiotProxyPath, arguments);

                    using (Process proc = Process.Start(startInfo))
                    {
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                    }
                }
                finally
                {
                    fileMappingHandle?.Dispose();
                }

                switch (exitCode)
                {
                    case 0:
                        // No error.
                        break;
                    case 1:
                        exception = new IOException(Resources.DIBLoadFailed);
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
