﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;
using OpenRA.FileSystem;

namespace OpenRA.Graphics
{
	/// <summary>
	/// 预先bake的骨骼动画使用的是常规boneid
	/// </summary>
	public class PreBakedSkeletalAnim
	{
		public readonly FrameBaked[] Frames;

		public PreBakedSkeletalAnim(int size)
		{
			Frames = new FrameBaked[size];
		}
	}

	public class FrameBaked
	{
		public mat4[] Trans;

		public FrameBaked(int size)
		{
			Trans = new mat4[size];
		}
	}

	public class SkeletalAnim
	{
		public readonly string Name;
		public readonly string Sequence;

		public readonly Frame[] Frames;

		public SkeletalAnim(IReadOnlyFileSystem fileSystem, string sequence, string filename, SkeletonAsset assetBind)
		{
			Sequence = sequence;

			var name = filename;

			if (!fileSystem.Exists(name))
				name += ".anim";

			if (!fileSystem.Exists(name))
			{
				throw new Exception("SkeletalAnim:FromFile: can't find file " + name);
			}

			SkeletalAnimReader reader;
			using (var s = fileSystem.Open(name))
			{
				reader = new SkeletalAnimReader(s, assetBind);
			}

			Frames = new Frame[reader.Frames.Length];
			reader.Frames.CopyTo(Frames, 0);
			Name = reader.animName;
		}
	}

	public class Frame
	{
		public Transformation[] Trans;
		public int Length => Trans.Length;
		public Frame(int size)
		{
			Trans = new Transformation[size];
		}
	}

	class SkeletalAnimReader
	{
		Dictionary<int, string> boneIdtoNames = new Dictionary<int, string>();
		public Frame[] Frames;
		public string animName;

		public SkeletalAnimReader(Stream s, SkeletonAsset skeleton)
		{
			string header = s.ReadASCII(8);

			if (header != "ORA_ANIM")
				throw new Exception("SkeletalAnimReader: read file which has error header");

			animName = s.ReadUntil('?');

			uint dictSize = s.ReadUInt32();
			for (int i = 0; i < dictSize; i++)
			{
				int id = s.ReadInt32();
				string name = s.ReadUntil('?');
				boneIdtoNames.Add(id, name);
			}

			uint length = s.ReadUInt32();
			Frames = new Frame[length];

			for (int i = 0; i < length; i++)
			{
				uint bones = s.ReadUInt32();
				Frames[i] = new Frame((int)bones);
				for (int j = 0; j < bones; j++)
				{

					if (skeleton != null && skeleton.BoneNameAnimIndex.ContainsKey(boneIdtoNames[j]))
					{
						vec3 scale = ReadVec3(s);
						quat rotation = ReadQuat(s);
						rotation = rotation.Normalized;
						vec3 translation = ReadVec3(s);
						Frames[i].Trans[skeleton.BoneNameAnimIndex[boneIdtoNames[j]]] = new Transformation(scale, rotation, translation);
					}
					else
					{
						Console.WriteLine("No Match Bone: " + boneIdtoNames[j] + " in skeleton: " + skeleton.Name);
					}
				}
			}
		}

		vec3 ReadVec3(Stream s)
		{
			float x, y, z;
			x = s.ReadFloat(); y = s.ReadFloat(); z = s.ReadFloat();
			return new vec3(x, y, z);
		}

		quat ReadQuat(Stream s)
		{
			float x, y, z, w;
			x = s.ReadFloat(); y = s.ReadFloat(); z = s.ReadFloat(); w = s.ReadFloat();
			return new quat(x, y, z, w);
		}
	}
}
