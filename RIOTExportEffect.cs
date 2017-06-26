/////////////////////////////////////////////////////////////////////////////////
//
// RIOT Save for Web Effect Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (C) 2016-2017 Nicholas Hayes
// 
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing;

namespace SaveForWebRIOT
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class RIOTExportEffect : Effect
    {
        public static string StaticName
        {
            get
            {
                return "Save for Web with RIOT";
            }
        }

        public RIOTExportEffect() : base(StaticName, null, "Tools", EffectFlags.Configurable)
        {
        }

        public override EffectConfigDialog CreateConfigDialog()
        {
            return new RIOTExportConfigDialog();
        }

        public override void Render(EffectConfigToken parameters, RenderArgs dstArgs, RenderArgs srcArgs, Rectangle[] rois, int startIndex, int length)
        {
            dstArgs.Surface.CopySurface(srcArgs.Surface, rois, startIndex, length);
        }
    }
}
