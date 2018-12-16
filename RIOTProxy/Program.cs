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

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RIOTProxy
{
    class Program
    {
        private static class Status
        {
            internal const int NoError = 0;
            internal const int WICLoadFailed = 1;
            internal const int OutOfMemory = 2;
            internal const int RIOTLoadFailed = 3;
        }

        private static unsafe bool ImageHasTransparency(BitmapSource image)
        {
            if (image.Format == PixelFormats.Bgra32)
            {
                WriteableBitmap writeable = new WriteableBitmap(image);

                byte* scan0 = (byte*)writeable.BackBuffer.ToPointer();
                int stride = writeable.BackBufferStride;
                int width = image.PixelWidth;
                int height = image.PixelHeight;

                for (int y = 0; y < height; y++)
                {
                    byte* ptr = scan0 + (y * stride);

                    for (int x = 0; x < width; x++)
                    {
                        if (ptr[3] != 255)
                        {
                            return true;
                        }

                        ptr += 4;
                    }
                }
            }

            return false;
        }

        private static unsafe int CreateDIBFromBitmap(string fileName, out IntPtr dibHandle)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("filename");
            }
            dibHandle = IntPtr.Zero;

            try
            {
                BitmapFrame frame = BitmapFrame.Create(new Uri(fileName), BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);

                int width = frame.PixelWidth;
                int height = frame.PixelHeight;

                PixelFormat dibPixelFormat;

                if (ImageHasTransparency(frame))
                {
                    dibPixelFormat = PixelFormats.Bgra32;
                }
                else
                {
                    dibPixelFormat = PixelFormats.Bgr24;
                }

                int dibBitsPerPixel = dibPixelFormat.BitsPerPixel;

                int bmiHeaderSize = Marshal.SizeOf(typeof(NativeStructs.BITMAPINFOHEADER));
                const long dibPaletteSize = 0L;
                int dibStride = ((((width * dibBitsPerPixel) + 31) & ~31) >> 3);
                long dibImageDataSize = dibStride * height;

                long dibSize = bmiHeaderSize + dibPaletteSize + dibImageDataSize;

                try
                {
                    dibHandle = Memory.Allocate(dibSize);
                }
                catch (OutOfMemoryException)
                {
                    Console.WriteLine("Out of memory");
                    return Status.OutOfMemory;
                }

                NativeStructs.BITMAPINFOHEADER* bmiHeader = (NativeStructs.BITMAPINFOHEADER*)dibHandle.ToPointer();
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

                BitmapSource image;
                if (frame.Format != dibPixelFormat)
                {
                    image = new FormatConvertedBitmap(frame, dibPixelFormat, null, 0.0);
                }
                else
                {
                    image = frame;
                }

                WriteableBitmap writeable = new WriteableBitmap(image);

                byte* bitmapScan0 = (byte*)writeable.BackBuffer.ToPointer();
                int bitmapStride = writeable.BackBufferStride;
                int lastBitmapRow = height - 1;
                byte* dibScan0 = (byte*)dibHandle.ToPointer() + bmiHeaderSize;

                UIntPtr dibRowLength = new UIntPtr((ulong)width * (ulong)(dibBitsPerPixel >> 3));

                for (int y = 0; y < height; y++)
                {
                    // Access the bitmap in the order needed for a bottom-up DIB.
                    byte* src = bitmapScan0 + ((lastBitmapRow - y) * bitmapStride);
                    byte* dst = dibScan0 + (y * dibStride);

                    SafeNativeMethods.CopyMemory((void*)dst, (void*)src, dibRowLength);
                }
            }
            catch (FileFormatException ex)
            {
                Console.WriteLine("WIC returned an error loading the image: " + ex.Message);
                return Status.WICLoadFailed;
            }
            catch (IOException ex)
            {
                Console.WriteLine("WIC returned an error loading the image: " + ex.Message);
                return Status.WICLoadFailed;
            }
            catch (NotSupportedException ex) // WINCODEC_ERR_COMPONENTNOTFOUND
            {
                Console.WriteLine("WIC returned an error loading the image: " + ex.Message);
                return Status.WICLoadFailed;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("WIC returned an error loading the image: " + ex.Message);
                return Status.WICLoadFailed;
            }

            return Status.NoError;
        }

        [STAThread]
        static int Main(string[] args)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif

            if (args.Length < 1)
            {
                Console.WriteLine("Usage:  RiotProxy filename");
                return 0;
            }

            SafeNativeMethods.SetErrorMode(SafeNativeMethods.SetErrorMode(0U) | NativeConstants.SEM_FAILCRITICALERRORS | NativeConstants.SEM_NOGPFAULTERRORBOX);

            int status = Status.NoError;

            IntPtr hDIB = IntPtr.Zero;
            try
            {
                status = CreateDIBFromBitmap(args[0], out hDIB);
                if (status == Status.NoError)
                {
                    try
                    {
                        SafeNativeMethods.RIOT_LoadFromDIB_U(hDIB, IntPtr.Zero, string.Empty, 0);
                    }
                    catch (DllNotFoundException)
                    {
                        Console.WriteLine("RIOT.dll was not found.");
                        status = Status.RIOTLoadFailed;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        Console.WriteLine("The entry point 'RIOT_LoadFromDIB_U' was not found in RIOT.dll.");
                        status = Status.RIOTLoadFailed;
                    }
                }
            }
            finally
            {
                if (hDIB != IntPtr.Zero)
                {
                    IntPtr hDIB2 = hDIB;
                    hDIB = IntPtr.Zero;

                    Memory.Free(hDIB2);
                }
            }

            return status;
        }
    }
}
