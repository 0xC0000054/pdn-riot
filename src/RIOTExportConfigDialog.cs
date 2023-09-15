/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021, 2022, 2023 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
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
    internal sealed class RIOTExportConfigDialog : EffectConfigForm
    {
        private Label infoLabel;
        private Thread riotWorkerThread;
        private static readonly string RiotProxyPath = Path.Combine(Path.GetDirectoryName(typeof(RIOTExportConfigDialog).Assembly.Location), "RIOTProxy.exe");
        private static readonly Version Win11OSVersion = new(10, 0, 22000, 0);

        public RIOTExportConfigDialog()
        {
            InitializeComponent();
            Text = RIOTExportEffect.StaticName;
        }

        protected override EffectConfigToken OnCreateInitialToken()
        {
            return new RIOTExportConfigToken();
        }

        protected override void OnLayout(LayoutEventArgs le)
        {
            int hMargin = LogicalToDeviceUnits(8);
            int vMargin = LogicalToDeviceUnits(8);

            infoLabel.Location = new System.Drawing.Point(hMargin, vMargin);
            infoLabel.Size = TextRenderer.MeasureText(infoLabel.Text,
                                                      infoLabel.Font,
                                                      new System.Drawing.Size(ClientSize.Width - infoLabel.Left - hMargin, int.MaxValue),
                                                      TextFormatFlags.WordBreak);
            infoLabel.PerformLayout();

            int clientWidth = infoLabel.Right + hMargin;
            int clientHeight = infoLabel.Bottom + vMargin;

            ClientSize = new System.Drawing.Size(clientWidth, clientHeight);

            base.OnLayout(le);
        }

        protected override void OnUpdateDialogFromToken(EffectConfigToken token)
        {
        }

        protected override void OnUpdateTokenFromDialog(EffectConfigToken dstToken)
        {
        }

        private void InitializeComponent()
        {
            infoLabel = new System.Windows.Forms.Label();
            SuspendLayout();
            //
            // infoLabel
            //
            infoLabel.AutoSize = true;
            infoLabel.Location = new System.Drawing.Point(13, 13);
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new System.Drawing.Size(183, 13);
            infoLabel.TabIndex = 1;
            infoLabel.Text = "RIOT will open in a separate window.";
            //
            // RIOTExportConfigDialog
            //
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            ClientSize = new System.Drawing.Size(368, 82);
            ControlBox = false;
            Controls.Add(infoLabel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Location = new System.Drawing.Point(0, 0);
            Name = "RIOTExportConfigDialog";
            ShowIcon = false;
            Controls.SetChildIndex(infoLabel, 0);
            ResumeLayout(false);
            PerformLayout();
        }

        private void ShowErrorMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>((string error) => Services.GetService<IExceptionDialogService>().ShowErrorDialog(this, error, string.Empty)),
                       message);
            }
            else
            {
                Services.GetService<IExceptionDialogService>().ShowErrorDialog(this, message, string.Empty);
            }
        }

        private void ShowErrorMessage(Exception exception)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Exception>((Exception ex) => Services.GetService<IExceptionDialogService>().ShowErrorDialog(this, ex)),
                       exception);
            }
            else
            {
                Services.GetService<IExceptionDialogService>().ShowErrorDialog(this, exception);
            }
        }

        protected override void OnLoaded()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                // Newer versions of RIOT parse the host process command line to determine the number of arguments
                // and activate its batch processing mode if it finds two or more arguments after the process name.
                //
                // Because of this we have to use the proxy process if Paint.NET was started with multiple arguments.
                if (System.Environment.GetCommandLineArgs().Length > 2)
                {
                    StartProxyProcessThread();
                }
                else
                {
                    StartRiotUIThread();
                }
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                if (System.Environment.OSVersion.Version >= Win11OSVersion)
                {
                    StartProxyProcessThread();
                }
                else
                {
                    ShowErrorMessage(Resources.Arm64OSRequirement);
                    CloseForm();
                }
            }
            else
            {
                ShowErrorMessage(string.Format(CultureInfo.CurrentCulture,
                                               Resources.UnsupportedPlatformFormat,
                                               RuntimeInformation.ProcessArchitecture));
                CloseForm();
            }
        }

        private static unsafe bool HasTransparency(IEffectInputBitmap<ColorBgra32> bitmap)
        {
            using (IBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock(bitmap.Bounds()))
            {
                RegionPtr<ColorBgra32> region = bitmapLock.AsRegionPtr();

                foreach (RegionRowPtr<ColorBgra32> row in region.Rows)
                {
                    ColorBgra32* ptr = row.Ptr;
                    ColorBgra32* endPtr = row.EndPtr;

                    while (ptr < endPtr)
                    {
                        if (ptr->A != 255)
                        {
                            return true;
                        }

                        ptr++;
                    }
                }
            }

            return false;
        }

        private void CloseForm()
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private static DIBInfo GetDibInfo(IEffectInputBitmap<ColorBgra32> bitmap)
        {
            SizeInt32 size = bitmap.Size;

            int width = size.Width;
            int height = size.Height;

            int dibBitsPerPixel = HasTransparency(bitmap) ? 32 : 24;

            int bmiHeaderSize = Marshal.SizeOf(typeof(NativeStructs.BITMAPINFOHEADER));
            long dibStride = ((((long)width * dibBitsPerPixel) + 31) & ~31) / 8;
            long dibImageDataSize = dibStride * height;

            // 24-bit and 32-bit DIB files do not have a color palette.
            long dibSize = bmiHeaderSize + dibImageDataSize;

            return new DIBInfo(bmiHeaderSize, width, height, dibStride, dibBitsPerPixel, dibSize);
        }

        private static unsafe void FillDib(IEffectInputBitmap<ColorBgra32> bitmap,
                                           Resolution documentResolution,
                                           DIBInfo info,
                                           void* baseAddress)
        {
            int bmiHeaderSize = info.BitmapInfoHeaderSize;
            int width = info.Width;
            int height = info.Height;
            long dibStride = info.Stride;
            int dibBitsPerPixel = info.BitsPerPixel;

            Vector2Int32 dpi = GetResolutionInDotsPerInch(documentResolution);

            NativeStructs.BITMAPINFOHEADER* bmiHeader = (NativeStructs.BITMAPINFOHEADER*)baseAddress;
            bmiHeader->biSize = (uint)bmiHeaderSize;
            bmiHeader->biWidth = width;
            bmiHeader->biHeight = height;
            bmiHeader->biPlanes = 1;
            bmiHeader->biBitCount = (ushort)dibBitsPerPixel;
            bmiHeader->biCompression = NativeConstants.BI_RGB;
            bmiHeader->biSizeImage = 0;
            // The RIOT developer documentation states that it expects the biXPelsPerMeter and biYPelsPerMeter
            // fields to use dots-per-inch, not dots-per-meter.
            bmiHeader->biXPelsPerMeter = dpi.X;
            bmiHeader->biYPelsPerMeter = dpi.Y;
            bmiHeader->biClrUsed = 0;
            bmiHeader->biClrImportant = 0;

            int lastBitmapRow = height - 1;
            int dibBytesPerPixel = dibBitsPerPixel / 8;

            byte* dibScan0 = (byte*)baseAddress + bmiHeaderSize;

            using (IBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock(bitmap.Bounds()))
            {
                RegionPtr<ColorBgra32> region = bitmapLock.AsRegionPtr();
                RegionRowPtrCollection<ColorBgra32> rows = region.Rows;

                for (int y = 0; y < height; y++)
                {
                    // Access the surface in the order needed for a bottom-up DIB.
                    ColorBgra32* src = rows[lastBitmapRow - y].Ptr;
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
                                throw new InvalidOperationException($"Unsupported {nameof(dibBitsPerPixel)} value: {dibBitsPerPixel}.");
                        }

                        src++;
                        dst += dibBytesPerPixel;
                    }
                }
            }

            static Vector2Int32 GetResolutionInDotsPerInch(Resolution resolution)
            {
                double xDpi;
                double yDpi;

                switch (resolution.Units)
                {
                    case MeasurementUnit.Pixel:
                        xDpi = yDpi = Document.DefaultDpi;
                        break;
                    case MeasurementUnit.Inch:
                        xDpi = resolution.X;
                        yDpi = resolution.Y;
                        break;
                    case MeasurementUnit.Centimeter:
                        xDpi = Document.DotsPerCmToDotsPerInch(resolution.X);
                        yDpi = Document.DotsPerCmToDotsPerInch(resolution.Y);
                        break;
                    default:
                        xDpi = yDpi = 0;
                        break;
                }

                return new Vector2Int32((int)Math.Round(xDpi), (int)Math.Round(yDpi));
            }
        }

        private static unsafe SafeMemoryMappedFileHandle CreateMemoryMappedDib(string name,
                                                                               DIBInfo info,
                                                                               IEffectInputBitmap<ColorBgra32> bitmap,
                                                                               Resolution documentResolution)
        {
            SafeMemoryMappedFileHandle handle = null;
            SafeMemoryMappedFileHandle temp = null;

            try
            {
                long fileMappingSize = info.TotalDIBSize;

                uint dwMaximumSizeHigh = (uint)(fileMappingSize >> 32);
                uint dwMaximumSizeLow = (uint)fileMappingSize;

                temp = SafeNativeMethods.CreateFileMappingW(new IntPtr(NativeConstants.INVALID_HANDLE_VALUE),
                                                            IntPtr.Zero,
                                                            NativeConstants.PAGE_READWRITE,
                                                            dwMaximumSizeHigh,
                                                            dwMaximumSizeLow,
                                                            name);
                if (temp.IsInvalid)
                {
                    throw new Win32Exception();
                }

                using (SafeMemoryMappedFileView view = SafeNativeMethods.MapViewOfFile(temp,
                                                                                       NativeConstants.FILE_MAP_WRITE,
                                                                                       0,
                                                                                       0,
                                                                                       0))
                {
                    if (view.IsInvalid)
                    {
                        throw new Win32Exception();
                    }

                    FillDib(bitmap, documentResolution, info, view.DangerousGetHandle().ToPointer());
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

        private void RunProxyProcess()
        {
            ErrorInfo errorInfo = null;

            try
            {
                string fileMappingName = "pdn_" + Guid.NewGuid().ToString();

                IEffectInputBitmap<ColorBgra32> bitmap = Environment.GetSourceBitmapBgra32();
                DIBInfo info = GetDibInfo(bitmap);
                Resolution documentResolution = Environment.Document.Resolution;

                using (SafeMemoryMappedFileHandle fileMappingHandle = CreateMemoryMappedDib(fileMappingName, info, bitmap, documentResolution))
                {
                    using (Process proc = new())
                    {
                        proc.StartInfo = new ProcessStartInfo(RiotProxyPath, fileMappingName);
                        proc.Start();

                        proc.WaitForExit();

                        switch (proc.ExitCode)
                        {
                            case 0:
                                // No error.
                                break;
                            case 1:
                                errorInfo = new(Resources.DIBLoadFailed);
                                break;
                            case 2:
                                errorInfo = new(Resources.OutOfMemory);
                                break;
                            case 3:
                                errorInfo = new(Resources.RIOTDllMissing);
                                break;
                            case 4:
                                errorInfo = new(Resources.RIOTEntrypointNotFound);
                                break;
                            case 5:
                                errorInfo = new(Resources.RIOTLoadFromDIBFailed);
                                break;
                            default:
                                errorInfo = new(string.Format(CultureInfo.InvariantCulture,
                                                              Resources.UnknownExitCodeFormat,
                                                              proc.ExitCode));
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorInfo = new(ex);
            }

            BeginInvoke(new Action<ErrorInfo>(RiotWorkerThreadFinished), errorInfo);
        }

        private void RiotWorkerThreadFinished(ErrorInfo errorInfo)
        {
            riotWorkerThread.Join();

            if (errorInfo != null)
            {
                if (errorInfo.Exception != null)
                {
                    ShowErrorMessage(errorInfo.Exception);
                }
                else if (!string.IsNullOrEmpty(errorInfo.Message))
                {
                    ShowErrorMessage(errorInfo.Message);
                }
            }

            CloseForm();
        }

        private unsafe void StartRiotUIThread()
        {
            riotWorkerThread = new Thread(RiotUIThreadDoWork);
            // RIOT may use COM, which requires STA.
            riotWorkerThread.SetApartmentState(ApartmentState.STA);
            riotWorkerThread.Start(Handle);
        }

        private unsafe void RiotUIThreadDoWork(object state)
        {
            nint parentWindowHandle = (nint)state;
            ErrorInfo errorInfo = null;

            try
            {
                IEffectInputBitmap<ColorBgra32> bitmap = Environment.GetSourceBitmapBgra32();
                DIBInfo info = GetDibInfo(bitmap);
                Resolution documentResolution = Environment.Document.Resolution;

                void* nativeDib = NativeMemory.Alloc((nuint)info.TotalDIBSize);

                try
                {
                    FillDib(bitmap, documentResolution, info, nativeDib);

                    try
                    {
                        if (!SafeNativeMethods.RIOT_LoadFromDIB_U(nativeDib, parentWindowHandle, string.Empty, 0))
                        {
                            errorInfo = new(Resources.RIOTLoadFromDIBFailed);
                        }
                    }
                    catch (DllNotFoundException)
                    {
                        errorInfo = new(Resources.RIOTDllMissing);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        errorInfo = new(Resources.RIOTEntrypointNotFound);
                    }
                }
                finally
                {
                    NativeMemory.Free(nativeDib);
                }
            }
            catch (Exception ex)
            {
                errorInfo = new(ex);
            }

            BeginInvoke(new Action<ErrorInfo>(RiotWorkerThreadFinished), errorInfo);
        }

        private void StartProxyProcessThread()
        {
            if (File.Exists(RiotProxyPath))
            {
                riotWorkerThread = new Thread(RunProxyProcess);
                riotWorkerThread.Start();
            }
            else
            {
                ShowErrorMessage(Resources.RIOTProxyNotFound);
                CloseForm();
            }
        }

        private sealed class DIBInfo
        {
            public DIBInfo(int bitmapInfoHeaderSize,
                           int width,
                           int height,
                           long stride,
                           int bitsPerPixel,
                           long totalDIBSize)
            {
                BitmapInfoHeaderSize = bitmapInfoHeaderSize;
                Width = width;
                Height = height;
                Stride = stride;
                BitsPerPixel = bitsPerPixel;
                TotalDIBSize = totalDIBSize;
            }

            public int BitmapInfoHeaderSize { get; }

            public int Width { get; }

            public int Height { get; }

            public long Stride { get; }

            public int BitsPerPixel { get;  }

            public long TotalDIBSize { get; }
        }

        private sealed class ErrorInfo
        {
            public ErrorInfo(Exception exception)
            {
                Exception = exception;
                Message = string.Empty;
            }

            public ErrorInfo(string message)
            {
                Exception = null;
                Message = message;
            }

            public Exception Exception { get; }

            public string Message { get; }
        }
    }
}
