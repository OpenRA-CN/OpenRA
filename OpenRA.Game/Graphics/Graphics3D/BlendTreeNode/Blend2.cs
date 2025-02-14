﻿using System;

namespace OpenRA.Graphics.Graphics3D
{
	/// <summary>
	/// 基础的混合节点。
	/// 混合两个输入节点的Mat4[]数据，输出一个混合后的Mat4[]数据
	/// 它会试图在更新输出时对输入节点的UpdateFrame进行调用
	/// </summary>
	class Blend2 : BlendNode
	{
		public BlendTreeNode InPutNodeA { get { return inPutNode1; } }
		public BlendTreeNode InPutNodeB { get { return inPutNode2; } }

		public float BlendValue = 0.0f;
		BlendTreeNode inPutNode1;
		BlendTreeNode inPutNode2;

		public Blend2(string name, uint id, BlendTree blendTree, AnimMask animMask, BlendTreeNode inPutNode1, BlendTreeNode inPutNode2)
			: base(name, id, blendTree, animMask)
		{
			this.inPutNode1 = inPutNode1;
			this.inPutNode2 = inPutNode2;
		}

		public override BlendTreeNodeOutPut UpdateOutPut(short optick, bool run, int step)
		{
			if (optick == tick)
				return outPut;
			tick = optick;

			var inPutValue1 = inPutNode1.UpdateOutPut(optick, run, step);
			var inPutValue2 = inPutNode2.UpdateOutPut(optick, run, step);

			outPut = BlendTreeUtil.Blend(inPutValue1, inPutValue2, BlendValue, animMask);

			return outPut;
		}
	}
}
