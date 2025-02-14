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
	public class WithModelWalkerBodyInfo : PausableConditionalTraitInfo, IRenderActorPreviewModelsInfo,  Requires<RenderModelsInfo>, Requires<IMoveInfo>, Requires<IFacingInfo>
	{
		public readonly string Sequence = "idle";

		[Desc("The speed of the walker's legs.")]
		public readonly int TickRate = 5;

		[Desc("Defines if the Model should have a shadow.")]
		public readonly bool ShowShadow = true;
		public override object Create(ActorInitializer init) { return new WithModelWalkerBody(init.Self, this); }

		public IEnumerable<ModelAnimation> RenderPreviewModels(
			ActorPreviewInitializer init, RenderModelsInfo rv, string image, Func<WRot> orientation, int facings, PaletteReference p)
		{
			var model = init.World.ModelCache.GetModelSequence(image, Sequence);
			var body = init.Actor.TraitInfo<BodyOrientationInfo>();
			var frame = init.GetValue<BodyAnimationFrameInit, uint>(this, 0);

			yield return new ModelAnimation(model, () => WVec.Zero,
				() => body.QuantizeOrientation(orientation(), facings),
				() => false, () => frame, ShowShadow);
		}
	}

	public class WithModelWalkerBody : PausableConditionalTrait<WithModelWalkerBodyInfo>, ITick, IActorPreviewInitModifier, IAutoMouseBounds
	{
		readonly IMove movement;
		readonly ModelAnimation modelAnimation;
		readonly RenderModels rv;

		uint tick, frame;
		readonly uint frames;

		public WithModelWalkerBody(Actor self, WithModelWalkerBodyInfo info)
			: base(info)
		{
			movement = self.Trait<IMove>();

			var body = self.Trait<BodyOrientation>();
			rv = self.Trait<RenderModels>();

			var model = self.World.ModelCache.GetModelSequence(rv.Image, info.Sequence);
			frames = model.Frames;
			modelAnimation = new ModelAnimation(model, () => WVec.Zero,
				() => body.QuantizeOrientation(self.Orientation),
				() => IsTraitDisabled, () => frame, info.ShowShadow);

			rv.Add(modelAnimation);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			if (movement.CurrentMovementTypes.HasMovementType(MovementType.Horizontal)
				|| movement.CurrentMovementTypes.HasMovementType(MovementType.Turn))
				tick++;

			if (tick < Info.TickRate)
				return;

			tick = 0;
			if (++frame == frames)
				frame = 0;
		}

		void IActorPreviewInitModifier.ModifyActorPreviewInit(Actor self, TypeDictionary inits)
		{
			inits.Add(new BodyAnimationFrameInit(frame));
		}

		Rectangle IAutoMouseBounds.AutoMouseoverBounds(Actor self, WorldRenderer wr)
		{
			return modelAnimation.ScreenBounds(self.CenterPosition, wr, rv.Info.Scale);
		}
	}

	public class BodyAnimationFrameInit : ValueActorInit<uint>
	{
		public BodyAnimationFrameInit(uint value)
			: base(value) { }
	}
}
