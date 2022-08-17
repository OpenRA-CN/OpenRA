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
using GlmSharp;
using OpenRA.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public static class Util
	{
		// yes, our channel order is nuts.
		static readonly int[] ChannelMasks = { 2, 1, 0, 3 };

		//Meow TODO: use the "rotation" on our graphic 
		public static int FastCreateCard(Vertex[] vertices,
			in WPos inPos, in vec3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Card)
			{
				throw new Exception("sprite's mesh type is not card");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			// sprite only has horizental part
			if (topBottom.X < 0)
			{
				//float3 leftBack = new float3(position.x + leftRight.X, position.y - topBottom.X / Game.Renderer.World3DRenderer.CosCameraPitch, position.z);
				//float3 rightBack = new float3(position.x + leftRight.Y, leftBack.Y, position.z);
				//float3 leftFront = new float3(leftBack.X, position.y + topBottom.Y / Game.Renderer.World3DRenderer.CosCameraPitch, position.z);
				//float3 rightFront = new float3(rightBack.X, leftFront.Y, position.z);

				float3 leftBack = new float3(position.x + scale * r.leftBack.X, position.y + scale * r.leftBack.Y, position.z);
				float3 rightBack = new float3(position.x + leftRight.Y, leftBack.Y, position.z);
				float3 leftFront = new float3(leftBack.X, position.y + scale * r.leftFront.Y, position.z);
				float3 rightFront = new float3(rightBack.X, leftFront.Y, position.z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightBack, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else if (topBottom.Y < 0) // sprite only has vertical part
			{
				//float3 leftTop = new float3(position.x + leftRight.X, position.y, position.z + (topBottom.X) / Game.Renderer.World3DRenderer.SinCameraPitch);
				//float3 rightTop = new float3(position.x + leftRight.Y, position.y, leftTop.Z);
				//float3 leftBottom = new float3(leftTop.X, position.y, position.z - (topBottom.Y) / Game.Renderer.World3DRenderer.SinCameraPitch);
				//float3 rightBottom = new float3(rightTop.X, position.y, leftBottom.Z);

				float3 leftTop = new float3(position.x + scale * r.leftTop.X, position.y, position.z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.x + leftRight.Y, position.y, leftTop.Z);
				float3 leftBottom = new float3(leftTop.X, position.y, position.z + scale * r.leftBottom.Z);
				float3 rightBottom = new float3(rightTop.X, position.y, leftBottom.Z);

				float sl = 0;
				float st = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				return 6;
			}
			else
			{
				float3 leftTop = new float3(position.x + scale * r.leftTop.X, position.y, position.z + scale * r.leftTop.Z);
				float3 rightTop = new float3(position.x + leftRight.Y, position.y, leftTop.Z);
				float3 leftBase = new float3(leftTop.X, position.y, position.z);
				float3 rightBase = new float3(rightTop.X, position.y, position.z);
				float3 leftFront = new float3(leftTop.X, position.y + scale * r.leftFront.Y, position.z);
				float3 rightFront = new float3(rightTop.X, leftFront.Y, position.z);

				float ycut = topBottom.X / (ssziehalf.Y * 2);

				float sl = 0;
				float st = 0;
				float sbase = 0;
				float sr = 0;
				float sb = 0;

				// See combined.vert for documentation on the channel attribute format
				var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
				attribC |= samplers.X << 6;
				if (r is SpriteWithSecondaryData ss)
				{
					sl = ss.SecondaryLeft;
					st = ss.SecondaryTop;
					sr = ss.SecondaryRight;
					sb = ss.SecondaryBottom;

					sbase = st - (st - sb) * ycut;

					attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
					attribC |= samplers.Y << 9;
				}

				var fAttribC = (float)attribC;
				float baseY = r.Top - (r.Top - r.Bottom) * ycut;

				vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 2] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 3] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 4] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 6] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 7] = new Vertex(rightBase, r.Right, baseY, sr, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 8] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

				vertices[nv + 9] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 10] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
				vertices[nv + 11] = new Vertex(leftBase, r.Left, baseY, sl, sbase, paletteTextureIndex, fAttribC, tint, alpha);
				return 12;
			}
		}

		public static void FastCreatePlane(Vertex[] vertices,
			in WPos inPos, in vec3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Plane)
			{
				throw new Exception("sprite's mesh type is not plane");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftBack = new float3(position.x + leftRight.X, position.y + scale * r.leftBack.Y, position.z);
			float3 rightBack = new float3(position.x + leftRight.Y, leftBack.Y, position.z);
			float3 leftFront = new float3(leftBack.X, position.y + scale * r.leftFront.Y, position.z);
			float3 rightFront = new float3(rightBack.X, leftFront.Y, position.z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightBack, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 3] = new Vertex(rightFront, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(leftFront, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(leftBack, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateBoard(Vertex[] vertices,
			in WPos inPos, in vec3 viewOffset,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Board)
			{
				throw new Exception("sprite's mesh type is not board");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftTop = new float3(position.x + leftRight.X, position.y, position.z + scale * r.leftTop.Z);
			float3 rightTop = new float3(position.x + leftRight.Y, position.y, leftTop.Z);
			float3 leftBottom = new float3(leftTop.X, position.y, position.z + scale * r.leftBottom.Z);
			float3 rightBottom = new float3(rightTop.X, position.y, leftBottom.Z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(rightTop, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 3] = new Vertex(rightBottom, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(leftBottom, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(leftTop, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateTile(MapVertex[] vertices, float3[] verticesColor,
			in float3 mpos, in float3 tpos, in float3 bpos, in float3 lpos, in float3 rpos,
			in float3 mnml, in float3 tnml, in float3 bnml, in float3 lnml, in float3 rnml,
			in float3 mtint, in float3 ttint, in float3 btint, in float3 ltint, in float3 rtint,
			in float3 tmrNml, in float3 mbrNml, in float3 tlmNml, in float3 mlbNml,
			Sprite r, int2 samplers, float paletteTextureIndex, float scale,
			in float3 tint, float alpha, int nv, bool rotation = true)
		{
			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			float sbaseTB = 0;
			float sbaseRL = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				sbaseTB = st - (st - sb) * 0.5f;
				sbaseRL = sr - (sr - sl) * 0.5f;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			float baseY = r.Top - (r.Top - r.Bottom) * 0.5f;
			float baseX = r.Right - (r.Right - r.Left) * 0.5f;

			// rotate isomatric tile to rect
			if (rotation)
			{
				float top = r.Top + (r.Bottom - r.Top) * 0.035f;
				float bottom = r.Bottom + (r.Top - r.Bottom) * 0.035f;
				float left = r.Left + (r.Right - r.Left) * 0.035f;
				float right = r.Right + (r.Left - r.Right) * 0.035f;
				//float top = r.Top;// + (r.Bottom - r.Top) * 0.033f;
				//float bottom = r.Bottom;// + (r.Top - r.Bottom) * 0.033f;
				//float left = r.Left;// + (r.Right - r.Left) * 0.033f;
				//float right = r.Right;// + (r.Left - r.Right) * 0.033f;

				vertices[nv] = new MapVertex(tpos, tnml, tmrNml, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, ttint, alpha);
				vertices[nv + 1] = new MapVertex(mpos, mnml, tmrNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 2] = new MapVertex(rpos, rnml, tmrNml, right, baseY, sr, sbaseTB, paletteTextureIndex, fAttribC, rtint, alpha);

				vertices[nv + 3] = new MapVertex(mpos, mnml, mbrNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 4] = new MapVertex(bpos, bnml, mbrNml, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, btint, alpha);
				vertices[nv + 5] = new MapVertex(rpos, rnml, mbrNml, right, baseY, sr, sbaseTB, paletteTextureIndex, fAttribC, rtint, alpha);

				vertices[nv + 6] = new MapVertex(tpos, tnml, tlmNml, baseX, top, sbaseRL, st, paletteTextureIndex, fAttribC, ttint, alpha);
				vertices[nv + 7] = new MapVertex(lpos, lnml, tlmNml, left, baseY, sl, sbaseTB, paletteTextureIndex, fAttribC, ltint, alpha);
				vertices[nv + 8] = new MapVertex(mpos, mnml, tlmNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);

				vertices[nv + 9] = new MapVertex(mpos, mnml, mlbNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 10] = new MapVertex(lpos, lnml, mlbNml, left, baseY, sl, sbaseTB, paletteTextureIndex, fAttribC, ltint, alpha);
				vertices[nv + 11] = new MapVertex(bpos, bnml, mlbNml, baseX, bottom, sbaseRL, sb, paletteTextureIndex, fAttribC, btint, alpha);

			}
			else
			{
				vertices[nv] = new MapVertex(tpos, tnml, tmrNml, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, ttint, alpha);
				vertices[nv + 1] = new MapVertex(mpos, mnml, tmrNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 2] = new MapVertex(rpos, rnml, tmrNml, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, rtint, alpha);

				vertices[nv + 3] = new MapVertex(mpos, mnml, mbrNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 4] = new MapVertex(bpos, bnml, mbrNml, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, btint, alpha);
				vertices[nv + 5] = new MapVertex(rpos, rnml, mbrNml, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, rtint, alpha);

				vertices[nv + 6] = new MapVertex(tpos, tnml, tlmNml, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, ttint, alpha);
				vertices[nv + 7] = new MapVertex(lpos, lnml, tlmNml, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, ltint, alpha);
				vertices[nv + 8] = new MapVertex(mpos, mnml, tlmNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);

				vertices[nv + 9] = new MapVertex(mpos, mnml, mlbNml, baseX, baseY, sbaseRL, sbaseTB, paletteTextureIndex, fAttribC, mtint, alpha);
				vertices[nv + 10] = new MapVertex(lpos, lnml, mlbNml, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, ltint, alpha);
				vertices[nv + 11] = new MapVertex(bpos, bnml, mlbNml, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, btint, alpha);
			}

			verticesColor[nv] = ttint;
			verticesColor[nv + 1] = mtint;
			verticesColor[nv + 2] = rtint;

			verticesColor[nv + 3] = mtint;
			verticesColor[nv + 4] = btint;
			verticesColor[nv + 5] = rtint;

			verticesColor[nv + 6] = ttint;
			verticesColor[nv + 7] = ltint;
			verticesColor[nv + 8] = mtint;

			verticesColor[nv + 9] = mtint;
			verticesColor[nv + 10] = ltint;
			verticesColor[nv + 11] = btint;
		}

		public static void FastCreateTilePlane(MapVertex[] vertices,
				in float3 mnml,
				in WPos inPos, in vec3 viewOffset,
				Sprite r, int2 samplers, float paletteTextureIndex, float scale,
				in float3 tint, float alpha, int nv, float rotation = 0f)
		{
			if (r.HasMeshCreateInfo)
			{
				if (!r.UpdateMeshInfo())
					throw new Exception("invalide create mesh time: sprite has not create mesh data");
			}

			if (r.SpriteMeshType != SpriteMeshType.Plane)
			{
				throw new Exception("sprite's mesh type is not plane");
			}

			if (scale < 0)
			{
				throw new Exception("invalide create mesh scale: only positve value supported");
			}

			float3 ssziehalf = scale * r.Ssizehalf;
			float3 soffset = scale * r.Soffset;
			float2 leftRight = scale * r.LeftRight;
			float2 topBottom = scale * r.TopBottom; // In general, both top and bottom are positive

			var position = Game.Renderer.World3DRenderer.Get3DRenderPositionFromWPos(inPos);
			position += viewOffset;

			float3 leftBack = new float3(position.x + leftRight.X, position.y + scale * r.leftBack.Y, position.z);
			float3 rightBack = new float3(position.x + leftRight.Y, leftBack.Y, position.z);
			float3 leftFront = new float3(leftBack.X, position.y + scale * r.leftFront.Y, position.z);
			float3 rightFront = new float3(rightBack.X, leftFront.Y, position.z);

			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;

			vertices[nv + 2] = new MapVertex(leftBack, mnml, mnml, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new MapVertex(rightBack, mnml, mnml, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv] = new MapVertex(rightFront, mnml, mnml, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);

			vertices[nv + 5] = new MapVertex(rightFront, mnml, mnml, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new MapVertex(leftFront, mnml, mnml, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 3] = new MapVertex(leftBack, mnml, mnml, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCreateQuad(Vertex[] vertices, in float3 o, Sprite r, int2 samplers, float paletteTextureIndex, int nv,
					in float3 size, in float3 tint, float alpha, float rotation = 0f)
		{
			float3 a, b, c, d;

			// Rotate sprite if rotation angle is not equal to 0
			if (rotation != 0f)
			{
				var center = o + 0.5f * size;
				var angleSin = (float)Math.Sin(-rotation);
				var angleCos = (float)Math.Cos(-rotation);

				// Rotated offset for +/- x with +/- y
				var ra = 0.5f * new float3(
					size.X * angleCos - size.Y * angleSin,
					size.X * angleSin + size.Y * angleCos,
					(size.X * angleSin + size.Y * angleCos) * size.Z / size.Y);

				// Rotated offset for +/- x with -/+ y
				var rb = 0.5f * new float3(
					size.X * angleCos + size.Y * angleSin,
					size.X * angleSin - size.Y * angleCos,
					(size.X * angleSin - size.Y * angleCos) * size.Z / size.Y);

				a = center - ra;
				b = center + rb;
				c = center + ra;
				d = center - rb;
			}
			else
			{
				a = o;
				b = new float3(o.X + size.X, o.Y, o.Z);
				c = new float3(o.X + size.X, o.Y + size.Y, o.Z + size.Z);
				d = new float3(o.X, o.Y + size.Y, o.Z + size.Z);
			}

			FastCreateQuad(vertices, a, b, c, d, r, samplers, paletteTextureIndex, tint, alpha, nv);
		}

		public static void FastCreateQuad(Vertex[] vertices,
			in float3 a, in float3 b, in float3 c, in float3 d,
			Sprite r, int2 samplers, float paletteTextureIndex,
			in float3 tint, float alpha, int nv)
		{
			float sl = 0;
			float st = 0;
			float sr = 0;
			float sb = 0;

			// See combined.vert for documentation on the channel attribute format
			var attribC = r.Channel == TextureChannel.RGBA ? 0x02 : ((byte)r.Channel) << 1 | 0x01;
			attribC |= samplers.X << 6;
			if (r is SpriteWithSecondaryData ss)
			{
				sl = ss.SecondaryLeft;
				st = ss.SecondaryTop;
				sr = ss.SecondaryRight;
				sb = ss.SecondaryBottom;

				attribC |= ((byte)ss.SecondaryChannel) << 4 | 0x08;
				attribC |= samplers.Y << 9;
			}

			var fAttribC = (float)attribC;
			vertices[nv] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 1] = new Vertex(b, r.Right, r.Top, sr, st, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 2] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 3] = new Vertex(c, r.Right, r.Bottom, sr, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 4] = new Vertex(d, r.Left, r.Bottom, sl, sb, paletteTextureIndex, fAttribC, tint, alpha);
			vertices[nv + 5] = new Vertex(a, r.Left, r.Top, sl, st, paletteTextureIndex, fAttribC, tint, alpha);
		}

		public static void FastCopyIntoChannel(Sprite dest, byte[] src, SpriteFrameType srcType)
		{
			var destData = dest.Sheet.GetData();
			var width = dest.Bounds.Width;
			var height = dest.Bounds.Height;

			if (dest.Channel == TextureChannel.RGBA)
			{
				var destStride = dest.Sheet.Size.Width;
				unsafe
				{
					// Cast the data to an int array so we can copy the src data directly
					fixed (byte* bd = &destData[0])
					{
						var data = (int*)bd;
						var x = dest.Bounds.Left;
						var y = dest.Bounds.Top;

						var k = 0;
						for (var j = 0; j < height; j++)
						{
							for (var i = 0; i < width; i++)
							{
								byte r, g, b, a;
								switch (srcType)
								{
									case SpriteFrameType.Bgra32:
									case SpriteFrameType.Bgr24:
										{
											b = src[k++];
											g = src[k++];
											r = src[k++];
											a = srcType == SpriteFrameType.Bgra32 ? src[k++] : (byte)255;
											break;
										}

									case SpriteFrameType.Rgba32:
									case SpriteFrameType.Rgb24:
										{
											r = src[k++];
											g = src[k++];
											b = src[k++];
											a = srcType == SpriteFrameType.Rgba32 ? src[k++] : (byte)255;
											break;
										}

									default:
										throw new InvalidOperationException($"Unknown SpriteFrameType {srcType}");
								}

								var cc = Color.FromArgb(a, r, g, b);
								data[(y + j) * destStride + x + i] = PremultiplyAlpha(cc).ToArgb();
							}
						}
					}
				}
			}
			else
			{
				var destStride = dest.Sheet.Size.Width * 4;
				var destOffset = destStride * dest.Bounds.Top + dest.Bounds.Left * 4 + ChannelMasks[(int)dest.Channel];
				var destSkip = destStride - 4 * width;

				var srcOffset = 0;
				for (var j = 0; j < height; j++)
				{
					for (var i = 0; i < width; i++, srcOffset++)
					{
						destData[destOffset] = src[srcOffset];
						destOffset += 4;
					}

					destOffset += destSkip;
				}
			}
		}

		public static void FastCopyIntoSprite(Sprite dest, Png src)
		{
			var destData = dest.Sheet.GetData();
			var destStride = dest.Sheet.Size.Width;
			var width = dest.Bounds.Width;
			var height = dest.Bounds.Height;

			unsafe
			{
				// Cast the data to an int array so we can copy the src data directly
				fixed (byte* bd = &destData[0])
				{
					var data = (int*)bd;
					var x = dest.Bounds.Left;
					var y = dest.Bounds.Top;

					var k = 0;
					for (var j = 0; j < height; j++)
					{
						for (var i = 0; i < width; i++)
						{
							Color cc;
							switch (src.Type)
							{
								case SpriteFrameType.Indexed8:
									{
										cc = src.Palette[src.Data[k++]];
										break;
									}

								case SpriteFrameType.Rgba32:
								case SpriteFrameType.Rgb24:
									{
										var r = src.Data[k++];
										var g = src.Data[k++];
										var b = src.Data[k++];
										var a = src.Type == SpriteFrameType.Rgba32 ? src.Data[k++] : (byte)255;
										cc = Color.FromArgb(a, r, g, b);
										break;
									}

								// Pngs don't support BGR[A], so no need to include them here
								default:
									throw new InvalidOperationException($"Unknown SpriteFrameType {src.Type}");
							}

							data[(y + j) * destStride + x + i] = PremultiplyAlpha(cc).ToArgb();
						}
					}
				}
			}
		}

		/// <summary>Rotates a quad about its center in the x-y plane.</summary>
		/// <param name="tl">The top left vertex of the quad</param>
		/// <param name="size">A float3 containing the X, Y, and Z lengths of the quad</param>
		/// <param name="rotation">The number of radians to rotate by</param>
		/// <returns>An array of four vertices representing the rotated quad (top-left, top-right, bottom-right, bottom-left)</returns>
		public static float3[] RotateQuad(float3 tl, float3 size, float rotation)
		{
			var center = tl + 0.5f * size;
			var angleSin = (float)Math.Sin(-rotation);
			var angleCos = (float)Math.Cos(-rotation);

			// Rotated offset for +/- x with +/- y
			var ra = 0.5f * new float3(
				size.X * angleCos - size.Y * angleSin,
				size.X * angleSin + size.Y * angleCos,
				(size.X * angleSin + size.Y * angleCos) * size.Z / size.Y);

			// Rotated offset for +/- x with -/+ y
			var rb = 0.5f * new float3(
				size.X * angleCos + size.Y * angleSin,
				size.X * angleSin - size.Y * angleCos,
				(size.X * angleSin - size.Y * angleCos) * size.Z / size.Y);

			return new float3[]
			{
				center - ra,
				center + rb,
				center + ra,
				center - rb
			};
		}

		/// <summary>
		/// Returns the bounds of an object. Used for determining which objects need to be rendered on screen, and which do not.
		/// </summary>
		/// <param name="offset">The top left vertex of the object</param>
		/// <param name="size">A float 3 containing the X, Y, and Z lengths of the object</param>
		/// <param name="rotation">The angle to rotate the object by (use 0f if there is no rotation)</param>
		public static Rectangle BoundingRectangle(float3 offset, float3 size, float rotation)
		{
			if (rotation == 0f)
				return new Rectangle((int)offset.X, (int)offset.Y, (int)size.X, (int)size.Y);

			var rotatedQuad = Util.RotateQuad(offset, size, rotation);
			var minX = rotatedQuad[0].X;
			var maxX = rotatedQuad[0].X;
			var minY = rotatedQuad[0].Y;
			var maxY = rotatedQuad[0].Y;
			for (var i = 1; i < rotatedQuad.Length; i++)
			{
				minX = Math.Min(rotatedQuad[i].X, minX);
				maxX = Math.Max(rotatedQuad[i].X, maxX);
				minY = Math.Min(rotatedQuad[i].Y, minY);
				maxY = Math.Max(rotatedQuad[i].Y, maxY);
			}

			return new Rectangle(
				(int)minX,
				(int)minY,
				(int)Math.Ceiling(maxX) - (int)minX,
				(int)Math.Ceiling(maxY) - (int)minY);
		}

		public static Color PremultiplyAlpha(Color c)
		{
			if (c.A == byte.MaxValue)
				return c;
			var a = c.A / 255f;
			return Color.FromArgb(c.A, (byte)(c.R * a + 0.5f), (byte)(c.G * a + 0.5f), (byte)(c.B * a + 0.5f));
		}

		public static Color PremultipliedColorLerp(float t, Color c1, Color c2)
		{
			// Colors must be lerped in a non-multiplied color space
			var a1 = 255f / c1.A;
			var a2 = 255f / c2.A;
			return PremultiplyAlpha(Color.FromArgb(
				(int)(t * c2.A + (1 - t) * c1.A),
				(int)((byte)(t * a2 * c2.R + 0.5f) + (1 - t) * (byte)(a1 * c1.R + 0.5f)),
				(int)((byte)(t * a2 * c2.G + 0.5f) + (1 - t) * (byte)(a1 * c1.G + 0.5f)),
				(int)((byte)(t * a2 * c2.B + 0.5f) + (1 - t) * (byte)(a1 * c1.B + 0.5f))));
		}

		public static float[] IdentityMatrix()
		{
			return Exts.MakeArray(16, j => (j % 5 == 0) ? 1.0f : 0);
		}

		public static float[] ScaleMatrix(float sx, float sy, float sz)
		{
			var mtx = IdentityMatrix();
			mtx[0] = sx;
			mtx[5] = sy;
			mtx[10] = sz;
			return mtx;
		}

		public static float[] TranslationMatrix(float x, float y, float z)
		{
			var mtx = IdentityMatrix();
			mtx[12] = x;
			mtx[13] = y;
			mtx[14] = z;
			return mtx;
		}

		public static float[] MatrixMultiply(float[] lhs, float[] rhs)
		{
			var mtx = new float[16];
			for (var i = 0; i < 4; i++)
				for (var j = 0; j < 4; j++)
				{
					mtx[4 * i + j] = 0;
					for (var k = 0; k < 4; k++)
						mtx[4 * i + j] += lhs[4 * k + j] * rhs[4 * i + k];
				}

			return mtx;
		}

		public static float[] MatrixVectorMultiply(float[] mtx, float[] vec)
		{
			var ret = new float[4];
			for (var j = 0; j < 4; j++)
			{
				ret[j] = 0;
				for (var k = 0; k < 4; k++)
					ret[j] += mtx[4 * k + j] * vec[k];
			}

			return ret;
		}

		public static float[] MatrixInverse(float[] m)
		{
			var mtx = new float[16];

			mtx[0] = m[5] * m[10] * m[15] -
				m[5] * m[11] * m[14] -
				m[9] * m[6] * m[15] +
				m[9] * m[7] * m[14] +
				m[13] * m[6] * m[11] -
				m[13] * m[7] * m[10];

			mtx[4] = -m[4] * m[10] * m[15] +
				m[4] * m[11] * m[14] +
				m[8] * m[6] * m[15] -
				m[8] * m[7] * m[14] -
				m[12] * m[6] * m[11] +
				m[12] * m[7] * m[10];

			mtx[8] = m[4] * m[9] * m[15] -
				m[4] * m[11] * m[13] -
				m[8] * m[5] * m[15] +
				m[8] * m[7] * m[13] +
				m[12] * m[5] * m[11] -
				m[12] * m[7] * m[9];

			mtx[12] = -m[4] * m[9] * m[14] +
				m[4] * m[10] * m[13] +
				m[8] * m[5] * m[14] -
				m[8] * m[6] * m[13] -
				m[12] * m[5] * m[10] +
				m[12] * m[6] * m[9];

			mtx[1] = -m[1] * m[10] * m[15] +
				m[1] * m[11] * m[14] +
				m[9] * m[2] * m[15] -
				m[9] * m[3] * m[14] -
				m[13] * m[2] * m[11] +
				m[13] * m[3] * m[10];

			mtx[5] = m[0] * m[10] * m[15] -
				m[0] * m[11] * m[14] -
				m[8] * m[2] * m[15] +
				m[8] * m[3] * m[14] +
				m[12] * m[2] * m[11] -
				m[12] * m[3] * m[10];

			mtx[9] = -m[0] * m[9] * m[15] +
				m[0] * m[11] * m[13] +
				m[8] * m[1] * m[15] -
				m[8] * m[3] * m[13] -
				m[12] * m[1] * m[11] +
				m[12] * m[3] * m[9];

			mtx[13] = m[0] * m[9] * m[14] -
				m[0] * m[10] * m[13] -
				m[8] * m[1] * m[14] +
				m[8] * m[2] * m[13] +
				m[12] * m[1] * m[10] -
				m[12] * m[2] * m[9];

			mtx[2] = m[1] * m[6] * m[15] -
				m[1] * m[7] * m[14] -
				m[5] * m[2] * m[15] +
				m[5] * m[3] * m[14] +
				m[13] * m[2] * m[7] -
				m[13] * m[3] * m[6];

			mtx[6] = -m[0] * m[6] * m[15] +
				m[0] * m[7] * m[14] +
				m[4] * m[2] * m[15] -
				m[4] * m[3] * m[14] -
				m[12] * m[2] * m[7] +
				m[12] * m[3] * m[6];

			mtx[10] = m[0] * m[5] * m[15] -
				m[0] * m[7] * m[13] -
				m[4] * m[1] * m[15] +
				m[4] * m[3] * m[13] +
				m[12] * m[1] * m[7] -
				m[12] * m[3] * m[5];

			mtx[14] = -m[0] * m[5] * m[14] +
				m[0] * m[6] * m[13] +
				m[4] * m[1] * m[14] -
				m[4] * m[2] * m[13] -
				m[12] * m[1] * m[6] +
				m[12] * m[2] * m[5];

			mtx[3] = -m[1] * m[6] * m[11] +
				m[1] * m[7] * m[10] +
				m[5] * m[2] * m[11] -
				m[5] * m[3] * m[10] -
				m[9] * m[2] * m[7] +
				m[9] * m[3] * m[6];

			mtx[7] = m[0] * m[6] * m[11] -
				m[0] * m[7] * m[10] -
				m[4] * m[2] * m[11] +
				m[4] * m[3] * m[10] +
				m[8] * m[2] * m[7] -
				m[8] * m[3] * m[6];

			mtx[11] = -m[0] * m[5] * m[11] +
				m[0] * m[7] * m[9] +
				m[4] * m[1] * m[11] -
				m[4] * m[3] * m[9] -
				m[8] * m[1] * m[7] +
				m[8] * m[3] * m[5];

			mtx[15] = m[0] * m[5] * m[10] -
				m[0] * m[6] * m[9] -
				m[4] * m[1] * m[10] +
				m[4] * m[2] * m[9] +
				m[8] * m[1] * m[6] -
				m[8] * m[2] * m[5];

			var det = m[0] * mtx[0] + m[1] * mtx[4] + m[2] * mtx[8] + m[3] * mtx[12];
			if (det == 0)
				return null;

			for (var i = 0; i < 16; i++)
				mtx[i] *= 1 / det;

			return mtx;
		}

		public static float[] MakeFloatMatrix(Int32Matrix4x4 imtx)
		{
			var multipler = 1f / imtx.M44;
			return new[]
			{
				imtx.M11 * multipler,
				imtx.M12 * multipler,
				imtx.M13 * multipler,
				imtx.M14 * multipler,

				imtx.M21 * multipler,
				imtx.M22 * multipler,
				imtx.M23 * multipler,
				imtx.M24 * multipler,

				imtx.M31 * multipler,
				imtx.M32 * multipler,
				imtx.M33 * multipler,
				imtx.M34 * multipler,

				imtx.M41 * multipler,
				imtx.M42 * multipler,
				imtx.M43 * multipler,
				imtx.M44 * multipler,
			};
		}

		public static float[] MatrixAABBMultiply(float[] mtx, float[] bounds)
		{
			// Corner offsets
			var ix = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
			var iy = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
			var iz = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };

			// Vectors to opposing corner
			var ret = new[]
			{
				float.MaxValue, float.MaxValue, float.MaxValue,
				float.MinValue, float.MinValue, float.MinValue
			};

			// Transform vectors and find new bounding box
			for (var i = 0; i < 8; i++)
			{
				var vec = new[] { bounds[ix[i]], bounds[iy[i]], bounds[iz[i]], 1 };
				var tvec = MatrixVectorMultiply(mtx, vec);

				ret[0] = Math.Min(ret[0], tvec[0] / tvec[3]);
				ret[1] = Math.Min(ret[1], tvec[1] / tvec[3]);
				ret[2] = Math.Min(ret[2], tvec[2] / tvec[3]);
				ret[3] = Math.Max(ret[3], tvec[0] / tvec[3]);
				ret[4] = Math.Max(ret[4], tvec[1] / tvec[3]);
				ret[5] = Math.Max(ret[5], tvec[2] / tvec[3]);
			}

			return ret;
		}
	}
}
