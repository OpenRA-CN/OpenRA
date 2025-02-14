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
using OpenRA.FileSystem;
using OpenRA.Graphics.Graphics3D;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public interface IModel : IDisposable
	{
		uint Frames { get; }
		uint Sections { get; }

		float[] TransformationMatrix(uint section, uint frame);
		float[] Size { get; }
		float[] Bounds(uint frame);

		IOrderedMesh RenderData(uint section);

		/// <summary>Returns the smallest rectangle that covers all rotations of all frames in a model</summary>
		Rectangle AggregateBounds { get; }
	}

	//public readonly struct ModelRenderData
	//{
	//	public readonly int Start;
	//	public readonly int Count;
	//	public readonly IShader Shader;
	//	public readonly IVertexBuffer VertexBuffer;
	//	public readonly Dictionary<string, ITexture> Textures;
	//	public ModelRenderData(int start, int count, IShader shader, IVertexBuffer vertexBuffer, Dictionary<string, ITexture> textures)
	//	{
	//		Start = start;
	//		Count = count;
	//		Shader = shader;
	//		VertexBuffer = vertexBuffer;
	//		Textures = textures;
	//	}
	//}

	public interface IModelLoader
	{
		bool TryLoadModel(IReadOnlyFileSystem fileSystem, string filename, out IModel model);
		bool TryLoadModel(IReadOnlyFileSystem fileSystem, string filename, MiniYaml yaml, out IModel model);
	}

	public interface IModelSequenceLoader
	{
		ModelCache CacheModels(IModelLoader[] loaders, IReadOnlyFileSystem fileSystem, ModData modData, IReadOnlyDictionary<string, MiniYamlNode> modelDefinitions);
	}
}
