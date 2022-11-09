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
using Newtonsoft.Json.Linq;
using OpenRA.Activities;
using OpenRA.GameRules;
using OpenRA.Mods.Cnc.Effects;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	public class DropPodsPowerInfo : SupportPowerInfo, IRulesetLoaded
	{
		[FieldLoader.Require]
		[Desc("Drop pod unit")]
		[ActorReference(new[] { typeof(AircraftInfo), typeof(FallsToEarthInfo) })]
		public readonly string[] UnitTypes = null;

		[Desc("Unit for transport passenger to starport")]
		public readonly string TransportorType = null;

		public readonly WVec TransportorLandOffset = WVec.Zero;

		public readonly WVec TransportorInitOffset = WVec.Zero;

		public readonly WAngle TransportorInitFacing = WAngle.Zero;

		public readonly int TransportorLoadDelay = 20;

		public readonly int TransportorPrepareDelay = 20;

		[Desc("Number of drop pods spawned.")]
		public readonly int2 Drops = new int2(5, 8);

		[Desc("Sets the approach direction.")]
		public readonly WAngle PodFacing = new WAngle(128);

		[Desc("Maximum offset from targetLocation")]
		public readonly int PodScatter = 3;

		[Desc("Effect sequence sprite image")]
		public readonly string EntryEffect = "podring";

		[Desc("Effect sequence to display in the air.")]
		[SequenceReference(nameof(EntryEffect))]
		public readonly string EntryEffectSequence = "idle";

		[PaletteReference]
		public readonly string EntryEffectPalette = "effect";

		[ActorReference]
		[Desc("Actor to spawn when the attack starts")]
		public readonly string CameraActor = null;

		[Desc("Number of ticks to keep the camera alive")]
		public readonly int CameraRemoveDelay = 25;

		[Desc("Which weapon to fire")]
		[WeaponReference]
		public readonly string Weapon = "Vulcan2";

		public WeaponInfo WeaponInfo { get; private set; }

		[Desc("Apply the weapon impact this many ticks into the effect")]
		public readonly int WeaponDelay = 0;

		public override object Create(ActorInitializer init) { return new DropPodsPower(init.Self, this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = (Weapon ?? string.Empty).ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			WeaponInfo = weapon;

			base.RulesetLoaded(rules, ai);
		}
	}

	public class DropPodsPower : SupportPower
	{
		readonly DropPodsPowerInfo info;
		Cargo cargo;

		public DropPodsPower(Actor self, DropPodsPowerInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			cargo = self.TraitOrDefault<Cargo>();
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);

			SendDropPods(self, order, info.PodFacing);
		}

		public void SendDropPods(Actor self, Order order, WAngle facing)
		{
			var actorInfo = self.World.Map.Rules.Actors[info.UnitTypes.First().ToLowerInvariant()];
			var aircraftInfo = actorInfo.TraitInfo<AircraftInfo>();
			var altitude = aircraftInfo.CruiseAltitude.Length;
			var approachRotation = WRot.FromYaw(facing);
			var fallsToEarthInfo = actorInfo.TraitInfo<FallsToEarthInfo>();
			var delta = new WVec(0, -altitude * aircraftInfo.Speed / fallsToEarthInfo.Velocity.Length, 0).Rotate(approachRotation);

			var drops = self.World.SharedRandom.Next(info.Drops.X, info.Drops.Y);
			List<Actor> pods = new List<Actor>();

			// cargo, passenger
			Dictionary<Actor, Actor> passengers = new Dictionary<Actor, Actor>();
			HashSet<Actor> reservePassengers = new HashSet<Actor>();

			if (info.TransportorType != null)
			{
				self.World.AddFrameEndTask(w =>
				{
					var target = order.Target.CenterPosition;
					var targetCell = self.World.Map.CellContaining(target);
					var podLocations = self.World.Map.FindTilesInCircle(targetCell, info.PodScatter)
						.Where(c => aircraftInfo.LandableTerrainTypes.Contains(w.Map.GetTerrainInfo(c).Type)
							&& !self.World.ActorMap.GetActorsAt(c).Any());

					if (!podLocations.Any())
						return;

					PlayLaunchSounds();

					// prepare pods and choose passengers
					for (var i = 0; i < drops; i++)
					{
						var unitType = info.UnitTypes.Random(self.World.SharedRandom);
						var podLocation = podLocations.Random(self.World.SharedRandom);
						var podTarget = Target.FromCell(w, podLocation);
						var location = self.World.Map.CenterOfCell(podLocation) - delta + new WVec(0, 0, altitude);

						var pod = w.CreateActor(false, unitType, new TypeDictionary
						{
							new CenterPositionInit(location),
							new OwnerInit(self.Owner),
							new FacingInit(facing)
						});

						var aircraft = pod.Trait<Aircraft>();
						if (!aircraft.CanLand(podLocation))
							pod.Dispose();
						else
						{
							pods.Add(pod);

							var podcargo = pod.TraitOrDefault<Cargo>();
							if (cargo != null && podcargo != null && cargo.Passengers.Any())
							{
								var passenger = cargo.Passengers.FirstOrDefault(p => podcargo.CanLoad(p) && !reservePassengers.Contains(p));
								if (passenger != null && !passenger.IsDead)
								{
									passengers.Add(pod, passenger);
									reservePassengers.Add(passenger);
								}
							}
						}
					}

					// call for transport
					if (passengers.Count > 0)
					{
						var landpos = self.CenterPosition + info.TransportorLandOffset;

						var trans = w.CreateActor(false, info.TransportorType, new TypeDictionary
						{
							new CenterPositionInit(landpos + info.TransportorInitOffset),
							new OwnerInit(self.Owner),
							new FacingInit(info.TransportorInitFacing)
						});

						w.Add(trans);
						trans.QueueActivity(new Land(trans, Target.FromActor(self), offset: info.TransportorLandOffset, addLandInfluence: false));
						trans.QueueActivity(new Wait(info.TransportorLoadDelay, false));
						trans.QueueActivity(new LoadDoppodPassengers(self, passengers.Values.ToArray()));
						trans.QueueActivity(new TakeOff(trans));
						trans.QueueActivity(new PrepareDroppods(passengers, pods.ToArray(),
							info.CameraActor, info.CameraRemoveDelay, targetCell, info.TransportorPrepareDelay));
						trans.QueueActivity(new RemoveSelf());
					}
					else
					{
						foreach (var p in passengers.Values)
						{
							cargo.Unload(self, p);
						}

						if (info.CameraActor != null)
						{
							var camera = w.CreateActor(info.CameraActor, new TypeDictionary
						{
							new LocationInit(targetCell),
							new OwnerInit(self.Owner),
						});

							camera.QueueActivity(new Wait(info.CameraRemoveDelay));
							camera.QueueActivity(new RemoveSelf());
						}

						foreach (var pod in pods)
						{
							w.Add(pod);

							var podcargo = pod.TraitOrDefault<Cargo>();
							if (podcargo != null && passengers.TryGetValue(pod, out var passenger))
							{
								if (passenger != null && !passenger.IsDead)
								{
									podcargo.Load(pod, passenger);
								}
							}
						}
					}

				});
			}
		}
	}

	public class LoadDoppodPassengers : Activity
	{
		Actor from;
		Actor[] passengers;

		public LoadDoppodPassengers(Actor from, Actor[] passengers)
		{
			this.from = from;
			this.passengers = passengers;
			IsInterruptible = false;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling) return true;
			var cargo = from.Trait<Cargo>();

			foreach (var p in passengers)
			{
				if (cargo.Passengers.Contains(p))
				{
					cargo.Unload(from, p);
				}

			}

			return true;
		}
	}

	public class PrepareDroppods : Activity
	{
		Dictionary<Actor, Actor> passengers;
		Actor[] pods;
		string cameraActor;
		int cameraDelay;
		CPos cameraCell;
		int dropDelay;

		public PrepareDroppods(Dictionary<Actor, Actor> passengers, Actor[] pods,
			string cameraActor, int cameraDelay, CPos cameraCell, int dropDelay = 0)
		{
			this.passengers = passengers;
			this.pods = pods;
			this.cameraActor = cameraActor;
			this.cameraDelay = cameraDelay;
			this.cameraCell = cameraCell;
			IsInterruptible = false;
			this.dropDelay = dropDelay;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling) return true;
			if (--dropDelay > 0)
				return false;

			var world = self.World;

			world.AddFrameEndTask(w =>
			{
				if (cameraActor != null)
				{
					var camera = w.CreateActor(cameraActor, new TypeDictionary
						{
							new LocationInit(cameraCell),
							new OwnerInit(self.Owner),
						});

					camera.QueueActivity(new Wait(cameraDelay));
					camera.QueueActivity(new RemoveSelf());
				}

				foreach (var pod in pods)
				{
					w.Add(pod);

					var podcargo = pod.TraitOrDefault<Cargo>();
					if (podcargo != null && passengers.TryGetValue(pod, out var passenger))
					{
						if (passenger != null && !passenger.IsDead && !passenger.IsInWorld)
						{
							podcargo.Load(pod, passenger);
						}
					}
				}

			});

			return true;
		}
	}
}
