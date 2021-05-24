using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.TA.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Traits
{
	[Desc("步兵将会面向目标并且举枪后才能开火，必须使用专用的Attack Activity，必须存在WithSmoothInfantryBody。")]
	public class AttackInfantryInfo : AttackBaseInfo, Requires<IFacingInfo>
	{
		[Desc("Tolerance for attack angle. Range [0, 512], 512 covers 360 degrees.")]
		public readonly new WAngle FacingTolerance = WAngle.Zero;

		[Desc("The angle relative to the actor's orientation used to fire the weapon from.")]
		public readonly WAngle FiringAngle = WAngle.Zero;

		public override object Create(ActorInitializer init) { return new AttackInfantry(init.Self, this); }
	}

	public class AttackInfantry : AttackBase
	{
		public new readonly AttackInfantryInfo Info;

		WithSmoothInfantryBody infantryBody;

		public AttackInfantry(Actor self, AttackInfantryInfo info)
			: base(self, info)
		{
			Info = info;
		}

		public bool TargetInInfantryFiringArc(Actor self, in Target target, WAngle facingTolerance)
		{
			if (facing == null)
				return true;

			var pos = self.CenterPosition;
			var targetedPosition = GetTargetPosition(pos, target);
			var delta = targetedPosition - pos;

			if (delta.HorizontalLengthSquared == 0 && infantryBody.ReadyToFire && !infantryBody.TransformAnimating)
				return true;

			if (Util.FacingWithinTolerance(facing.Facing, delta.Yaw + Info.FiringAngle, facingTolerance))
			{
				infantryBody = self.Trait<WithSmoothInfantryBody>();

				if (!infantryBody.ReadyToFire && !infantryBody.TransformAnimating)
					infantryBody.StartPrepare = true;
				if (infantryBody.ReadyToFire && !infantryBody.TransformAnimating)
					return true;
			}

			return false;
		}

		protected override bool CanAttack(Actor self, in Target target)
		{
			if (!base.CanAttack(self, target))
			{
				return false;
			}

			return TargetInInfantryFiringArc(self, target, Info.FacingTolerance);
		}

		protected override void Tick(Actor self)
		{
			base.Tick(self);
		}

		public override Activity GetAttackActivity(Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
		{
			return new Activities.Attack(self, newTarget, allowMove, forceAttack, targetLineColor);
		}
	}
}
