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

using PaintDotNet.Effects;

namespace SaveForWebRIOT
{
    public sealed class RIOTExportConfigToken : EffectConfigToken
    {
        public RIOTExportConfigToken()
        {
        }

        private RIOTExportConfigToken(RIOTExportConfigToken cloneMe)
        {
        }

        public override object Clone()
        {
            return new RIOTExportConfigToken(this);
        }
    }
}
