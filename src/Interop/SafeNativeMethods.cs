/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;

namespace SaveForWebRIOT.Interop
{
    [System.Security.SuppressUnmanagedCodeSecurity]
    internal static class SafeNativeMethods
    {
        [DllImport("RIOT.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern unsafe bool RIOT_LoadFromDIB_U(void* dib,
                                                              IntPtr parentWindowHandle,
                                                              [MarshalAs(UnmanagedType.LPWStr)] string fileName,
                                                              int flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeMemoryMappedFileHandle CreateFileMappingW(IntPtr hFile,
                                                                             IntPtr lpFileMappingAttributes,
                                                                             uint flProtect,
                                                                             uint dwMaximumSizeHigh,
                                                                             uint dwMaximumSizeLow,
                                                                             [MarshalAs(UnmanagedType.LPWStr)] string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe SafeMemoryMappedFileView MapViewOfFile(SafeMemoryMappedFileHandle hFileMappingObject,
                                                                             uint dwDesiredAccess,
                                                                             uint dwFileOffsetHigh,
                                                                             uint dwFileOffsetLow,
                                                                             UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);
    }
}
