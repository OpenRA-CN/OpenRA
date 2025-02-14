﻿#region Copyright & License Information
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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public class RenderMeshesInfo : TraitInfo, Requires<BodyOrientationInfo>
	{
		[Desc("Defaults to the actor name.")]
		public readonly string Image = null;

		//[Desc("Custom palette name")]
		//[PaletteReference]
		//public readonly string Palette = null;

		//[PaletteReference]
		//[Desc("Custom PlayerColorPalette: BaseName")]
		//public readonly string PlayerPalette = "player";

		//[PaletteReference]
		//public readonly string NormalsPalette = null;

		//[PaletteReference]
		//public readonly string ShadowPalette = null;

		[Desc("Change size.")]
		public readonly float Scale = 1;

		public readonly int ZOffset = 1;

		public override object Create(ActorInitializer init) { return new RenderMeshes(init.Self, this); }
	}

	public class RenderMeshes : IRender, ITick, INotifyOwnerChanged
	{
		public readonly RenderMeshesInfo Info;
		readonly List<MeshInstance> meshes = new List<MeshInstance>();

		readonly Actor self;
		readonly BodyOrientation body;
		Color remap;

		public RenderMeshes(Actor self, RenderMeshesInfo info)
		{
			this.self = self;
			Info = info;
			body = self.Trait<BodyOrientation>();
		}

		bool initializePalettes = true;
		public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { initializePalettes = true; }

		void ITick.Tick(Actor self)
		{

		}

		IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
		{
			if (initializePalettes)
			{
				remap = self.Owner.Color;
				initializePalettes = false;
			}

			return new IRenderable[]
			{
				new MeshRenderable(meshes, self.CenterPosition, Info.ZOffset, remap, Info.Scale)
			};
		}

		IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
		{
			var pos = self.CenterPosition;
			foreach (var c in meshes)
				if (c.IsVisible())
					yield return c.ScreenBounds(pos, wr, Info.Scale);
		}

		public string Image => Info.Image ?? self.Info.Name;

		public void Add(MeshInstance m)
		{
			meshes.Add(m);
		}

		public void Remove(MeshInstance m)
		{
			meshes.Remove(m);
		}
	}
}
