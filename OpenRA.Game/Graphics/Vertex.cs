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

using System.Runtime.InteropServices;

namespace OpenRA.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Vertex
	{
		// 3d position
		public readonly float X, Y, Z;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float S, T, U, V;

		// Palette and channel flags
		public readonly float P, C;

		// Color tint
		public readonly float R, G, B, A;

		public Vertex(in float3 xyz, float s, float t, float u, float v, float p, float c)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, float3.Ones, 1f) { }

		public Vertex(in float3 xyz, float s, float t, float u, float v, float p, float c, in float3 tint, float a)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a) { }

		public Vertex(float x, float y, float z, float s, float t, float u, float v, float p, float c, in float3 tint, float a)
			: this(x, y, z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a) { }

		public Vertex(float x, float y, float z, float s, float t, float u, float v, float p, float c, float r, float g, float b, float a)
		{
			X = x; Y = y; Z = z;
			S = s; T = t;
			U = u; V = v;
			P = p; C = c;
			R = r; G = g; B = b; A = a;
		}
	}

	public readonly struct ScreenVertex
	{
		// 3d position
		public readonly float X, Y;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float U, V;

		public ScreenVertex(float x, float y, float u, float v)
		{
			X = x; Y = y;
			U = u; V = v;
		}
	}

	public struct MapVertex
	{
		// 3d position
		public readonly float X, Y, Z;

		// Primary and secondary texture coordinates or RGBA color
		public readonly float S, T, U, V;

		// Palette and channel flags
		public readonly float P, C;

		// Color tint
		public readonly float R, G, B, A;

		// 3d normal
		public readonly float NX, NY, NZ;

		// 3d normal
		public readonly float FNX, FNY, FNZ;

		public readonly float TU, TV;

		public readonly uint DrawType;

		public MapVertex(in float3 xyz, in float3 nml, in float3 fnml, float s, float t, float u, float v, float p, float c, in float3 tint, float a, float tu, float tv, uint type)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a, nml.X, nml.Y, nml.Z, fnml.X, fnml.Y, fnml.Z, tu, tv, type) { }

		public MapVertex(float x, float y, float z, float s, float t, float u, float v, float p, float c, in float3 tint, float a, float nx, float ny, float nz, float fnx, float fny, float fnz, float tu, float tv, uint type)
			: this(x, y, z, s, t, u, v, p, c, tint.X, tint.Y, tint.Z, a, nx, ny, nz, fnx, fny, fnz, tu, tv, type) { }

		public MapVertex(float x, float y, float z,
			float s, float t, float u, float v,
			float p, float c,
			float r, float g, float b, float a,
			float nx, float ny, float nz,
			float fnx, float fny, float fnz,
			float tu, float tv, uint type)
		{
			X = x; Y = y; Z = z;
			S = s; T = t;
			U = u; V = v;
			P = p; C = c;
			R = r; G = g; B = b; A = a;
			NX = nx; NY = ny; NZ = nz;
			FNX = fnx; FNY = fny; FNZ = fnz;
			TU = tu; TV = tv;
			DrawType = type;
		}
	};
}
