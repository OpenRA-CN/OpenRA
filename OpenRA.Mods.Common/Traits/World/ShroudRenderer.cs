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
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	public class ShroudRendererInfo : TraitInfo
	{
		public readonly string Sequence = "shroud";
		[SequenceReference(nameof(Sequence))]
		public readonly string[] ShroudVariants = { "shroud" };

		[SequenceReference(nameof(Sequence))]
		public readonly string[] FogVariants = { "fog" };

		[PaletteReference]
		public readonly string ShroudPalette = "shroud";

		[PaletteReference]
		public readonly string FogPalette = "fog";

		[Desc("Bitfield of shroud directions for each frame. Lower four bits are",
			"corners clockwise from TL; upper four are edges clockwise from top")]
		public readonly int[] Index = { 12, 9, 8, 3, 1, 6, 4, 2, 13, 11, 7, 14 };

		[Desc("Use the upper four bits when calculating frame")]
		public readonly bool UseExtendedIndex = false;

		[SequenceReference(nameof(Sequence))]
		[Desc("Override for source art that doesn't define a fully shrouded tile")]
		public readonly string OverrideFullShroud = null;

		public readonly int OverrideShroudIndex = 15;

		[SequenceReference(nameof(Sequence))]
		[Desc("Override for source art that doesn't define a fully fogged tile")]
		public readonly string OverrideFullFog = null;

		public readonly int OverrideFogIndex = 15;

		public readonly BlendMode ShroudBlend = BlendMode.Alpha;
		public override object Create(ActorInitializer init) { return new ShroudRenderer(init.World, this); }
	}

	public sealed class ShroudRenderer : IRenderShroud, IWorldLoaded, INotifyActorDisposing
	{
		[Flags]
		enum Edges : byte
		{
			None = 0,
			TopLeft = 0x01,
			TopRight = 0x02,
			BottomRight = 0x04,
			BottomLeft = 0x08,
			AllCorners = TopLeft | TopRight | BottomRight | BottomLeft,
			TopSide = 0x10,
			RightSide = 0x20,
			BottomSide = 0x40,
			LeftSide = 0x80,
			AllSides = TopSide | RightSide | BottomSide | LeftSide,
			Top = TopSide | TopLeft | TopRight,
			Right = RightSide | TopRight | BottomRight,
			Bottom = BottomSide | BottomRight | BottomLeft,
			Left = LeftSide | TopLeft | BottomLeft,
			All = Top | Right | Bottom | Left
		}

		readonly struct TileInfo
		{
			//public readonly float3 ScreenPosition;
			public readonly WPos WorldPosition;
			public readonly byte Variant;

			public TileInfo(in WPos wpos, byte variant)
			{
				//ScreenPosition = screenPosition;
				WorldPosition = wpos;
				Variant = variant;
			}
		}

		readonly ShroudRendererInfo info;
		readonly World world;
		readonly Map map;
		readonly Edges notVisibleEdges;
		readonly byte variantStride;
		readonly byte[] edgesToSpriteIndexOffset;

		readonly CellLayer<TileInfo> tileInfos;
		readonly CellLayer<bool> cellsDirty;
		bool anyCellDirty;
		readonly (Sprite Sprite, float Scale, float Alpha)[] fogSprites, shroudSprites;

		Shroud shroud;
		Func<PPos, bool> visibleUnderShroud, visibleUnderFog;
		TerrainSpriteLayer shroudLayer, fogLayer;
		PaletteReference shroudPaletteReference, fogPaletteReference;
		bool disposed;

		public ShroudRenderer(World world, ShroudRendererInfo info)
		{
			if (info.ShroudVariants.Length != info.FogVariants.Length)
				throw new ArgumentException("ShroudRenderer must define the same number of shroud and fog variants!", nameof(info));

			if ((info.OverrideFullFog == null) ^ (info.OverrideFullShroud == null))
				throw new ArgumentException("ShroudRenderer cannot define overrides for only one of shroud or fog!", nameof(info));

			if (info.ShroudVariants.Length > byte.MaxValue)
				throw new ArgumentException("ShroudRenderer cannot define this many shroud and fog variants.", nameof(info));

			if (info.Index.Length >= byte.MaxValue)
				throw new ArgumentException("ShroudRenderer cannot define this many indexes for shroud directions.", nameof(info));

			this.info = info;
			this.world = world;
			map = world.Map;

			tileInfos = new CellLayer<TileInfo>(map);

			cellsDirty = new CellLayer<bool>(map);
			anyCellDirty = true;

			// Load sprite variants
			var variantCount = info.ShroudVariants.Length;
			variantStride = (byte)(info.Index.Length + (info.OverrideFullShroud != null ? 1 : 0));
			shroudSprites = new (Sprite, float, float)[variantCount * variantStride];
			fogSprites = new (Sprite, float, float)[variantCount * variantStride];

			var sequenceProvider = map.Rules.Sequences;
			for (var j = 0; j < variantCount; j++)
			{
				var shroudSequence = sequenceProvider.GetSequence(info.Sequence, info.ShroudVariants[j]);
				var fogSequence = sequenceProvider.GetSequence(info.Sequence, info.FogVariants[j]);
				for (var i = 0; i < info.Index.Length; i++)
				{
					shroudSprites[j * variantStride + i] = (shroudSequence.GetSprite(i), shroudSequence.Scale, shroudSequence.GetAlpha(i));
					fogSprites[j * variantStride + i] = (fogSequence.GetSprite(i), fogSequence.Scale, fogSequence.GetAlpha(i));
				}

				if (info.OverrideFullShroud != null)
				{
					var i = (j + 1) * variantStride - 1;
					shroudSequence = sequenceProvider.GetSequence(info.Sequence, info.OverrideFullShroud);
					fogSequence = sequenceProvider.GetSequence(info.Sequence, info.OverrideFullFog);
					shroudSprites[i] = (shroudSequence.GetSprite(0), shroudSequence.Scale, shroudSequence.GetAlpha(0));
					fogSprites[i] = (fogSequence.GetSprite(0), fogSequence.Scale, fogSequence.GetAlpha(0));
				}
			}

			// Mapping of shrouded directions -> sprite index
			edgesToSpriteIndexOffset = new byte[(byte)(info.UseExtendedIndex ? Edges.All : Edges.AllCorners) + 1];
			for (var i = 0; i < info.Index.Length; i++)
				edgesToSpriteIndexOffset[info.Index[i]] = (byte)i;

			if (info.OverrideFullShroud != null)
				edgesToSpriteIndexOffset[info.OverrideShroudIndex] = (byte)(variantStride - 1);

			notVisibleEdges = info.UseExtendedIndex ? Edges.AllSides : Edges.AllCorners;

			world.RenderPlayerChanged += WorldOnRenderPlayerChanged;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			// Initialize tile cache
			// This includes the region outside the visible area to cover any sprites peeking outside the map
			foreach (var uv in w.Map.AllCells.MapCoords)
			{
				var pos = w.Map.CenterOfCell(uv.ToCPos(map));
				//var screen = wr.Screen3DPosition(pos - new WVec(0, 0, pos.Z));
				var variant = (byte)Game.CosmeticRandom.Next(info.ShroudVariants.Length);
				tileInfos[uv] = new TileInfo(pos - new WVec(0, 0, pos.Z), variant);
			}

			// All tiles are visible in the editor
			if (w.Type == WorldType.Editor)
				visibleUnderShroud = _ => true;
			else
				visibleUnderShroud = puv => map.Contains(puv);

			visibleUnderFog = puv => map.Contains(puv);

			var shroudBlend = shroudSprites[0].Sprite.BlendMode;
			if (shroudSprites.Any(s => s.Sprite.BlendMode != shroudBlend))
				throw new InvalidDataException("Shroud sprites must all use the same blend mode.");

			var fogBlend = fogSprites[0].Sprite.BlendMode;
			if (fogSprites.Any(s => s.Sprite.BlendMode != fogBlend))
				throw new InvalidDataException("Fog sprites must all use the same blend mode.");

			var emptySprite = new Sprite(shroudSprites[0].Sprite.Sheet, Rectangle.Empty, TextureChannel.Alpha, spriteMeshType: SpriteMeshType.Plane);
			shroudPaletteReference = wr.Palette(info.ShroudPalette);
			fogPaletteReference = wr.Palette(info.FogPalette);
			shroudLayer = new TerrainSpriteLayer(w, wr, emptySprite, shroudBlend, false);
			fogLayer = new TerrainSpriteLayer(w, wr, emptySprite, fogBlend, false);

			WorldOnRenderPlayerChanged(world.RenderPlayer);
		}

		Edges GetEdges(PPos puv, Func<PPos, bool> isVisible)
		{
			if (!isVisible(puv))
				return notVisibleEdges;

			var cell = ((MPos)puv).ToCPos(map);

			// If a side is shrouded then we also count the corners.
			var edge = Edges.None;
			if (!isVisible((PPos)(cell + new CVec(0, -1)).ToMPos(map))) edge |= Edges.Top;
			if (!isVisible((PPos)(cell + new CVec(1, 0)).ToMPos(map))) edge |= Edges.Right;
			if (!isVisible((PPos)(cell + new CVec(0, 1)).ToMPos(map))) edge |= Edges.Bottom;
			if (!isVisible((PPos)(cell + new CVec(-1, 0)).ToMPos(map))) edge |= Edges.Left;

			var ucorner = edge & Edges.AllCorners;
			if (!isVisible((PPos)(cell + new CVec(-1, -1)).ToMPos(map))) edge |= Edges.TopLeft;
			if (!isVisible((PPos)(cell + new CVec(1, -1)).ToMPos(map))) edge |= Edges.TopRight;
			if (!isVisible((PPos)(cell + new CVec(1, 1)).ToMPos(map))) edge |= Edges.BottomRight;
			if (!isVisible((PPos)(cell + new CVec(-1, 1)).ToMPos(map))) edge |= Edges.BottomLeft;

			// RA provides a set of frames for tiles with shrouded
			// corners but unshrouded edges. We want to detect this
			// situation without breaking the edge -> corner enabling
			// in other combinations. The XOR turns off the corner
			// bits that are enabled twice, which gives the behavior
			// we want here.
			return info.UseExtendedIndex ? edge ^ ucorner : edge & Edges.AllCorners;
		}

		void WorldOnRenderPlayerChanged(Player player)
		{
			var newShroud = player?.Shroud;

			if (shroud != newShroud)
			{
				if (shroud != null)
					shroud.OnShroudChanged -= UpdateShroudCell;

				if (newShroud != null)
				{
					visibleUnderShroud = puv => newShroud.IsExplored(puv);
					visibleUnderFog = puv => newShroud.IsVisible(puv);
					newShroud.OnShroudChanged += UpdateShroudCell;
				}
				else
				{
					visibleUnderShroud = puv => map.Contains(puv);
					visibleUnderFog = puv => map.Contains(puv);
				}

				shroud = newShroud;
			}

			// Dirty the full projected space so the cells outside
			// the map bounds can be initialized as fully shrouded.
			cellsDirty.Clear(true);
			anyCellDirty = true;
			var tl = new PPos(0, 0);
			var br = new PPos(map.MapSize.X - 1, map.MapSize.Y - 1);
			UpdateShroud(new ProjectedCellRegion(map, tl, br));
		}

		void UpdateShroud(IEnumerable<PPos> region)
		{
			if (!anyCellDirty)
				return;

			foreach (var puv in region)
			{
				var uv = (MPos)puv;
				if (!cellsDirty[uv] || !tileInfos.Contains(uv))
					continue;

				cellsDirty[uv] = false;

				TileInfo tileInfo = tileInfos[uv];
				var shroudSprite = GetSprite(shroudSprites, GetEdges(puv, visibleUnderShroud), tileInfo.Variant);
				var shroudPos = tileInfo.WorldPosition;//tileInfo.ScreenPosition;
				//if (shroudSprite.Sprite != null)
				//	shroudPos += shroudSprite.Sprite.Offset - 0.5f * shroudSprite.Sprite.Size;

				var fogSprite = GetSprite(fogSprites, GetEdges(puv, visibleUnderFog), tileInfo.Variant);
				var fogPos = tileInfo.WorldPosition;
				//if (fogSprite.Sprite != null)
				//	fogPos += fogSprite.Sprite.Offset - 0.5f * fogSprite.Sprite.Size;

				shroudLayer.Update(uv, shroudSprite.Sprite, shroudPaletteReference, shroudPos, shroudSprite.Scale, shroudSprite.Alpha, true, 0);
				fogLayer.Update(uv, fogSprite.Sprite, fogPaletteReference, fogPos, fogSprite.Scale, fogSprite.Alpha, true, 0);
			}

			anyCellDirty = false;
		}

		void IRenderShroud.RenderShroud(WorldRenderer wr)
		{
			UpdateShroud(map.ProjectedCells);
			fogLayer.Draw(wr.Viewport);
			shroudLayer.Draw(wr.Viewport);
		}

		void UpdateShroudCell(PPos puv)
		{
			var uv = (MPos)puv;
			cellsDirty[uv] = true;
			anyCellDirty = true;
			var cell = uv.ToCPos(map);
			foreach (var direction in CVec.Directions)
				if (map.Contains((PPos)(cell + direction).ToMPos(map)))
					cellsDirty[cell + direction] = true;
		}

		(Sprite Sprite, float Scale, float Alpha) GetSprite((Sprite, float, float)[] sprites, Edges edges, int variant)
		{
			if (edges == Edges.None)
				return (null, 1f, 1f);

			return sprites[variant * variantStride + edgesToSpriteIndexOffset[(byte)edges]];
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			shroudLayer.Dispose();
			fogLayer.Dispose();
			disposed = true;
		}
	}
}
