using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Traits.Render
{
	/// <summary>
	/// 特殊的步兵body，有多种过渡动画，必须配套使用RenderRandomSprites以随机选择素材sequence。
	/// 同时应该使用TakeCoverSmoothly，AttackInfantry来改变InfantryBody的状态以播放过渡动画。
	/// </summary>
	[Desc("特殊的步兵body，有多种过渡动画，必须配套使用RenderRandomSprites以随机选择素材sequence。")]
	public class WithSmoothInfantryBodyInfo : ConditionalTraitInfo, IRenderActorPreviewSpritesInfo, Requires<IMoveInfo>, Requires<RenderRandomSpritesInfo>
	{
		public readonly int MinIdleDelay = 30;
		public readonly int MaxIdleDelay = 110;

		public readonly int MinGuardDelay = 50;
		public readonly int MaxGuardDelay = 80;

		[SequenceReference]
		public readonly string DefaultAttackSequence = null;

		[SequenceReference(dictionaryReference: LintDictionaryReference.Values)]
		[Desc("Attack sequence to use for each armament.",
			"A dictionary of [armament name]: [sequence name(s)].",
			"Multiple sequence names can be defined to specify per-burst animations.")]
		public readonly Dictionary<string, string[]> AttackSequences = new Dictionary<string, string[]>();

		[SequenceReference]
		public readonly string[] IdleSequences = { };

		[SequenceReference]
		public readonly string[] StandSequences = { "stand" };

		[SequenceReference]
		public readonly string[] StandToGuardSequences = { "stand->guard" };

		[SequenceReference]
		public readonly string[] GuardSequences = { "guard" };

		[SequenceReference]
		public readonly string[] GuardToStandSequences = { "guard->stand" };

		// move
		[SequenceReference]
		public readonly string MoveSequence = "run";

		[SequenceReference]
		public readonly string[] GuardToMoveSequences = { "guard->run" };

		[SequenceReference]
		public readonly string[] StandToMoveSequences = { "stand->run" };

		[SequenceReference]
		public readonly string[] MoveToStandSequences = { "run->stand" };

		// prone
		[SequenceReference]
		public readonly string[] StandToProneSequences = { "stand->prone" };

		[SequenceReference]
		public readonly string[] ProneToStandSequences = { "prone->stand" };

		[PaletteReference(nameof(IsPlayerPalette))]
		[Desc("Custom palette name")]
		public readonly string Palette = null;

		[Desc("Palette is a player palette BaseName")]
		public readonly bool IsPlayerPalette = false;

		public override object Create(ActorInitializer init) { return new WithSmoothInfantryBody(init, this); }

		public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
		{
			if (!EnabledByDefault)
				yield break;

			var anim = new Animation(init.World, image, init.GetFacing());
			anim.PlayRepeating(RenderRandomSprites.NormalizeSequence(anim, init.GetDamageState(), StandSequences.First()));

			if (IsPlayerPalette)
				p = init.WorldRenderer.Palette(Palette + init.Get<OwnerInit>().InternalName);
			else if (Palette != null)
				p = init.WorldRenderer.Palette(Palette);

			yield return new SpriteActorPreview(anim, () => WVec.Zero, () => 0, p);
		}
	}

	public class WithSmoothInfantryBody : ConditionalTrait<WithSmoothInfantryBodyInfo>, ITick, INotifyAttack, INotifyIdle
	{
		IMove move;
		protected readonly Animation DefaultAnimation;

		bool dirty;
		string idleSequence;
		int idleDelay;
		int guardDelay;

		public bool TransformAnimating;
		public bool StartPrepare;
		public bool ReadyToFire;
		public bool TransformProne;
		public bool IsProne;

		bool hasSGTransformAnim;
		bool hasSPTransformAnim;

		AnimationState state;
		IRenderInfantrySequenceModifier rsm;

		bool IsModifyingSequence { get { return rsm != null && rsm.IsModifyingSequence; } }
		bool wasModifying;

		// Allow subclasses to override the info that we use for rendering
		protected virtual WithSmoothInfantryBodyInfo GetDisplayInfo()
		{
			return Info;
		}

		public WithSmoothInfantryBody(ActorInitializer init, WithSmoothInfantryBodyInfo info)
			: base(info)
		{
			var self = init.Self;
			var rs = self.Trait<RenderRandomSprites>();
			StartPrepare = false;
			ReadyToFire = false;
			TransformAnimating = false;
			TransformProne = false;
			IsProne = false;

			DefaultAnimation = new Animation(init.World, rs.GetImage(self), RenderRandomSprites.MakeFacingFunc(self));
			rs.Add(new AnimationWithOffset(DefaultAnimation, null, () => IsTraitDisabled), info.Palette, info.IsPlayerPalette);
			PlayStandAnimation(self);

			move = init.Self.Trait<IMove>();
		}

		protected override void Created(Actor self)
		{
			rsm = self.TraitOrDefault<IRenderInfantrySequenceModifier>();
			var info = GetDisplayInfo();
			idleDelay = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);

			base.Created(self);
		}

		protected virtual string NormalizeInfantrySequence(Actor self, string baseSequence)
		{
			var prefix = IsModifyingSequence ? rsm.SequencePrefix : "";

			if (DefaultAnimation.HasSequence(prefix + baseSequence) && IsProne)
				return prefix + baseSequence;

			return baseSequence;
		}

		protected virtual bool AllowIdleAnimation(Actor self)
		{
			return GetDisplayInfo().IdleSequences.Length > 0 && !IsModifyingSequence;
		}

		public void PlayStandAnimation(Actor self)
		{
			state = AnimationState.Waiting;
			ReadyToFire = false;
			TransformAnimating = false;
			StartPrepare = false;
			var sequence = DefaultAnimation.GetRandomExistingSequence(Info.StandSequences, Game.CosmeticRandom);
			if (sequence != null)
			{
				var normalized = NormalizeInfantrySequence(self, sequence);
				DefaultAnimation.PlayRepeating(normalized);
			}
		}

		public void PlayGuardAnimation(Actor self)
		{
			if (state != AnimationState.Guarding)
			{
				state = AnimationState.Guard;
			}

			StartPrepare = false;
			TransformAnimating = false;
			ReadyToFire = true;
			var sequence = DefaultAnimation.GetRandomExistingSequence(Info.GuardSequences, Game.CosmeticRandom);
			if (sequence != null)
			{
				var normalized = NormalizeInfantrySequence(self, sequence);
				DefaultAnimation.PlayRepeating(normalized);
			}
		}

		public void PlayGuardTransformAnimation(Actor self, bool guardToStand = false)
		{
			StartPrepare = false;
			TransformAnimating = true;

			var sequenceSG = DefaultAnimation.GetRandomExistingSequence(Info.StandToGuardSequences, Game.CosmeticRandom);
			var sequenceGS = DefaultAnimation.GetRandomExistingSequence(Info.GuardToStandSequences, Game.CosmeticRandom);
			if (sequenceGS == null)
			{
				var normalized = NormalizeInfantrySequence(self, sequenceSG);
				if (guardToStand)
					DefaultAnimation.PlayBackwardsThen(normalized, () =>
					{
						ReadyToFire = false;
						TransformAnimating = false;
						PlayStandAnimation(self);
					});
				else
					DefaultAnimation.PlayThen(normalized, () =>
					{
						ReadyToFire = true;
						TransformAnimating = false;
						state = AnimationState.Guard;
					});
			}
			else
			{
				var normalizedSG = NormalizeInfantrySequence(self, sequenceSG);
				var normalizedGS = NormalizeInfantrySequence(self, sequenceGS);
				if (guardToStand)
					DefaultAnimation.PlayThen(normalizedGS, () =>
					{
						ReadyToFire = false;
						TransformAnimating = false;
						PlayStandAnimation(self);
					});
				else
					DefaultAnimation.PlayThen(normalizedSG, () =>
					{
						ReadyToFire = true;
						TransformAnimating = false;
						state = AnimationState.Guard;
					});
			}
		}

		public void PlayProneTransformAnimation(Actor self, bool getUp = false)
		{
			StartPrepare = false;
			TransformAnimating = true;
			ReadyToFire = false;
			var sequenceSP = DefaultAnimation.GetRandomExistingSequence(Info.StandToProneSequences, Game.CosmeticRandom);
			var sequencePS = DefaultAnimation.GetRandomExistingSequence(Info.ProneToStandSequences, Game.CosmeticRandom);
			if (getUp && sequencePS != null)
			{
				DefaultAnimation.PlayThen(sequencePS, () =>
				{
					TransformAnimating = false;
					PlayStandAnimation(self);
				});
			}
			else if (!getUp && sequenceSP != null)
			{
				DefaultAnimation.PlayThen(sequenceSP, () =>
				{
					TransformAnimating = false;
					PlayStandAnimation(self);
				});
			}
			else
			{
				TransformAnimating = false;
			}
		}

		void Attacking(Actor self, Armament a, Barrel barrel)
		{
			var info = GetDisplayInfo();
			var sequence = info.DefaultAttackSequence;

			if (info.AttackSequences.TryGetValue(a.Info.Name, out var sequences) && sequences.Length > 0)
			{
				sequence = sequences[0];

				// Find the sequence corresponding to this barrel/burst.
				if (barrel != null && sequences.Length > 1)
					for (var i = 0; i < sequences.Length; i++)
						if (a.Barrels[i] == barrel)
							sequence = sequences[i];
			}

			if (!string.IsNullOrEmpty(sequence) && DefaultAnimation.HasSequence(NormalizeInfantrySequence(self, sequence)))
			{
				state = AnimationState.Attacking;

				// DefaultAnimation.PlayThen(NormalizeInfantrySequence(self, sequence), () => PlayStandAnimation(self));
				DefaultAnimation.PlayThen(NormalizeInfantrySequence(self, sequence), () => state = AnimationState.Guard);
			}
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
		{
			// HACK: The FrameEndTask makes sure that this runs after Tick(), preventing that from
			// overriding the animation when an infantry unit stops to attack
			self.World.AddFrameEndTask(_ => Attacking(self, a, barrel));
		}

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel) { }

		void ITick.Tick(Actor self)
		{
			Tick(self);
		}

		protected virtual void Tick(Actor self)
		{
			if (rsm != null)
			{
				if (wasModifying != rsm.IsModifyingSequence)
					dirty = true;

				wasModifying = rsm.IsModifyingSequence;
			}

			if (TransformAnimating)
			{
				// keep playing Transform Animation
			}
			else if (TransformProne && state != AnimationState.Attacking)
			{
				// need TakeCoverSmooth to work
				TransformProne = false;
				if (IsProne)
				{
					PlayProneTransformAnimation(self);
				}
				else
				{
					PlayProneTransformAnimation(self, true);
				}
			}
			else if (StartPrepare)
			{
				PlayGuardTransformAnimation(self);
			}
			else if ((state != AnimationState.Moving || dirty) && move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
			{
				state = AnimationState.Moving;
				StartPrepare = false;
				if (!TransformAnimating)
				{
					if (ReadyToFire)
					{
						// Guard To Move
						ReadyToFire = false;
						TransformAnimating = true;
						var sequenceGM = DefaultAnimation.GetRandomExistingSequence(Info.GuardToMoveSequences, Game.CosmeticRandom);
						if (sequenceGM != null)
						{
							var normalized = NormalizeInfantrySequence(self, sequenceGM);
							DefaultAnimation.PlayThen(normalized, () =>
							{
								TransformAnimating = false;
								DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, GetDisplayInfo().MoveSequence));
							});
						}
						else
						{
							TransformAnimating = false;
							DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, GetDisplayInfo().MoveSequence));
						}
					}
					else
					{
						// Stand to Move
						TransformAnimating = true;
						var sequenceSM = DefaultAnimation.GetRandomExistingSequence(Info.StandToMoveSequences, Game.CosmeticRandom);
						if (sequenceSM != null)
						{
							var normalized = NormalizeInfantrySequence(self, sequenceSM);
							DefaultAnimation.PlayThen(normalized, () =>
							{
								TransformAnimating = false;
								DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, GetDisplayInfo().MoveSequence));
							});
						}
						else
						{
							TransformAnimating = false;
							DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, GetDisplayInfo().MoveSequence));
						}
					}
				}
				else
				{
					// Move
					DefaultAnimation.PlayRepeating(NormalizeInfantrySequence(self, GetDisplayInfo().MoveSequence));
				}
			}
			else if (((state == AnimationState.Moving || dirty) && !move.CurrentMovementTypes.HasMovementType(MovementType.Horizontal))
				|| ((state == AnimationState.Idle || state == AnimationState.IdleAnimating) && !self.IsIdle && (state != AnimationState.Guarding)))
			{
					// Move To Stand
					TransformAnimating = true;
					var sequenceMS = DefaultAnimation.GetRandomExistingSequence(Info.MoveToStandSequences, Game.CosmeticRandom);
					if (sequenceMS != null)
					{
						var normalized = NormalizeInfantrySequence(self, sequenceMS);
						DefaultAnimation.PlayContinueThen(normalized, () =>
						{
							TransformAnimating = false;
							PlayStandAnimation(self);
						});
					}
					else
					{
						TransformAnimating = false;
						PlayStandAnimation(self);
					}
			}
			else if (state == AnimationState.Guarding)
			{
				ReadyToFire = true;
				PlayGuardAnimation(self);
			}

			dirty = false;
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (state == AnimationState.Guard)
			{
				var info = GetDisplayInfo();
				state = AnimationState.Guarding;
				guardDelay = self.World.SharedRandom.Next(info.MinGuardDelay, info.MaxGuardDelay);
			}
			else if (state == AnimationState.Waiting)
			{
				if (!AllowIdleAnimation(self))
					return;
				state = AnimationState.Idle;
				var info = GetDisplayInfo();
				idleSequence = info.IdleSequences.Random(self.World.SharedRandom);
				idleDelay = self.World.SharedRandom.Next(info.MinIdleDelay, info.MaxIdleDelay);
			}
			else if (state == AnimationState.Guarding && --guardDelay <= 0)
			{
				PlayGuardTransformAnimation(self, true);
				state = AnimationState.Waiting;
			}
			else if (state == AnimationState.Idle && idleDelay > 0 && --idleDelay == 0)
			{
				if (!AllowIdleAnimation(self))
					return;
				state = AnimationState.IdleAnimating;
				DefaultAnimation.PlayThen(idleSequence, () => PlayStandAnimation(self));
			}
		}

		enum AnimationState
		{
			Idle,
			Attacking,
			Moving,
			Waiting,
			IdleAnimating,

			// new states
			Guard,
			Guarding
		}
	}
}
