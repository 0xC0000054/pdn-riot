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
using PaintDotNet.Effects;
using PaintDotNet.Imaging;
using System.Drawing;

namespace SaveForWebRIOT
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class RIOTExportEffect : BitmapEffect
    {
        public static string StaticName
        {
            get
            {
                return "Save for Web with RIOT";
            }
        }

        public static Bitmap StaticIcon
        {
            get
            {
                return new Bitmap(typeof(RIOTExportEffect), "icon.png");
            }
        }

        public RIOTExportEffect() : base(StaticName, StaticIcon, "Tools", BitmapEffectOptions.Create() with { IsConfigurable = true })
        {
        }

        protected override IEffectConfigForm OnCreateConfigForm()
        {
            return new RIOTExportConfigDialog();
        }

        protected override unsafe void OnRender(IBitmapEffectOutput output)
        {
            using (IBitmapLock<ColorBgra32> dst = output.LockBgra32())
            {
                Environment.GetSourceBitmapBgra32().CopyPixels(dst.Buffer, dst.BufferStride, dst.BufferSize, output.Bounds);
            }
        }
    }
}
