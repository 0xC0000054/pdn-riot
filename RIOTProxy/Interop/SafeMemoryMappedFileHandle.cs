﻿/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using Microsoft.Win32.SafeHandles;

namespace RIOTProxy.Interop
{
    internal sealed class SafeMemoryMappedFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeMemoryMappedFileHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle() => SafeNativeMethods.CloseHandle(handle);
    }
}
