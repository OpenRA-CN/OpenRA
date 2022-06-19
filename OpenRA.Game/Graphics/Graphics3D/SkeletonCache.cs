﻿using System;
using System.Collections.Generic;
using OpenRA.FileSystem;

namespace OpenRA.Graphics
{
	public class SkeletonCache
	{
		readonly Dictionary<string, SkeletonAsset> assets = new Dictionary<string, SkeletonAsset>();
		readonly Dictionary<string, OrderedSkeleton> orderedSkeletons = new Dictionary<string, OrderedSkeleton>();

		public OrderedSkeleton GetOrderedSkeleton(string unit)
		{
			if (!HasOrderedSkeleton(unit))
				throw new InvalidOperationException(
					$"Unit `{unit}` does not have a OrderedSkeleton");

			return orderedSkeletons[unit];
		}

		public bool HasOrderedSkeleton(in string name)
		{
			if (orderedSkeletons.ContainsKey(name))
				return true;
			else
				return false;
		}

		public bool HasSkeletonAsset(in string name)
		{
			if (assets.ContainsKey(name))
				return true;
			else
				return false;
		}

		public SkeletonAsset UpdateSkeletonAsset(in IReadOnlyFileSystem fileSystem, in string filename, in MiniYaml skeletonDefine, in string unit)
		{
			var fields = (filename).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			var assetname = fields[0].Trim();

			if (!HasSkeletonAsset(assetname))
			{
				var skeletonAsset = new SkeletonAsset(fileSystem, assetname);

				var info = skeletonDefine.ToDictionary();
				if (info.ContainsKey("Anims"))
				{
					var animsInfo = info["Anims"].ToDictionary();
					foreach (var animDefine in animsInfo)
					{
						skeletonAsset.TryAddAnimation(fileSystem, animDefine.Key, animDefine.Value.Value);
					}
				}
				else
				{
					Console.WriteLine("Unit " + unit + " has no animations");
				}

				assets.Add(assetname, skeletonAsset);
			}

			return assets[assetname];
		}

		public OrderedSkeleton UpdateOrderedSkeleton(in SkeletonAsset asset, in string unitName, in MiniYaml skeletonDefine)
		{
			var name = unitName;
			if (!HasOrderedSkeleton(name))
			{
				var skeletonAsset = new OrderedSkeleton(name, asset, skeletonDefine);
				orderedSkeletons.Add(name, skeletonAsset);
			}

			return orderedSkeletons[name];
		}

		public void UpdateAllSkeletonTexture()
		{
			foreach (var sk in orderedSkeletons)
			{
				sk.Value.UpdateAnimTextureData();
			}
		}
	}
}
