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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class SpriteRenderable : IPalettedRenderable, IModifyableRenderable, IFinalizedRenderable
	{
		public static readonly IEnumerable<IRenderable> None = Array.Empty<IRenderable>();

		readonly Sprite sprite;
		readonly WPos pos;
		readonly WVec offset;
		readonly int zOffset;
		readonly PaletteReference palette;
		readonly float scale;
		readonly float3 tint;
		readonly TintModifiers tintModifiers;
		readonly float alpha;
		readonly bool isDecoration;
		readonly BlendMode blendMode;
		public BlendMode BlendMode => blendMode;
		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha, float3 tint, TintModifiers tintModifiers, bool isDecoration, bool isShadow = false)
		{
			this.sprite = sprite;
			this.pos = pos;
			this.offset = offset;
			this.zOffset = zOffset;
			this.palette = palette;
			this.scale = scale;
			this.tint = tint;
			this.isDecoration = isDecoration;
			this.tintModifiers = tintModifiers;
			this.alpha = alpha;
			if (isShadow || (sprite.BlendMode == BlendMode.None && alpha < 0.9999f))
			{
				sprite.ChangeBlendMode(BlendMode.Alpha);
			}

			blendMode = sprite.BlendMode;

			// PERF: Remove useless palette assignments for RGBA sprites
			// HACK: This is working around the fact that palettes are defined on traits rather than sequences
			// and can be removed once this has been fixed
			if (sprite.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false))
				this.palette = null;
		}

		public WPos Pos => pos + offset;
		public WVec Offset => offset;
		public PaletteReference Palette => palette;
		public int ZOffset => zOffset;
		public bool IsDecoration => isDecoration;

		public float Alpha => alpha;
		public float3 Tint => tint;
		public TintModifiers TintModifiers => tintModifiers;

		public IPalettedRenderable WithPalette(PaletteReference newPalette) { return new SpriteRenderable(sprite, pos, offset, zOffset, newPalette, scale, alpha, tint, tintModifiers, isDecoration); }
		public IRenderable WithZOffset(int newOffset) { return new SpriteRenderable(sprite, pos, offset, newOffset, palette, scale, alpha, tint, tintModifiers, isDecoration); }
		public IRenderable OffsetBy(in WVec vec) { return new SpriteRenderable(sprite, pos + vec, offset, zOffset, palette, scale, alpha, tint, tintModifiers, isDecoration); }
		public IRenderable AsDecoration() { return new SpriteRenderable(sprite, pos, offset, zOffset, palette, scale, alpha, tint, tintModifiers, true); }

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new SpriteRenderable(sprite, pos, offset, zOffset, palette, scale, newAlpha, tint, tintModifiers, isDecoration);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new SpriteRenderable(sprite, pos, offset, zOffset, palette, scale, alpha, newTint, newTintModifiers, isDecoration);
		}

		float3 ScreenPosition(WorldRenderer wr)
		{
			var s = 0.5f * scale * sprite.Size;
			return wr.Screen3DPxPosition(pos) + wr.ScreenPxOffset(offset) - new float3((int)s.X, (int)s.Y, s.Z);
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var wsr = Game.Renderer.WorldSpriteRenderer;
			var t = alpha * tint;
			if (wr.TerrainLighting != null && (tintModifiers & TintModifiers.IgnoreWorldTint) == 0)
				t *= wr.TerrainLighting.TintAt(pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = alpha;
			if ((tintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			//wsr.DrawSprite(sprite, palette, ScreenPosition(wr), scale, t, a);

			var viewOffset = Game.Renderer.World3DRenderer.InverseCameraFrontMeterPerWPos * (zOffset + 1);

			if (sprite.SpriteMeshType == SpriteMeshType.Plane)
				wsr.DrawPlaneSprite(sprite, palette, Pos, viewOffset, scale, t, a);
			else if (sprite.SpriteMeshType == SpriteMeshType.Card)
				wsr.DrawCardSprite(sprite, palette, Pos, viewOffset, scale, t, a);
			else if (sprite.SpriteMeshType == SpriteMeshType.Board)
				wsr.DrawBoardSprite(sprite, palette, Pos, viewOffset, scale, t, a);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var pos = ScreenPosition(wr) + sprite.Offset;
			var bpos = ScreenPosition(wr);
			var tl = wr.Viewport.WorldToViewPx(pos);
			var br = wr.Viewport.WorldToViewPx(pos + sprite.Size);
			var ca = wr.Viewport.WorldToViewPx(pos + sprite.Size / 2);
			var cb = wr.Viewport.WorldToViewPx(bpos + sprite.Size / 2);
			Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);
			Game.Renderer.RgbaColorRenderer.DrawScreenLine(ca, cb, 2,Color.Azure);
			Game.Renderer.RgbaColorRenderer.DrawRect(cb - new int2(1,1), cb + new int2(1, 1), 1, Color.BlueViolet);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var screenOffset = ScreenPosition(wr) + sprite.Offset;
			return new Rectangle((int)screenOffset.X, (int)screenOffset.Y, (int)sprite.Size.X, (int)sprite.Size.Y);
		}
	}
}
