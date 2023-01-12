#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor is selectable. Defines bounds of selectable area, selection class, selection priority and selection priority modifiers.")]
	public class IsometricSelectableInfo : TraitInfo, IMouseBoundsInfo, ISelectableInfo, IRulesetLoaded, Requires<BuildingInfo>
	{
		[Desc("Defines a custom rectangle for mouse interaction with the actor.",
			"If null, the engine will guess an appropriate size based on the building's footprint.",
			"The first two numbers define the width and depth of the footprint rectangle.",
			"The (optional) second two numbers define an x and y offset from the actor center.")]
		public readonly int[] Bounds = null;

		[Desc("Height above the footprint for the top of the interaction rectangle.")]
		public readonly int Height = 24;

		[Desc("Defines a custom rectangle for Decorations (e.g. the selection box).",
			"If null, Bounds will be used instead.")]
		public readonly int[] DecorationBounds = null;

		[Desc("Defines a custom height for Decorations (e.g. the selection box).",
			"If < 0, Height will be used instead.",
			"If Height is 0, this must be defined with a value greater than 0.")]
		public readonly int DecorationHeight = -1;

		public readonly int Priority = 10;

		[Desc("Allow selection priority to be modified using a hotkey.",
			"Valid values are None (priority is not affected by modifiers)",
			"Ctrl (priority is raised when Ctrl pressed) and",
			"Alt (priority is raised when Alt pressed).")]
		public readonly SelectionPriorityModifiers PriorityModifiers = SelectionPriorityModifiers.None;

		[Desc("All units having the same selection class specified will be selected with select-by-type commands (e.g. double-click). ",
			"Defaults to the actor name when not defined or inherited.")]
		public readonly string Class = null;

		[VoiceReference]
		public readonly string Voice = "Select";

		public override object Create(ActorInitializer init) { return new IsometricSelectable(init.Self, this); }

		int ISelectableInfo.Priority => Priority;
		SelectionPriorityModifiers ISelectableInfo.PriorityModifiers => PriorityModifiers;
		string ISelectableInfo.Voice => Voice;

		public virtual void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var grid = Game.ModData.Manifest.Get<MapGrid>();
			if (grid.Type != MapGridType.RectangularIsometric)
				throw new YamlException("IsometricSelectable can only be used in mods that use the RectangularIsometric MapGrid type.");

			if (Height == 0 && DecorationHeight <= 0)
				throw new YamlException("DecorationHeight must be defined and greater than 0 if Height is 0.");
		}
	}

	public class IsometricSelectable : IMouseBounds, ISelectable
	{
		readonly IsometricSelectableInfo info;
		readonly string selectionClass = null;
		readonly BuildingInfo buildingInfo;
		bool inited = false;
		WPos posleft, posright, postop, posbottom;

		public IsometricSelectable(Actor self, IsometricSelectableInfo info)
		{
			this.info = info;
			selectionClass = string.IsNullOrEmpty(info.Class) ? self.Info.Name : info.Class;
			buildingInfo = self.Info.TraitInfo<BuildingInfo>();
		}

		Polygon Bounds(Actor self, WorldRenderer wr, int[] bounds, int height, float scale = 1)
		{
			int2 left, right, top, bottom;
			if (bounds != null)
			{
				// Convert from WDist to pixels
				var offset = bounds.Length >= 4 ? new int2(bounds[2] * wr.TileSize.Width / wr.TileScale, bounds[3] * wr.TileSize.Height / wr.TileScale) : int2.Zero;
				var center = wr.ScreenPxPosition(self.CenterPosition) + offset;
				left = center - (new float2(bounds[0] * wr.TileSize.Width / (2 * wr.TileScale), 0) * scale).ToInt2();
				right = left + (new float2(bounds[0] * wr.TileSize.Width / wr.TileScale, 0) * scale).ToInt2();
				top = center - (new float2(0, bounds[1] * wr.TileSize.Height / (2 * wr.TileScale)) * scale).ToInt2();
				bottom = top + (new float2(0, bounds[1] * wr.TileSize.Height / wr.TileScale) * scale).ToInt2();
			}
			else
			{
				if (inited)
				{
					left = wr.ScreenPxPosition(FloatScale(posleft, scale) + new WVec(self.CenterPosition));
					right = wr.ScreenPxPosition(FloatScale(posright, scale) + new WVec(self.CenterPosition));
					top = wr.ScreenPxPosition(FloatScale(postop, scale) + new WVec(self.CenterPosition));
					bottom = wr.ScreenPxPosition(FloatScale(posbottom, scale) + new WVec(self.CenterPosition));
				}
				else
				{
					var xMin = int.MaxValue;
					var xMax = int.MinValue;
					var yMin = int.MaxValue;
					var yMax = int.MinValue;
					foreach (var c in buildingInfo.OccupiedTiles(self.Location))
					{
						xMin = Math.Min(xMin, c.X);
						xMax = Math.Max(xMax, c.X);
						yMin = Math.Min(yMin, c.Y);
						yMax = Math.Max(yMax, c.Y);
					}

					left = wr.ScreenPxPosition(self.World.Map.CenterOfCell(new CPos(xMin, yMax)) - new WVec(768, 0, 0));
					right = wr.ScreenPxPosition(self.World.Map.CenterOfCell(new CPos(xMax, yMin)) + new WVec(768, 0, 0));
					top = wr.ScreenPxPosition(self.World.Map.CenterOfCell(new CPos(xMin, yMin)) - new WVec(0, 768, 0));
					bottom = wr.ScreenPxPosition(self.World.Map.CenterOfCell(new CPos(xMax, yMax)) + new WVec(0, 768, 0));

					posleft = self.World.Map.CenterOfCell(new CPos(xMin, yMax)) - new WVec(768, 0, 0) - new WVec(self.CenterPosition);
					posright = self.World.Map.CenterOfCell(new CPos(xMax, yMin)) + new WVec(768, 0, 0) - new WVec(self.CenterPosition);
					postop = self.World.Map.CenterOfCell(new CPos(xMin, yMin)) - new WVec(0, 768, 0) - new WVec(self.CenterPosition);
					posbottom = self.World.Map.CenterOfCell(new CPos(xMax, yMax)) + new WVec(0, 768, 0) - new WVec(self.CenterPosition);

					inited = true;
				}
			}

			if (height == 0)
				return new Polygon(new[] { top, left, bottom, right });

			var h = new int2(0, (int)(height * scale));
			return new Polygon(new[] { top - h, left - h, left, bottom, right, right - h });
		}

		WPos FloatScale(WPos pos, float scale)
		{
			return new WPos((int)(pos.X * scale),(int)(pos.Y * scale), (int)(pos.Z * scale));
		}

		public Polygon Bounds(Actor self, WorldRenderer wr)
		{
			return Bounds(self, wr, info.Bounds, info.Height);
		}

		public Polygon DecorationBounds(Actor self, WorldRenderer wr, float scale = 1)
		{
			return Bounds(self, wr, info.DecorationBounds ?? info.Bounds, info.DecorationHeight >= 0 ? info.DecorationHeight : info.Height, scale);
		}

		Polygon IMouseBounds.MouseoverBounds(Actor self, WorldRenderer wr)
		{
			return Bounds(self, wr);
		}

		string ISelectable.Class => selectionClass;
	}
}
