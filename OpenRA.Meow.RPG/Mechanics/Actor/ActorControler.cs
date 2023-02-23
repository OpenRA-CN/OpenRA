using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenRA.Graphics;
using OpenRA.Meow.RPG.Mechanics;
using OpenRA.Meow.RPG.Mechanics.Display;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Meow.RPG
{
	public class ActorControlerInfo : PausableConditionalTraitInfo
	{
		[CursorReference]
		[Desc("Cursor to display when targeting a teleport location.")]
		public readonly string TargetCursor = "attack";

		[GrantedConditionReference]
		[Desc("Condition to grant when under control.")]
		public readonly string Condition = "under-control";

		// lock on animation

		[Desc("Image used for this decoration. Defaults to the actor's type.")]
		public readonly string LockOnAnimImage = null;

		[FieldLoader.Require]
		[SequenceReference(nameof(LockOnAnimImage), allowNullImage: true)]
		[Desc("Sequence used for this decoration (can be animated).")]
		public readonly string LockOnAnimSequence = null;

		[PaletteReference(nameof(LockOnAnimIsPlayerPalette))]
		[Desc("Palette to render the sprite in. Reference the world actor's PaletteFrom* traits.")]
		public readonly string LockOnAnimPalette = "chrome";

		[Desc("Custom palette is a player palette BaseName")]
		public readonly bool LockOnAnimIsPlayerPalette = false;

		public override object Create(ActorInitializer init) { return new ActorControler(init.Self, this); }

	}

	public class ActorControler : PausableConditionalTrait<ActorControlerInfo>,
		INotifyCreated, IResolveOrder, ITick, IRenderAboveShroud
	{
		readonly ActorControlerInfo info;
		readonly Actor self;

		AttackBase[] attacks;
		IFacing facing;
		Turreted[] turreteds;
		IMover mover;

		readonly Animation lockAnim;
		readonly string lockAnimimage;

		// state
		Target attackTarget = Target.Invalid;
		Actor lockedOnActor = null;
		WVec moverDir = WVec.Zero;
		int controlingConditionToken = Actor.InvalidConditionToken;
		public bool UnderControl;

		public string TargetCursor => info.TargetCursor;

		bool IRenderAboveShroud.SpatiallyPartitionable => false;

		ControlerType controlerType;

		public enum ControlerType
		{
			None,
			Mobile,
			Airborne,
		}

		public ActorControler(Actor self , ActorControlerInfo info)
			: base(info)
		{
			this.self = self;
			this.info = info;

			lockAnimimage = info.LockOnAnimImage ?? self.Info.Name;
			lockAnim = new Animation(self.World, lockAnimimage, () => self.World.Paused);
			lockAnim.PlayRepeating(info.LockOnAnimSequence);
		}

		protected override void TraitEnabled(Actor self)
		{
			if (controlingConditionToken == Actor.InvalidConditionToken && UnderControl)
				controlingConditionToken = self.GrantCondition(Info.Condition);
		}

		protected override void TraitDisabled(Actor self)
		{
			if (controlingConditionToken != Actor.InvalidConditionToken)
				controlingConditionToken = self.RevokeCondition(controlingConditionToken);

			ClearTarget();
			moverDir = WVec.Zero;
		}

		public void Tick(Actor self)
		{
			if ((lockedOnActor != null && (lockedOnActor.IsDead || !lockedOnActor.IsInWorld)) ||
				!UnderControl)
			{
				lockedOnActor = null;
			}

			if (UnderControl)
			{
				if (controlingConditionToken == Actor.InvalidConditionToken)
					controlingConditionToken = self.GrantCondition(Info.Condition);
			}
			else
			{
				if (controlingConditionToken != Actor.InvalidConditionToken)
					controlingConditionToken = self.RevokeCondition(controlingConditionToken);

				moverDir = WVec.Zero;
			}

			bool moving = false;

			if (mover != null)
			{
				mover.UnderControl = UnderControl;
				if (mover.CanMove)
				{
					mover.MoveToward(moverDir);
					moving = true;
				}
			}

			if (attacks != null)
			{
				foreach (var a in attacks)
				{
					a.CanResolveAttackOrder = !UnderControl;
				}
			}

			if (!CanAttack() || attackTarget.Type == TargetType.Invalid)
			{
				if (attacks != null)
				{
					foreach (var a in attacks)
					{
						a.IsAiming = false;

						foreach (var arm in a.Armaments)
						{
							arm.IgnoreWeaponTargetCheck = false;
						}
					}
				}

				return;
			}

			if (lockedOnActor != null)
			{
				attackTarget = Target.FromActor(lockedOnActor);
			}

			bool turnFacing = false;
			WAngle attackFace = WAngle.Zero;
			int range = 0;
			var hoffset = 0;
			// determine if we should turn turrets or faceing
			if (attacks != null)
			{
				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					a.IsAiming = true;
					var thisZoffset = 0;
					foreach (var arm in a.Armaments)
					{
						arm.IgnoreWeaponTargetCheck = true;
						thisZoffset += arm.AdditionalLocalOffset().Z + arm.Barrels[0].Offset.Z;
					}

					hoffset += thisZoffset / a.Armaments.Count();

					if (!(a is AttackFollow) || a is AttackAircraft)
					{
						attackFace = a.Info.FiringAngle;
						turnFacing = true;
					}

					range = Math.Max(range, a.GetMiniArmMaximumRange(attackTarget).Length);
				}

				hoffset /= attacks.Length;

				// re-calculate target
				var firecenter = (self.CenterPosition + new WVec(0, 0, hoffset));
				var dir = attackTarget.CenterPosition - firecenter;
				var dist = dir.Length;
				var horizonDist = dir.HorizontalLength;
				if (range < horizonDist && range > 1)
				{
					var tPos = firecenter + ((range - 1) * dir / dist);

					// tPos = new WPos(tPos, self.World.Map.HeightOfTerrain(tPos));
					attackTarget = Target.FromPos(tPos);
				}

				if (facing != null && turnFacing && moving)
				{
					if (mover is Aircraft)
					{
						(mover as Aircraft).UnderControlDesiredFacing = (attackTarget.CenterPosition - self.CenterPosition).Yaw;
					}
					else
					{
						var desiredFacing = (attackTarget.CenterPosition - self.CenterPosition).Yaw;
						if (desiredFacing + attackFace != facing.Facing)
							facing.Facing = Mods.Common.Util.TickFacing(facing.Facing, desiredFacing + attackFace, facing.TurnSpeed);
					}

				}

				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					if (a is AttackFollow && !(a is AttackAircraft) && UnderControl)
					{
						(a as AttackFollow).SetRequestedTarget(attackTarget, true);
					}
					else
					{
						a.DoAttack(self, attackTarget);
					}
				}
			}
		}

		public bool CanAttack()
		{
			return UnderControl && !IsTraitDisabled && attacks != null && attacks.Length > 0;
		}

		void ClearTarget()
		{
			attackTarget = Target.Invalid;
			if (attacks != null)
			{
				foreach (var a in attacks)
				{
					if (a.IsTraitDisabled)
						continue;

					a.IsAiming = false;
					if (a is AttackFollow && (a as AttackFollow).RequestedTarget.Type != TargetType.Invalid)
					{
						(a as AttackFollow).ClearRequestedTarget(false);
					}
				}
			}
		}

		protected override void Created(Actor self)
		{
			attacks = self.TraitsImplementing<AttackBase>().ToArray();
			facing = self.TraitOrDefault<IFacing>();
			turreteds = self.TraitsImplementing<Turreted>().ToArray();
			mover = self.TraitOrDefault<IMover>();
			if (mover != null && (mover is Mobile))
				controlerType = ControlerType.Mobile;

			base.Created(self);
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Controler:Enable")
			{
				UnderControl = true;
				self.CancelActivity();
			}
			else if (order.OrderString == "Controler:Disable")
			{
				if (UnderControl)
					ClearTarget();

				UnderControl = false;
			}

			if (order.OrderString == "Controler:Mi1Down" && order.Target.Type != TargetType.Invalid && CanAttack())
			{
				if (UnderControl)
					attackTarget = order.Target;
			}

			if (order.OrderString == "Contorler:Mi1Up")
			{
				if (UnderControl)
					ClearTarget();
			}

			if (order.OrderString == "Contorler:Mi2Up")
			{
				if (UnderControl)
				{
					lockedOnActor = order.Target.Actor;
				}
			}

			if (order.OrderString == "Mover:Move" && UnderControl)
			{
				self.CancelActivity();
				moverDir = new WVec(order.Target.CenterPosition);
			}
			else if (order.OrderString == "Mover:Stop")
			{
				moverDir = WVec.Zero;
			}
		}

		IEnumerable<Graphics.IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, Graphics.WorldRenderer wr)
		{
			if (lockAnim == null || lockedOnActor == null)
				return Enumerable.Empty<IRenderable>();
			var screenPos = wr.ScreenPxPosition(lockedOnActor.CenterPosition);
			return new IRenderable[]
			{
				new SpriteRenderable(lockAnim.Image, lockedOnActor.CenterPosition, WVec.Zero, 1024,
					wr.Palette(Info.LockOnAnimPalette + (Info.LockOnAnimIsPlayerPalette ? self.Owner.InternalName : "")), 1f, 1f, float3.Ones, TintModifiers.IgnoreWorldTint, true)
			};
		}
	}
}
