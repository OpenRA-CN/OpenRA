#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.RA2.Traits.Render
{
	public class WithMindControllerPipsDecorationInfo : WithDecorationBaseInfo, Requires<MindControllerInfo>
	{
		[Desc("If non-zero, override the spacing between adjacent pips.")]
		public readonly int2 PipStride = int2.Zero;

		[Desc("Image that defines the pip sequences.")]
		public readonly string Image = "pips";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence used for indicating mindcontrolled units.")]
		public readonly string UsedSequence = "pip-green";

		[SequenceReference(nameof(Image))]
		[Desc("Sequence used for indicating unused mindcontrol slots.")]
		public readonly string UnusedSequence = "pip-empty";

		[PaletteReference]
		public readonly string Palette = "chrome";

		public override object Create(ActorInitializer init) { return new WithMindControllerPipsDecoration(init.Self, this); }
	}

	public class WithMindControllerPipsDecoration : WithDecorationBase<WithMindControllerPipsDecorationInfo>
	{
		readonly MindController mindController;
		readonly Animation pips;

		public WithMindControllerPipsDecoration(Actor self, WithMindControllerPipsDecorationInfo info)
			: base(self, info)
		{
			mindController = self.Trait<MindController>();
			pips = new Animation(self.World, info.Image);
		}

		string GetPipSequence(int i)
		{
			if (i < mindController.SlavesCount)
				return Info.UsedSequence;
			return Info.UnusedSequence;
		}

		protected override IEnumerable<IRenderable> RenderDecoration(Actor self, WorldRenderer wr, int2 screenPos)
		{
			pips.PlayRepeating(Info.UnusedSequence);
			var palette = wr.Palette(Info.Palette);
			var pipSize = pips.Image.Size.XY.ToInt2();
			var pipStride = Info.PipStride != int2.Zero ? Info.PipStride : new int2(pipSize.X, 0);
			screenPos -= pipSize / 2;
			for (var i = 0; i < mindController.Info.Capacity; i++)
			{
				pips.PlayRepeating(GetPipSequence(i));
				yield return new UISpriteRenderable(pips.Image, self.CenterPosition, screenPos, 0, palette);

				screenPos += pipStride;
			}
		}
	}
}
