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

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits.Render
{
	// TODO: This trait is hacky and should go away as soon as we support granting a condition on docking, in favor of toggling two regular WithModelBodies
	public class WithModelUnloadBodyInfo : TraitInfo, IRenderActorPreviewModelsInfo, Requires<RenderModelsInfo>
	{
		[Desc("Model sequence name to use when docked to a refinery.")]
		public readonly string UnloadSequence = "unload";

		[Desc("Model sequence name to use when undocked from a refinery.")]
		public readonly string IdleSequence = "idle";

		[Desc("Defines if the Model should have a shadow.")]
		public readonly bool ShowShadow = true;

		public override object Create(ActorInitializer init) { return new WithModelUnloadBody(init.Self, this); }

		public IEnumerable<ModelAnimation> RenderPreviewModels(
			ActorPreviewInitializer init, RenderModelsInfo rv, string image, Func<WRot> orientation, int facings, PaletteReference p)
		{
			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var model = init.World.ModelCache.GetModelSequence(image, IdleSequence);
			yield return new ModelAnimation(model, () => WVec.Zero,
				() => body.QuantizeOrientation(orientation(), facings),
				() => false, () => 0, ShowShadow);
		}
	}

	public class WithModelUnloadBody : IAutoMouseBounds
	{
		public bool Docked;

		readonly ModelAnimation modelAnimation;
		readonly RenderModels rv;

		public WithModelUnloadBody(Actor self, WithModelUnloadBodyInfo info)
		{
			var body = self.Trait<BodyOrientation>();
			rv = self.Trait<RenderModels>();

			var idleModel = self.World.ModelCache.GetModelSequence(rv.Image, info.IdleSequence);
			modelAnimation = new ModelAnimation(idleModel, () => WVec.Zero,
				() => body.QuantizeOrientation(self.Orientation),
				() => Docked,
				() => 0, info.ShowShadow);

			rv.Add(modelAnimation);

			var unloadModel = self.World.ModelCache.GetModelSequence(rv.Image, info.UnloadSequence);
			rv.Add(new ModelAnimation(unloadModel, () => WVec.Zero,
				() => body.QuantizeOrientation(self.Orientation),
				() => !Docked,
				() => 0, info.ShowShadow));
		}

		Rectangle IAutoMouseBounds.AutoMouseoverBounds(Actor self, WorldRenderer wr)
		{
			return modelAnimation.ScreenBounds(self.CenterPosition, wr, rv.Info.Scale);
		}
	}
}
