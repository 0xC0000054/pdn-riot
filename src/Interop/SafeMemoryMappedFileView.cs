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

using Microsoft.Win32.SafeHandles;

namespace SaveForWebRIOT.Interop
{
    internal sealed class SafeMemoryMappedFileView : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeMemoryMappedFileView() : base(true)
        {
        }

        protected override bool ReleaseHandle() => SafeNativeMethods.UnmapViewOfFile(handle);
    }
}
