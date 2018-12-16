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
using System.ComponentModel;

namespace RIOTProxy
{
    internal static class Memory
    {
        private static readonly IntPtr hHeap = SafeNativeMethods.GetProcessHeap();

        public static IntPtr Allocate(long size)
        {
            IntPtr block = SafeNativeMethods.HeapAlloc(hHeap, 0, new UIntPtr((ulong)size));

            if (block == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
            GC.AddMemoryPressure(size);

            return block;
        }

        public static void Free(IntPtr block)
        {
            long size = SafeNativeMethods.HeapSize(hHeap, 0, block).ToInt64();

            if (!SafeNativeMethods.HeapFree(hHeap, 0, block))
            {
                throw new Win32Exception();
            }

            GC.RemoveMemoryPressure(size);
        }
    }
}
