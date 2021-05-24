using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.TA.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Traits
{
	[Desc("WithSmoothInfantryBody专用的TakeCover，在匍匐时提供一个condition，并且使得步兵可以播放过渡动画。必须存在WithSmoothInfantryBody。")]
	public class TakeCoverSmoothlyInfo : TurretedInfo
	{
		[Desc("How long (in ticks) the actor remains prone.",
			"Negative values mean actor remains prone permanently.")]
		public readonly int Duration = 100;

		[Desc("Prone movement speed as a percentage of the normal speed.")]
		public readonly int SpeedModifier = 50;

		[Desc("Damage types that trigger prone state. Defined on the warheads.",
			"If Duration is negative (permanent), you can leave this empty to trigger prone state immediately.")]
		public readonly BitSet<DamageType> DamageTriggers = default(BitSet<DamageType>);

		[Desc("Damage modifiers for each damage type (defined on the warheads) while the unit is prone.")]
		public readonly Dictionary<string, int> DamageModifiers = new Dictionary<string, int>();

		[Desc("Muzzle offset modifier to apply while prone.")]
		public readonly WVec ProneOffset = new WVec(500, 0, 0);

		[SequenceReference(prefix: true)]
		[Desc("Sequence prefix to apply while prone.")]
		public readonly string ProneSequencePrefix = "prone_";

		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = "isprone";

		public override object Create(ActorInitializer init) { return new TakeCoverSmoothly(init, this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (Duration > -1 && DamageTriggers.IsEmpty)
				throw new YamlException("TakeCoverTA: If Duration isn't negative (permanent), DamageTriggers is required.");

			base.RulesetLoaded(rules, ai);
		}
	}

	public class TakeCoverSmoothly : Turreted, INotifyDamage, IDamageModifier, ISpeedModifier, ISync, IRenderInfantrySequenceModifier
	{
		readonly TakeCoverSmoothlyInfo info;
		WithSmoothInfantryBody infantryBody;
		int conditionToken = Actor.InvalidConditionToken;
		[Sync]
		int remainingDuration = 0;

		public bool IsProne { get { return !IsTraitDisabled && remainingDuration != 0; } }

		bool IRenderInfantrySequenceModifier.IsModifyingSequence { get { return IsProne; } }
		string IRenderInfantrySequenceModifier.SequencePrefix { get { return info.ProneSequencePrefix; } }

		public TakeCoverSmoothly(ActorInitializer init, TakeCoverSmoothlyInfo info)
			: base(init, info)
		{
			this.info = info;
			if (info.Duration < 0 && info.DamageTriggers.IsEmpty)
				remainingDuration = info.Duration;
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (IsTraitPaused || IsTraitDisabled)
				return;

			if (e.Damage.Value <= 0 || !e.Damage.DamageTypes.Overlaps(info.DamageTriggers))
				return;

			if (!IsProne)
				localOffset = info.ProneOffset;

			remainingDuration = info.Duration;
		}

		protected override void Tick(Actor self)
		{
			base.Tick(self);

			if (!IsTraitPaused && remainingDuration > 0)
				remainingDuration--;

			if (remainingDuration != 0)
				localOffset = WVec.Zero;

			if (IsProne && conditionToken == Actor.InvalidConditionToken)
			{
				infantryBody = self.Trait<WithSmoothInfantryBody>();
				infantryBody.IsProne = IsProne;
				infantryBody.TransformProne = true;
				conditionToken = self.GrantCondition(info.Condition);
			}
			else if (!IsProne && conditionToken != Actor.InvalidConditionToken)
			{
				infantryBody = self.Trait<WithSmoothInfantryBody>();
				infantryBody.IsProne = IsProne;
				infantryBody.TransformProne = true;
				conditionToken = self.RevokeCondition(conditionToken);
			}
		}

		public override bool HasAchievedDesiredFacing
		{
			get { return true; }
		}

		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
		{
			if (!IsProne)
				return 100;

			if (damage == null || damage.DamageTypes.IsEmpty)
				return 100;

			var modifierPercentages = info.DamageModifiers.Where(x => damage.DamageTypes.Contains(x.Key)).Select(x => x.Value);
			return Util.ApplyPercentageModifiers(100, modifierPercentages);
		}

		int ISpeedModifier.GetSpeedModifier()
		{
			return IsProne ? info.SpeedModifier : 100;
		}

		protected override void TraitDisabled(Actor self)
		{
			remainingDuration = 0;
		}

		protected override void TraitEnabled(Actor self)
		{
			if (info.Duration < 0 && info.DamageTriggers.IsEmpty)
			{
				remainingDuration = info.Duration;
				localOffset = info.ProneOffset;
			}
		}
	}
}
