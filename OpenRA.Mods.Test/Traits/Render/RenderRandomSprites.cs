using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.TA.Traits.Render
{
	class Tools
	{
		public static string SplitAndRandom(string input, World world)
		{
			string[] strs = input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			int i = world.SharedRandom.Next(strs.Length);
			return strs[i];
		}
	}

	public interface IRenderActorPreviewSpritesInfo : ITraitInfoInterface
	{
		IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p);
	}

	[Desc("用于随机使用素材序列和色盘")]
	public class RenderRandomSpritesInfo : TraitInfo, IRenderActorPreviewInfo
	{
		[Desc("The sequence name that defines the actor sprites. Defaults to the actor name.")]
		public readonly string Images = null;

		[Desc("A dictionary of faction-specific image overrides.")]
		public readonly Dictionary<string, string> FactionImages = null;

		[PaletteReference]
		[Desc("Custom palette name")]
		public readonly string Palette = null;

		[PaletteReference(true)]
		[Desc("Custom PlayerColorPalette: BaseName")]
		public readonly string PlayerPalettes = "player";

		public override object Create(ActorInitializer init) { return new RenderRandomSprites(init, this); }

		public IEnumerable<IActorPreview> RenderPreview(ActorPreviewInitializer init)
		{
			var sequenceProvider = init.World.Map.Rules.Sequences;
			var faction = init.GetValue<FactionInit, string>(this);
			var ownerName = init.Get<OwnerInit>().InternalName;
			var image = GetImage(init, sequenceProvider, faction);

			var palette = init.WorldRenderer.Palette(Palette ?? Tools.SplitAndRandom(PlayerPalettes, init.World) + ownerName);

			var facings = 0;
			var body = init.Actor.TraitInfoOrDefault<BodyOrientationInfo>();
			if (body != null)
			{
				facings = body.QuantizedFacings;

				if (facings == -1)
				{
					var qbo = init.Actor.TraitInfoOrDefault<IQuantizeBodyOrientationInfo>();
					facings = qbo != null ? qbo.QuantizedBodyFacings(init.Actor, sequenceProvider, faction) : 1;
				}
			}

			foreach (var spi in init.Actor.TraitInfos<IRenderActorPreviewSpritesInfo>())
				foreach (var preview in spi.RenderPreviewSprites(init, image, facings, palette))
					yield return preview;
		}

		public string GetImage(ActorPreviewInitializer init, SequenceProvider sequenceProvider, string faction)
		{
			if (FactionImages != null && !string.IsNullOrEmpty(faction) && FactionImages.TryGetValue(faction, out var factionImages))
			{
				return Tools.SplitAndRandom(factionImages, init.World).ToLowerInvariant();
			}

			return (Tools.SplitAndRandom(Images, init.World) ?? init.Actor.Name).ToLowerInvariant();
		}

		public string GetImage(Actor actor, SequenceProvider sequenceProvider, string faction)
		{
			if (FactionImages != null && !string.IsNullOrEmpty(faction) && FactionImages.TryGetValue(faction, out var factionImages))
			{
				return Tools.SplitAndRandom(factionImages, actor.World).ToLowerInvariant();
			}

			return (Tools.SplitAndRandom(Images, actor.World) ?? actor.Info.Name).ToLowerInvariant();
		}
	}

	public class RenderRandomSprites : IRender, ITick, INotifyOwnerChanged, INotifyEffectiveOwnerChanged, IActorPreviewInitModifier
	{
		static readonly (DamageState DamageState, string Prefix)[] DamagePrefixes =
		{
			(DamageState.Critical, "critical-"),
			(DamageState.Heavy, "damaged-"),
			(DamageState.Medium, "scratched-"),
			(DamageState.Light, "scuffed-")
		};

		class AnimationWrapper
		{
			public readonly AnimationWithOffset Animation;
			public readonly string Palette;
			public readonly bool IsPlayerPalette;
			public PaletteReference PaletteReference { get; private set; }

			bool cachedVisible;
			WVec cachedOffset;
			ISpriteSequence cachedSequence;

			public AnimationWrapper(AnimationWithOffset animation, string palette, bool isPlayerPalette)
			{
				Animation = animation;
				Palette = palette;
				IsPlayerPalette = isPlayerPalette;
			}

			public void CachePalette(WorldRenderer wr, Player owner)
			{
				PaletteReference = wr.Palette(IsPlayerPalette ? Palette + owner.InternalName : Palette);
			}

			public void OwnerChanged()
			{
				// Update the palette reference next time we draw
				if (IsPlayerPalette)
					PaletteReference = null;
			}

			public bool IsVisible
			{
				get
				{
					return Animation.DisableFunc == null || !Animation.DisableFunc();
				}
			}

			public bool Tick()
			{
				// Tick the animation
				Animation.Animation.Tick();

				// Return to the caller whether the renderable position or size has changed
				var visible = IsVisible;
				var offset = Animation.OffsetFunc != null ? Animation.OffsetFunc() : WVec.Zero;
				var sequence = Animation.Animation.CurrentSequence;

				var updated = visible != cachedVisible || offset != cachedOffset || sequence != cachedSequence;
				cachedVisible = visible;
				cachedOffset = offset;
				cachedSequence = sequence;

				return updated;
			}
		}

		public readonly RenderRandomSpritesInfo Info;
		readonly string faction;
		readonly List<AnimationWrapper> anims = new List<AnimationWrapper>();
		string cachedImage;
		public readonly World World;

		public static Func<WAngle> MakeFacingFunc(Actor self)
		{
			var facing = self.TraitOrDefault<IFacing>();
			if (facing == null)
				return () => WAngle.Zero;

			return () => facing.Facing;
		}

		public RenderRandomSprites(ActorInitializer init, RenderRandomSpritesInfo info)
		{
			Info = info;
			faction = init.GetValue<FactionInit, string>(init.Self.Owner.Faction.InternalName);
			World = init.World;
		}

		public string GetImage(Actor self)
		{
			if (cachedImage != null)
				return cachedImage;

			return cachedImage = Info.GetImage(self, self.World.Map.Rules.Sequences, faction);
		}

		public void UpdatePalette()
		{
			foreach (var anim in anims)
				anim.OwnerChanged();
		}

		public virtual void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner) { UpdatePalette(); }
		public void OnEffectiveOwnerChanged(Actor self, Player oldEffectiveOwner, Player newEffectiveOwner) { UpdatePalette(); }

		public virtual IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr)
		{
			foreach (var a in anims)
			{
				if (!a.IsVisible)
					continue;

				if (a.PaletteReference == null)
				{
					var owner = self.EffectiveOwner != null && self.EffectiveOwner.Disguised ? self.EffectiveOwner.Owner : self.Owner;
					a.CachePalette(wr, owner);
				}

				foreach (var r in a.Animation.Render(self, wr, a.PaletteReference))
					yield return r;
			}
		}

		public virtual IEnumerable<Rectangle> ScreenBounds(Actor self, WorldRenderer wr)
		{
			foreach (var a in anims)
				if (a.IsVisible)
					yield return a.Animation.ScreenBounds(self, wr);
		}

		void ITick.Tick(Actor self)
		{
			Tick(self);
		}

		protected virtual void Tick(Actor self)
		{
			var updated = false;
			foreach (var a in anims)
				updated |= a.Tick();

			if (updated)
				self.World.ScreenMap.AddOrUpdate(self);
		}

		public void Add(AnimationWithOffset anim, string palette = null, bool isPlayerPalette = false)
		{
			// Use defaults
			if (palette == null)
			{
				palette = Info.Palette ?? Tools.SplitAndRandom(Info.PlayerPalettes, World);
				isPlayerPalette = Info.Palette == null;
			}

			anims.Add(new AnimationWrapper(anim, palette, isPlayerPalette));
		}

		public void Remove(AnimationWithOffset anim)
		{
			anims.RemoveAll(a => a.Animation == anim);
		}

		public static string UnnormalizeSequence(string sequence)
		{
			// Remove existing damage prefix
			foreach (var s in DamagePrefixes)
			{
				if (sequence.StartsWith(s.Prefix, StringComparison.Ordinal))
				{
					sequence = sequence.Substring(s.Prefix.Length);
					break;
				}
			}

			return sequence;
		}

		public static string NormalizeSequence(Animation anim, DamageState state, string sequence)
		{
			// Remove any existing damage prefix
			sequence = UnnormalizeSequence(sequence);

			foreach (var s in DamagePrefixes)
				if (state >= s.DamageState && anim.HasSequence(s.Prefix + sequence))
					return s.Prefix + sequence;

			return sequence;
		}

		// Required by WithSpriteBody and WithInfantryBody
		public int2 AutoSelectionSize(Actor self)
		{
			return AutoRenderSize(self);
		}

		// Required by WithSpriteBody and WithInfantryBody
		public int2 AutoRenderSize(Actor self)
		{
			return anims.Where(b => b.IsVisible
				&& b.Animation.Animation.CurrentSequence != null)
					.Select(a => (a.Animation.Animation.Image.Size.XY * a.Animation.Animation.CurrentSequence.Scale).ToInt2())
					.FirstOrDefault();
		}

		void IActorPreviewInitModifier.ModifyActorPreviewInit(Actor self, TypeDictionary inits)
		{
			if (!inits.Contains<FactionInit>())
				inits.Add(new FactionInit(faction));
		}
	}
}
