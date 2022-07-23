/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2018, 2021, 2022 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

namespace RIOTProxy.Interop
{
    internal static class NativeConstants
    {
        internal const int ERROR_NOT_ENOUGH_MEMORY = 8;
        internal const int ERROR_OUTOFMEMORY = 14;

        internal const int FILE_MAP_READ = 4;

        internal const uint SEM_FAILCRITICALERRORS = 1U;
        internal const uint SEM_NOGPFAULTERRORBOX = 2U;
    }
}
