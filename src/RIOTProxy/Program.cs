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

using RIOTProxy.Interop;
using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RIOTProxy
{
    internal class Program
    {
        private static class Status
        {
            internal const int NoError = 0;
            internal const int DIBLoadFailed = 1;
            internal const int OutOfMemory = 2;
            internal const int RIOTDllMissing = 3;
            internal const int RIOTEntrypointNotFound = 4;
        }

        private static int GetStatusForWin32Error(int win32Error)
        {
            int status;

            switch (win32Error)
            {
                case NativeConstants.ERROR_NOT_ENOUGH_MEMORY:
                case NativeConstants.ERROR_OUTOFMEMORY:
                    status = Status.OutOfMemory;
                    break;
                default:
                    status = Status.DIBLoadFailed;
                    break;
            }

            return status;
        }

        [STAThread]
        private static int Main(string[] args)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif

            if (args.Length < 2)
            {
                return 0;
            }

            SafeNativeMethods.SetErrorMode(SafeNativeMethods.SetErrorMode(0U) | NativeConstants.SEM_FAILCRITICALERRORS | NativeConstants.SEM_NOGPFAULTERRORBOX);

            int status = Status.NoError;

            using (SafeMemoryMappedFileHandle file = SafeNativeMethods.OpenFileMappingW(NativeConstants.FILE_MAP_READ, false, args[0]))
            {
                if (file.IsInvalid)
                {
                    status = GetStatusForWin32Error(Marshal.GetLastWin32Error());
                }
                else
                {
                    if (uint.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint dibSize))
                    {
                        using (SafeMemoryMappedFileView view = SafeNativeMethods.MapViewOfFile(file,
                                                                                               NativeConstants.FILE_MAP_READ,
                                                                                               0,
                                                                                               0,
                                                                                               new UIntPtr(dibSize)))
                        {
                            if (view.IsInvalid)
                            {
                                status = GetStatusForWin32Error(Marshal.GetLastWin32Error());
                            }
                            else
                            {
                                bool needsRelease = false;

                                view.DangerousAddRef(ref needsRelease);

                                try
                                {
                                    try
                                    {
                                        SafeNativeMethods.RIOT_LoadFromDIB_U(view.DangerousGetHandle(), IntPtr.Zero, string.Empty, 0);
                                    }
                                    catch (DllNotFoundException)
                                    {
                                        status = Status.RIOTDllMissing;
                                    }
                                    catch (EntryPointNotFoundException)
                                    {
                                        status = Status.RIOTEntrypointNotFound;
                                    }
                                }
                                finally
                                {
                                    if (needsRelease)
                                    {
                                        view.DangerousRelease();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        status = Status.DIBLoadFailed;
                    }
                }
            }

            return status;
        }
    }
}
