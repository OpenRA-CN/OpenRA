#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.Common.Effects
{
	public class ContrailFader : IEffect
	{
		readonly WPos pos;
		readonly ContrailRenderable trail;
		public WAngle AngleStep = WAngle.Zero;
		public WDist SpreadStep = WDist.Zero;
		public WAngle SpreadAngle = WAngle.Zero;
		public WVec LeftVector;
		public WVec UpVector;

		int ticks;

		public ContrailFader(WPos pos, ContrailRenderable trail)
		{
			this.pos = pos;
			this.trail = trail;
		}

		public void Tick(World world)
		{
			if (ticks++ == trail.Length)
				world.AddFrameEndTask(w => w.Remove(this));

			var moveStep = WVec.Zero;
			if (SpreadStep != WDist.Zero)
			{
				// Note: WAngle.Sin(x) = 1024 * Math.Sin(2pi/1024 * x)
				moveStep = SpreadStep.Length * SpreadAngle.Cos() * LeftVector / (1024 * 1024)
					+ SpreadStep.Length * SpreadAngle.Sin() * UpVector / (1024 * 1024);

				SpreadAngle += AngleStep;
			}

			trail.Update(pos, moveStep);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			yield return trail;
		}
	}
}
