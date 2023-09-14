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

using System;

namespace SaveForWebRIOT
{
    internal static class PluginIconUtil
    {
        private static readonly ValueTuple<int, string>[] AvailableIcons = new ValueTuple<int, string>[]
        {
            (96, "Resources.icons.flame-96.png"),
            (120, "Resources.icons.flame-120.png"),
            (144, "Resources.icons.flame-144.png"),
            (192, "Resources.icons.flame-192.png"),
            (384, "Resources.icons.flame-384.png"),
        };

        internal static string GetIconResourceNameForDpi(int dpi)
        {
            for (int i = 0; i < AvailableIcons.Length; i++)
            {
                ValueTuple<int, string> icon = AvailableIcons[i];

                if (icon.Item1 >= dpi)
                {
                    return icon.Item2;
                }
            }

            return "Resources.Icons.flame-384.png";
        }
    }
}
