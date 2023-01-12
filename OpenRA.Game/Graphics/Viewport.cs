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
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Graphics
{
	[Flags]
	public enum ScrollDirection { None = 0, Up = 1, Left = 2, Down = 4, Right = 8 }

	public interface INotifyViewportZoomExtentsChanged
	{
		void ViewportZoomExtentsChanged(float minZoom, float maxZoom);
	}

	public static class ViewportExts
	{
		public static bool Includes(this ScrollDirection d, ScrollDirection s)
		{
			// PERF: Enum.HasFlag is slower and requires allocations.
			return (d & s) == s;
		}

		public static ScrollDirection Set(this ScrollDirection d, ScrollDirection s, bool val)
		{
			return (d.Includes(s) != val) ? d ^ s : d;
		}
	}

	public class Viewport
	{
		readonly WorldRenderer worldRenderer;
		readonly WorldViewportSizes viewportSizes;
		readonly GraphicSettings graphicSettings;

		// Map bounds (world-px)
		readonly Rectangle mapBounds;
		public readonly Size tileSize;

		public Target LerpTarget = Target.Invalid;

		// Viewport geometry (world-px)
		public int2 CenterLocation { get; private set; }

		public int2 TopLeft => CenterLocation - ViewportSize / 2;
		public int2 BottomRight => CenterLocation + ViewportSize / 2;

		public Vector3 ViewPoint { get; private set; }
		public WPos CenterPosition => worldRenderer.ProjectedPosition(CenterLocation);
		public WPos TopLeftPosition => worldRenderer.ProjectedPosition(TopLeft);
		public WPos BottomRightPosition => worldRenderer.ProjectedPosition(BottomRight);

		public Rectangle Rectangle => new Rectangle(TopLeft, new Size(ViewportSize.X, ViewportSize.Y));

		public int2 ViewportSize { get; private set; }
		ProjectedCellRegion cells;
		bool cellsDirty = true;

		ProjectedCellRegion allCells;
		bool allCellsDirty = true;

		WorldViewport lastViewportDistance;

		float zoom = 1f;
		float minZoom = 1f;
		float maxZoom = 2f;

		bool unlockMinZoom;
		float unlockedMinZoomScale;
		float unlockedMinZoom = 1f;

		public float Zoom
		{
			get => zoom;

			private set
			{
				zoom = value;
				ViewportSize = (1f / zoom * new float2(Game.Renderer.NativeResolution)).ToInt2();
				cellsDirty = true;
				allCellsDirty = true;
			}
		}

		public float MinZoom => minZoom;

		public void AdjustZoom(float dz)
		{
			// Exponential ensures that equal positive and negative steps have the same effect
			Zoom = (zoom * (float)Math.Exp(dz)).Clamp(unlockMinZoom ? unlockedMinZoom : minZoom, maxZoom);
		}

		public void AdjustZoom(float dz, int2 center)
		{
			var oldCenter = worldRenderer.Viewport.ViewToWorldPx(center);
			AdjustZoom(dz);
			var newCenter = worldRenderer.Viewport.ViewToWorldPx(center);
			CenterLocation += oldCenter - newCenter;
		}

		public void ToggleZoom()
		{
			// Unlocked zooms always reset to the default zoom
			if (zoom < minZoom)
				Zoom = minZoom;
			else
				Zoom = zoom > minZoom ? minZoom : maxZoom;
		}

		public void UnlockMinimumZoom(float scale)
		{
			unlockMinZoom = true;
			unlockedMinZoomScale = scale;
			UpdateViewportZooms(false);
		}

		public static long LastMoveRunTime = 0;
		public static int2 LastMousePos;

		float ClosestTo(float[] collection, float target)
		{
			var closestValue = collection.First();
			var subtractResult = Math.Abs(closestValue - target);

			foreach (var element in collection)
			{
				if (Math.Abs(element - target) < subtractResult)
				{
					subtractResult = Math.Abs(element - target);
					closestValue = element;
				}
			}

			return closestValue;
		}

		public ScrollDirection GetBlockedDirections()
		{
			var ret = ScrollDirection.None;
			if (CenterLocation.Y <= mapBounds.Top)
				ret |= ScrollDirection.Up;
			if (CenterLocation.X <= mapBounds.Left)
				ret |= ScrollDirection.Left;
			if (CenterLocation.Y >= mapBounds.Bottom)
				ret |= ScrollDirection.Down;
			if (CenterLocation.X >= mapBounds.Right)
				ret |= ScrollDirection.Right;

			return ret;
		}

		public Viewport(WorldRenderer wr, Map map)
		{
			worldRenderer = wr;
			var grid = Game.ModData.Manifest.Get<MapGrid>();
			viewportSizes = Game.ModData.Manifest.Get<WorldViewportSizes>();
			graphicSettings = Game.Settings.Graphics;

			// Calculate map bounds in world-px
			if (wr.World.Type == WorldType.Editor)
			{
				// The full map is visible in the editor
				var width = map.MapSize.X * grid.TileSize.Width;
				var height = map.MapSize.Y * grid.TileSize.Height;
				if (wr.World.Map.Grid.Type == MapGridType.RectangularIsometric)
					height /= 2;

				mapBounds = new Rectangle(0, 0, width, height);
				CenterLocation = new int2(width / 2, height / 2);
			}
			else
			{
				var tl = wr.ScreenPxPosition(map.ProjectedTopLeft);
				var br = wr.ScreenPxPosition(map.ProjectedBottomRight);
				mapBounds = Rectangle.FromLTRB(tl.X, tl.Y, br.X, br.Y);
				CenterLocation = (tl + br) / 2;
			}

			tileSize = grid.TileSize;

			UpdateViewportZooms();
		}

		long lastLerpTime;
		public void Tick()
		{
			if (lastViewportDistance != graphicSettings.ViewportDistance)
				UpdateViewportZooms();

			if (LerpTarget.Type != TargetType.Invalid)
			{
				var deltaScale = Math.Max(1, Math.Min(Game.RunTime - lastLerpTime, 25f));
				lastLerpTime = Game.RunTime;
				var fpos = new float3(LerpTarget.CenterPosition.X, LerpTarget.CenterPosition.Y - (float)LerpTarget.CenterPosition.Z * Game.Renderer.World3DRenderer.TanCameraPitch, 0);
				ViewPoint = Vector3.Lerp(ViewPoint,
					Game.Renderer.World3DRenderer.Get3DRenderPositionFromFloat3(fpos),
					0.5f / deltaScale * 25f);

				Center(World3DCoordinate.Vec3ToWPosForRender(ViewPoint));
			}
			else
			{
				ViewPoint = World3DCoordinate.WPosToVec3(CenterPosition);
			}
		}

		float CalculateMinimumZoom(float minHeight, float maxHeight)
		{
			var h = Game.Renderer.NativeResolution.Height;

			// Check the easy case: the native resolution is within the maximum limit
			// Also catches the case where the user may force a resolution smaller than the minimum window size
			if (h <= maxHeight)
				return 1;

			// Find a clean fraction that brings us within the desired range to reduce aliasing
			var step = 1f;
			while (true)
			{
				var testZoom = 1f;
				while (true)
				{
					var nextZoom = testZoom + step;
					if (h < minHeight * nextZoom)
						break;

					testZoom = nextZoom;
				}

				if (h < maxHeight * testZoom)
					return testZoom;

				step /= 2;
			}
		}

		void UpdateViewportZooms(bool resetCurrentZoom = true)
		{
			lastViewportDistance = graphicSettings.ViewportDistance;

			var vd = graphicSettings.ViewportDistance;
			if (viewportSizes.AllowNativeZoom && vd == WorldViewport.Native)
				minZoom = viewportSizes.DefaultScale;
			else
			{
				var range = viewportSizes.GetSizeRange(vd);
				minZoom = CalculateMinimumZoom(range.X, range.Y) * viewportSizes.DefaultScale;
			}

			maxZoom = Math.Min(minZoom * viewportSizes.MaxZoomScale, Game.Renderer.NativeResolution.Height * viewportSizes.DefaultScale / viewportSizes.MaxZoomWindowHeight);

			if (unlockMinZoom)
			{
				// Spectators and the map editor support zooming out by an extra factor of two.
				// TODO: Allow zooming out until the full map is visible
				// We need to improve our viewport scroll handling to center the map as we zoom out
				// before this will work well enough to enable
				unlockedMinZoom = minZoom * unlockedMinZoomScale;
			}

			if (resetCurrentZoom)
				Zoom = minZoom;
			else
				Zoom = Zoom.Clamp(minZoom, maxZoom);

			var maxSize = (1f / (unlockMinZoom ? unlockedMinZoom : minZoom) * new float2(Game.Renderer.NativeResolution));
			Game.Renderer.SetMaximumViewportSize(new Size((int)maxSize.X, (int)maxSize.Y));

			foreach (var t in worldRenderer.World.WorldActor.TraitsImplementing<INotifyViewportZoomExtentsChanged>())
				t.ViewportZoomExtentsChanged(minZoom, maxZoom);
		}

		public CPos ViewToWorld(int2 view)
		{
			var world = worldRenderer.Viewport.ViewToWorldPx(view);
			var map = worldRenderer.World.Map;
			var candidates = CandidateMouseoverCells(world).ToList();

			foreach (var uv in candidates)
			{
				// Coarse filter to nearby cells
				var p = map.CenterOfCell(uv.ToCPos(map.Grid.Type));
				var s = worldRenderer.ScreenPxPosition(p);
				if (Math.Abs(s.X - world.X) <= tileSize.Width && Math.Abs(s.Y - world.Y) <= tileSize.Height)
				{
					if (map.CellInfos.Contains(uv))
					{
						var cellinfo = map.CellInfos[uv];
						WPos[] corners = new WPos[4] { map.TerrainVertices[cellinfo.T].LogicPos,
						map.TerrainVertices[cellinfo.B].LogicPos,
						map.TerrainVertices[cellinfo.L].LogicPos,
						map.TerrainVertices[cellinfo.R].LogicPos };
						var screen = corners.Select(c => worldRenderer.ScreenPxPosition(c)).ToArray();
						if (screen.PolygonContains(world))
							return uv.ToCPos(map);
					}
					else
					{
						var ramp = map.Grid.Type == MapGridType.RectangularIsometric ? CellRamp.FlatRampIso : CellRamp.FlatRamp;
						var pos = map.CenterOfCell(uv);
						pos = new WPos(pos.X, pos.Y, map.MiniHeightOfCell(uv));
						var screen = ramp.Corners.Select(c => worldRenderer.ScreenPxPosition(pos + c)).ToArray();

						if (screen.PolygonContains(world))
							return uv.ToCPos(map);
					}
				}
			}

			// Mouse is not directly over a cell (perhaps on a cliff)
			// Try and find the closest cell
			if (candidates.Count > 0)
			{
				return candidates.MinBy(uv =>
				{
					var p = map.CenterOfCell(uv.ToCPos(map.Grid.Type));
					var s = worldRenderer.ScreenPxPosition(p);
					var dx = Math.Abs(s.X - world.X);
					var dy = Math.Abs(s.Y - world.Y);

					return dx * dx + dy * dy;
				}).ToCPos(map);
			}

			// Something is very wrong, but lets return something that isn't completely bogus and hope the caller can recover
			return worldRenderer.World.Map.CellContaining(worldRenderer.ProjectedPosition(ViewToWorldPx(view)));
		}

		/// <summary>
		/// Local use only
		/// </summary>
		public WPos ViewToWorldPos(int2 view, CPos cell)
		{
			var dirZero = worldRenderer.ProjectedPosition(ViewToWorldPx(view));
			var map = worldRenderer.World.Map;
			var w3dr = Game.Renderer.World3DRenderer;
			var pos = map.CenterOfCell(cell);
			var viewOffset = (int)(pos.Z * (w3dr.InverseCameraFront.Y / w3dr.InverseCameraFront.Z));
			return new WPos(dirZero.X, dirZero.Y + viewOffset, pos.Z);
		}

		/// <summary>
		/// Local use only
		/// </summary>
		public WPos ViewToWorldPos(int2 view)
		{
			var cell = ViewToWorld(view);
			return ViewToWorldPos(view, cell);
		}

		/// <summary> Returns an unfiltered list of all cells that could potentially contain the mouse cursor</summary>
		IEnumerable<MPos> CandidateMouseoverCells(int2 world)
		{
			var map = worldRenderer.World.Map;
			var minPos = worldRenderer.ProjectedPosition(world);

			// Find all the cells that could potentially have been clicked
			var a = map.CellContaining(minPos - new WVec(1024, 0, 0)).ToMPos(map.Grid.Type);
			var b = map.CellContaining(minPos + new WVec(512, 512 * map.Grid.MaximumTerrainHeight, 0)).ToMPos(map.Grid.Type);

			for (var v = b.V; v >= a.V; v--)
				for (var u = b.U; u >= a.U; u--)
					yield return new MPos(u, v);
		}

		public int2 ViewToWorldPx(int2 view) { return (graphicSettings.UIScale / Zoom * view.ToFloat2()).ToInt2() + TopLeft; }
		public int2 WorldToViewPx(int2 world) { return ((Zoom / graphicSettings.UIScale) * (world - TopLeft).ToFloat2()).ToInt2(); }
		public int2 WorldToViewPx(in float3 world) { return ((Zoom / graphicSettings.UIScale) * (world - TopLeft).XY).ToInt2(); }

		public void Center(IEnumerable<Actor> actors)
		{
			if (!actors.Any())
				return;

			Center(actors.Select(a => a.CenterPosition).Average());
		}

		public void Center(WPos pos)
		{
			CenterLocation = worldRenderer.ScreenPxPosition(pos).Clamp(mapBounds);
			cellsDirty = true;
			allCellsDirty = true;
		}

		public void Scroll(float2 delta, bool ignoreBorders)
		{
			// Convert scroll delta from world-px to viewport-px
			CenterLocation += (1f / Zoom * delta).ToInt2();
			cellsDirty = true;
			allCellsDirty = true;

			if (!ignoreBorders)
				CenterLocation = CenterLocation.Clamp(mapBounds);
		}

		// Rectangle (in viewport coords) that contains things to be drawn
		public Rectangle GetScissorBounds(bool insideBounds)
		{
			// Visible rectangle in world coordinates (expanded to the corners of the cells)
			var bounds = insideBounds ? VisibleCellsInsideBounds : AllVisibleCells;
			var map = worldRenderer.World.Map;
			var ctl = map.CenterOfCell(((MPos)bounds.TopLeft).ToCPos(map)) - new WVec(512, 512, 0);
			var cbr = map.CenterOfCell(((MPos)bounds.BottomRight).ToCPos(map)) + new WVec(512, 512, 0);

			// Convert to screen coordinates
			var tl = worldRenderer.ScreenPxPosition(ctl - new WVec(0, 0, ctl.Z)) - TopLeft;
			var br = worldRenderer.ScreenPxPosition(cbr - new WVec(0, 0, cbr.Z)) - TopLeft;

			// Add an extra half-cell fudge to avoid clipping isometric tiles
			return Rectangle.FromLTRB(tl.X - tileSize.Width / 2, tl.Y - tileSize.Height / 2,
				br.X + tileSize.Width / 2, br.Y + tileSize.Height / 2);
		}

		ProjectedCellRegion CalculateVisibleCells(bool insideBounds)
		{
			var map = worldRenderer.World.Map;

			// Calculate the projected cell position at the corners of the visible area
			var tl = (PPos)map.CellContaining(worldRenderer.ProjectedPosition(TopLeft)).ToMPos(map);
			var br = (PPos)map.CellContaining(worldRenderer.ProjectedPosition(BottomRight)).ToMPos(map);

			// RectangularIsometric maps don't have straight edges, and so we need an additional
			// cell margin to include the cells that are half visible on each edge.
			if (map.Grid.Type == MapGridType.RectangularIsometric)
			{
				tl = new PPos(tl.U - 1, tl.V - 1);
				br = new PPos(br.U + 1, br.V + 1);
			}

			// Clamp to the visible map bounds, if requested
			if (insideBounds)
			{
				tl = map.Clamp(tl);
				br = map.Clamp(br);
			}

			return new ProjectedCellRegion(map, tl, br);
		}

		public ProjectedCellRegion VisibleCellsInsideBounds
		{
			get
			{
				if (cellsDirty)
				{
					cells = CalculateVisibleCells(true);
					cellsDirty = false;
				}

				return cells;
			}
		}

		public ProjectedCellRegion AllVisibleCells
		{
			get
			{
				if (allCellsDirty)
				{
					allCells = CalculateVisibleCells(false);
					allCellsDirty = false;
				}

				return allCells;
			}
		}
	}
}
