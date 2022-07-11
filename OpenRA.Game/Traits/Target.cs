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
using System.Linq;

namespace OpenRA.Traits
{
	public enum TargetType : byte { Invalid, Actor, Terrain, FrozenActor }
	public struct Target
	{
		public static readonly Target[] None = Array.Empty<Target>();
		public static readonly Target Invalid = default(Target);

		readonly TargetType type;
		readonly Actor actor;
		readonly FrozenActor frozen;
		readonly WPos terrainCenterPosition;
		readonly WPos[] terrainPositions;
		readonly CPos? cell;
		readonly SubCell? subCell;
		readonly int generation;
		WVec offset;
		public void SetOffset(WVec offset)
		{
			this.offset = offset;
		}

		Target(WPos terrainCenterPosition, WVec offset, WPos[] terrainPositions = null)
		{
			type = TargetType.Terrain;
			this.terrainCenterPosition = terrainCenterPosition;
			this.terrainPositions = terrainPositions ?? new[] { terrainCenterPosition };

			actor = null;
			frozen = null;
			cell = null;
			subCell = null;
			generation = 0;
			this.offset = offset;
		}

		Target(World w, CPos c, SubCell subCell)
		{
			type = TargetType.Terrain;
			terrainCenterPosition = w.Map.CenterOfSubCell(c, subCell);
			terrainPositions = new[] { terrainCenterPosition };
			cell = c;
			this.subCell = subCell;

			actor = null;
			frozen = null;
			generation = 0;
			offset = WVec.Zero;
		}

		Target(Actor a, int generation)
		{
			type = TargetType.Actor;
			actor = a;
			this.generation = generation;

			terrainCenterPosition = WPos.Zero;
			terrainPositions = null;
			frozen = null;
			cell = null;
			subCell = null;
			offset = WVec.Zero;
		}

		Target(FrozenActor fa)
		{
			type = TargetType.FrozenActor;
			frozen = fa;

			terrainCenterPosition = WPos.Zero;
			terrainPositions = null;
			actor = null;
			cell = null;
			subCell = null;
			generation = 0;
			offset = WVec.Zero;
		}

		public static Target FromPos(WPos p) { return new Target(p, WVec.Zero); }
		public static Target FromTargetPositions(in Target t) { return new Target(t.CenterPosition, t.offset, t.Positions.ToArray()); }
		public static Target FromCell(World w, CPos c, SubCell subCell = SubCell.FullCell) { return new Target(w, c, subCell); }
		public static Target FromActor(Actor a) { return a != null ? new Target(a, a.Generation) : Invalid; }
		public static Target FromFrozenActor(FrozenActor fa) { return new Target(fa); }

		public Actor Actor => actor;
		public FrozenActor FrozenActor => frozen;

		public TargetType Type
		{
			get
			{
				if (type == TargetType.Actor)
				{
					// Actor is no longer in the world
					if (!actor.IsInWorld || actor.IsDead)
						return TargetType.Invalid;

					// Actor generation has changed (teleported or captured)
					if (actor.Generation != generation)
						return TargetType.Invalid;
				}

				return type;
			}
		}

		public bool IsValidFor(Actor targeter)
		{
			if (targeter == null)
				return false;

			switch (Type)
			{
				case TargetType.Actor:
					return actor.IsTargetableBy(targeter);
				case TargetType.FrozenActor:
					return frozen.IsValid && frozen.Visible && !frozen.Hidden;
				case TargetType.Invalid:
					return false;
				default:
				case TargetType.Terrain:
					return true;
			}
		}

		// Currently all or nothing.
		// TODO: either replace based on target type or put in singleton trait
		public bool RequiresForceFire
		{
			get
			{
				if (actor == null)
					return false;

				// PERF: Avoid LINQ.
				var isTargetable = false;
				foreach (var targetable in actor.Targetables)
				{
					if (!targetable.IsTraitEnabled())
						continue;

					isTargetable = true;
					if (!targetable.RequiresForceFire)
						return false;
				}

				return isTargetable;
			}
		}

		// Representative position - see Positions for the full set of targetable positions.
		public WPos CenterPosition
		{
			get
			{
				switch (Type)
				{
					case TargetType.Actor:
						return offset == WVec.Zero ? actor.CenterPosition : actor.CenterPosition + offset;
					case TargetType.FrozenActor:
						return frozen.CenterPosition;
					case TargetType.Terrain:
						return terrainCenterPosition;
					default:
					case TargetType.Invalid:
						throw new InvalidOperationException("Attempting to query the position of an invalid Target");
				}
			}
		}

		// Positions available to target for range checks
		static readonly WPos[] NoPositions = Array.Empty<WPos>();
		public IEnumerable<WPos> Positions
		{
			get
			{
				switch (Type)
				{
					case TargetType.Actor:
						return offset == WVec.Zero ? actor.GetTargetablePositions() : actor.GetTargetablePositions(offset);
					case TargetType.FrozenActor:
						// TargetablePositions may be null if it is Invalid
						return frozen.TargetablePositions ?? NoPositions;
					case TargetType.Terrain:
						return terrainPositions;
					default:
					case TargetType.Invalid:
						return NoPositions;
				}
			}
		}

		public bool IsInRange(WPos origin, WDist range)
		{
			if (Type == TargetType.Invalid)
				return false;

			// Target ranges are calculated in 2D, so ignore height differences
			return Positions.Any(t => (t - origin).HorizontalLengthSquared <= range.LengthSquared);
		}

		public override string ToString()
		{
			switch (Type)
			{
				case TargetType.Actor:
					return actor.ToString();

				case TargetType.FrozenActor:
					return frozen.ToString();

				case TargetType.Terrain:
					return terrainCenterPosition.ToString();

				default:
				case TargetType.Invalid:
					return "Invalid";
			}
		}

		// Expose internal state for serialization by the orders code *only*
		internal static Target FromSerializedActor(Actor a, int generation) { return a != null ? new Target(a, generation) : Invalid; }
		internal TargetType SerializableType => type;
		internal Actor SerializableActor => actor;
		internal int SerializableGeneration => generation;
		internal CPos? SerializableCell => cell;
		internal SubCell? SerializableSubCell => subCell;
		internal WPos SerializablePos => terrainCenterPosition;
	}
}
