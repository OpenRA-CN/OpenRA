#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class RailgunHelixRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos pos;
		readonly int zOffset;
		readonly Railgun railgun;
		readonly RailgunInfo info;
		readonly WDist helixRadius;
		readonly int alpha;
		readonly int ticks;
		readonly BlendMode blendMode;
		WAngle angle;
		public BlendMode BlendMode => blendMode;
		public RailgunHelixRenderable(WPos pos, int zOffset, Railgun railgun, RailgunInfo railgunInfo, int ticks, BlendMode blendMode)
		{
			this.pos = pos;
			this.zOffset = zOffset;
			this.railgun = railgun;
			info = railgunInfo;
			this.ticks = ticks;
			this.blendMode = blendMode;

			helixRadius = info.HelixRadius + new WDist(ticks * info.HelixRadiusDeltaPerTick);
			alpha = (railgun.HelixColor.A + ticks * info.HelixAlphaDeltaPerTick).Clamp(0, 255);
			angle = new WAngle(ticks * info.HelixAngleDeltaPerTick.Angle);
		}

		public WPos Pos => pos;
		public int ZOffset => zOffset;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return new RailgunHelixRenderable(pos, newOffset, railgun, info, ticks, blendMode); }
		public IRenderable OffsetBy(in WVec vec) { return new RailgunHelixRenderable(pos + vec, zOffset, railgun, info, ticks, blendMode); }
		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			if (railgun.ForwardStep == WVec.Zero)
				return;

			var screenWidth = wr.RenderVector(new WVec(info.HelixThickness.Length, 0, 0))[0];

			// Move forward from self to target to draw helix
			var centerPos = pos;
			var points = new float3[railgun.CycleCount * info.QuantizationCount];
			for (var i = points.Length - 1; i >= 0; i--)
			{
				// Make it narrower near the end.
				var rad = i < info.QuantizationCount ? helixRadius / 4 :
					i < 2 * info.QuantizationCount ? helixRadius / 2 :
					helixRadius;

				// Note: WAngle.Sin(x) = 1024 * Math.Sin(2pi/1024 * x)
				var u = rad.Length * angle.Cos() * railgun.LeftVector / (1024 * 1024)
					+ rad.Length * angle.Sin() * railgun.UpVector / (1024 * 1024);
				points[i] = wr.Render3DPosition(centerPos + u);

				centerPos += railgun.ForwardStep;
				angle += railgun.AngleStep;
			}

			Game.Renderer.WorldRgbaColorRenderer.DrawWorldLine(points, screenWidth, Color.FromArgb(alpha, railgun.HelixColor), blendMode: blendMode);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
