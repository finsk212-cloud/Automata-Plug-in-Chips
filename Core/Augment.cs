using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace Augments
{
	public enum AugmentRarity
	{
		Common,
		Rare,
		Epic,
		Legendary
	}

	public enum AugmentClass
	{
		Universal,
		Melee,
		Ranged,
		Magic,
		Summon,
		Support
	}

	public enum AugmentHitSource
	{
		NormalAttack,
		AugmentProc
	}

	// Base class for every augment in the mod. To add a new augment, make a new
	// class that inherits from this one (see the examples in AugmentDatabase.cs).
	public abstract class Augment
	{
		// Set only while a legacy hit hook is being dispatched. Existing augments
		// can use this to scale numeric progress, damage, duration, or chance when
		// an explicitly permitted augment proc (such as Echo Chamber) hits.
		protected float HitEffectiveness { get; private set; } = 1f;

		protected int ScaleHitEffect(int amount)
		{
			return amount <= 0 ? 0 : System.Math.Max(1, (int)(amount * HitEffectiveness));
		}

		protected bool PassesHitEffectivenessRoll()
		{
			return HitEffectiveness >= 1f || (HitEffectiveness > 0f && Main.rand.NextFloat() < HitEffectiveness);
		}

		// Augment instances are singletons shared by every player, so per-player
		// state lives on AugmentPlayer instead of on the augment itself. Gameplay
		// hooks must use the `player` they receive; this accessor exists only for
		// the parameterless display properties (StatusValue, CooldownRemaining,
		// IsCharging, ChargeIndicatorPercent), which are read exclusively by
		// client-side UI and therefore always describe the local player.
		protected static AugmentPlayer LocalPlayerState => Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
		// Unique key used for saving/loading and for dupe-checking.
		// Keep this short, lowercase, and STABLE - if you rename it later,
		// anyone who already owns it will lose it on their next load.
		public abstract string Id { get; }

		public abstract string DisplayName { get; }
		public abstract string Description { get; }
		public abstract AugmentRarity Rarity { get; }
		public abstract AugmentClass Class { get; }

		public virtual string GetDetailedDescription(Player player)
		{
			return Description;
		}

		public static float GetFortuneScaledChance(Player player, float baseChance)
		{
			if (player == null)
				return baseChance;

			float fortune = player.GetModPlayer<AugmentPlayer>().TotalFortune;
			return baseChance * (1f + fortune);
		}

		public static bool RollFortuneChance(Player player, float baseChance)
		{
			float effectiveChance = GetFortuneScaledChance(player, baseChance);
			return Main.rand.NextFloat() < effectiveChance;
		}

		public static string FormatCurrentChance(float baseChance)
		{
			var ap = Main.LocalPlayer?.GetModPlayer<AugmentPlayer>();
			float fortune = ap?.TotalFortune ?? 0f;
			float currentChance = baseChance * (1f + fortune) * 100f;
			return AugmentText.Trigger($"{currentChance:0.#}% chance");
		}

		public static string FormatFortuneChance(float baseChance, Player player)
		{
			float fortune = player?.GetModPlayer<AugmentPlayer>()?.TotalFortune ?? 0f;
			float finalChance = baseChance * (1f + fortune);
			int basePercent = (int)System.Math.Round(baseChance * 100f);
			int fortunePercent = (int)System.Math.Round(fortune * 100f);
			string finalPercentStr = (finalChance * 100f).ToString("0.0") + "%";

			if (fortune > 0f)
				return $"{AugmentText.Trigger(basePercent + "% chance")} [c/94A3B8:({basePercent}% × (1 + {fortunePercent}% Fortune) = {finalPercentStr})]";
			else
				return $"{AugmentText.Trigger(basePercent + "% chance")} [c/94A3B8:({basePercent}% × (1 + 0% Fortune) = {basePercent}%)]";
		}

		// Debug/testing augments are never offered by RollChoices, but can
		// still be granted directly via the /augment command.
		public virtual bool IsDebugOnly => false;

		// True for augments that can never be sold/removed once acquired
		// (e.g. the Avatar augments) - the vendor's Remove list shows these
		// with a "(Permanent)" label instead of a working sell button.
		public virtual bool IsPermanent => false;

		// True for augments whose effect operates on a proximity radius around the
		// owner — used by AugmentAuraDrawer to decide whether to show the aura circle.
		public virtual bool HasAuraEffect => false;

		// Non-null for augments triggered by a keybind (e.g. Cleanse).
		// AugmentListEntry reads this to show a "no key bound" hint when unset.
		public virtual ModKeybind ActiveModKeybind => null;

		// Additive contribution to the player's TotalFortune (see
		// AugmentPlayer.TotalFortune), summed live across every owned augment -
		// not separately saved. Scales every Fortune-aware chance check
		// (e.g. Lucky Strike's proc chance) and biases RollChoices toward
		// lucky-themed cards.
		public virtual float FortuneBonus => 0f;

		// Marks this augment as "lucky-themed" for RollChoices' Fortune bias -
		// owning any Fortune-granting augment gives each choice-popup slot a
		// chance to specifically try for one of these instead of a fully
		// random pick.
		public virtual bool IsLuckyThemed => false;

		// Null means this augment isn't part of any Keystone family. A
		// non-null value groups it with sibling augments sharing the same
		// family string - granting one permanently locks every other augment
		// in that family out of future RollChoices results (see
		// AugmentPlayer.LockedKeystoneFamilies), while everything outside the
		// family stays fully available.
		public virtual string KeystoneFamily => null;

		// The synergy family this augment belongs to (e.g. "field_medic").
		// Owning multiple members of the same family unlocks threshold synergy bonuses.
		public virtual string FamilyId => null;

		// When true (owned by the player, not necessarily on this augment
		// itself), every owned augment's OnHitNPCWithItem/OnHitNPCWithProj
		// fires a second time on a crit - see AugmentPlayer's dispatchers.
		// Scoped specifically to those discrete on-hit reactions (debuffs,
		// bonus strikes, heals, kill triggers); ModifyHitNPCWithItem/Proj's
		// flat damage-bonus modifiers are untouched and never duplicated.
		public virtual bool DuplicatesOnHitEffects => false;

		// Ticks left before this augment's effect can trigger again. Cooldown-gated
		// augments override this to expose their private cooldownRemaining field,
		// which the cooldown indicator UI reads to know what to display.
		public virtual int CooldownRemaining => 0;

		// Optional explicit cooldown text for UI display (e.g. "30s cooldown", "12hr cooldown").
		// If null, the UI automatically extracts any cooldown clause present in Description.
		public virtual string CooldownText => null;

		// True if CooldownRemaining should be read as ticks-until-next-in-game-day
		// (displayed as whole hours) rather than a simple seconds countdown.
		public virtual bool CooldownDisplayInHours => false;

		// A live numeric value to show as a status icon in the same row as the
		// cooldown indicators (e.g. Adaptive Armor's current defense bonus).
		// Null hides the icon. Unlike CooldownRemaining, this typically counts
		// UP rather than down.
		public virtual int? StatusValue => null;

		// True while this augment is actively building toward some effect
		// (e.g. Steady Hands ramping crit chance while standing still) - the
		// charge indicator UI shows a box for this augment whenever true.
		public virtual bool IsCharging => false;

		// The current charge progress, shown as a percentage (0-100) in the
		// charge indicator UI. Only read while IsCharging is true.
		public virtual int ChargeIndicatorPercent => 0;

		// Color for the StatusValue icon text - override to match whatever
		// AugmentText color category the description already uses for this value.
		public virtual Color StatusValueColor => Color.White;

		// Appended right after the StatusValue number (e.g. "%" for a percentage
		// stat like crit chance) - empty by default, matching the flat-number
		// values (defense, etc.) most StatusValue overrides show.
		public virtual string StatusValueSuffix => "";

		// Optional icon graphic drawn inside the cooldown/status indicator box,
		// behind the countdown/value text. Null (the default) means "no icon,
		// just show the number" - the original indicator behavior.
		public virtual Texture2D Icon => null;

		// Fires once when the player dies (after PreKill allows death through).
		// respawnTimer is already set by vanilla before this fires, so reading
		// or modifying it here reflects the correct starting value.
		public virtual void OnKill(Player player) { }

		// Fires exactly once, the instant the player picks this augment.
		// Good for one-time setup, not for anything that needs to keep happening.
		public virtual void OnAcquire(Player player) { }

		// Fires every single game tick while the player owns this augment.
		// This is where most "passive" effects will live (regen ticks, timers, etc).
		public virtual void OnUpdate(Player player) { }

		// Fires after vanilla run speeds are calculated for the player.
		public virtual void PostUpdateRunSpeeds(Player player) { }

		// Fires every tick, after statDefense is reset to its base/armor value
		// but before it's finalized for the frame - the correct place to add a
		// temporary flat defense bonus (same timing buffs/accessories use).
		public virtual void UpdateEquips(Player player) { }

		// Fires during Player.UpdateLifeRegen(), after vanilla calculations -
		// the correct place to modify or halt player.lifeRegen.
		public virtual void UpdateLifeRegen(Player player) { }

		// Fires during Player.UpdateBadLifeRegen(), after UpdateLifeRegen -
		// the definitive place to enforce negative regen or clamp positive regen to 0.
		public virtual void UpdateBadLifeRegen(Player player) { }

		// Fires whenever the player hits an NPC with a held weapon (melee, etc).
		// `hit` contains the finalized damage/crit info for that swing.
		public virtual void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit) { }

		// Source-aware entry point used by AugmentPlayer. Proc damage is only
		// dispatched here when its projectile tag explicitly allows on-hit effects.
		public virtual void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source)
		{
			OnHitNPCWithItem(player, item, target, hit, source, 1f);
		}

		public virtual void OnHitNPCWithItem(Player player, Item item, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
		{
			float previousEffectiveness = HitEffectiveness;
			HitEffectiveness = effectiveness;
			try
			{
				OnHitNPCWithItem(player, item, target, hit);
			}
			finally
			{
				HitEffectiveness = previousEffectiveness;
			}

			OnAugmentHitNPC(player, target, hit, source, effectiveness);
		}

		// Fires whenever the player hits an NPC with a projectile. This also
		// covers thrust-style melee weapons (short swords, spears) which
		// register their hit through a projectile instead of a direct swing -
		// any melee augment that reacts to hits should implement BOTH this
		// and OnHitNPCWithItem, or it'll silently miss those weapons.
		public virtual void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit) { }

		public virtual void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source)
		{
			OnHitNPCWithProj(player, proj, target, hit, source, 1f);
		}

		public virtual void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
		{
			float previousEffectiveness = HitEffectiveness;
			HitEffectiveness = effectiveness;
			try
			{
				OnHitNPCWithProj(player, proj, target, hit);
			}
			finally
			{
				HitEffectiveness = previousEffectiveness;
			}

			OnAugmentHitNPC(player, target, hit, source, effectiveness);
		}

		// Shared opt-in hook for augments that intentionally react to both
		// ordinary attacks and damage created by another augment.
		public virtual void OnAugmentHitNPC(Player player, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness) { }

		// Fires BEFORE a melee weapon hit is finalized - use this (not the
		// OnHit hooks above) to actually boost outgoing damage, via
		// modifiers.FlatBonusDamage += amount. Same short-sword/spear caveat
		// applies: implement both this and the WithProj version below.
		public virtual void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers) { }

		public virtual void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source)
		{
			ModifyHitNPCWithItem(player, item, target, ref modifiers, source, 1f);
		}

		public virtual void ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
		{
			if (source == AugmentHitSource.NormalAttack)
			{
				float previousEffectiveness = HitEffectiveness;
				HitEffectiveness = effectiveness;
				try
				{
					ModifyHitNPCWithItem(player, item, target, ref modifiers);
				}
				finally
				{
					HitEffectiveness = previousEffectiveness;
				}
			}

			ModifyAugmentHitNPC(player, target, ref modifiers, source, effectiveness);
		}

		// Projectile equivalent of the above (covers thrust melee weapons).
		public virtual void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers) { }

		public virtual void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source)
		{
			ModifyHitNPCWithProj(player, proj, target, ref modifiers, source, 1f);
		}

		public virtual void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness)
		{
			if (source == AugmentHitSource.NormalAttack)
			{
				float previousEffectiveness = HitEffectiveness;
				HitEffectiveness = effectiveness;
				try
				{
					ModifyHitNPCWithProj(player, proj, target, ref modifiers);
				}
				finally
				{
					HitEffectiveness = previousEffectiveness;
				}
			}

			ModifyAugmentHitNPC(player, target, ref modifiers, source, effectiveness);
		}

		// Modifier counterpart to OnAugmentHitNPC. Legacy modifier hooks remain
		// normal-attack-only; future augments can selectively handle proc damage here.
		public virtual void ModifyAugmentHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers, AugmentHitSource source, float effectiveness) { }

		// Fires before any damage calculation, for guaranteed/random no-cost
		// dodges (the same hook Black Belt's own dodge runs through, per
		// tModLoader's docs) - return true to fully negate the hit. Unlike
		// ModifyHurt's Cancel(), a successful dodge through this hook also
		// automatically grants vanilla's ~1.33s post-dodge invincibility
		// window, with no extra code needed. Use ConsumableDodge instead for
		// anything that should consume a stack/buff or have its own cooldown.
		public virtual bool FreeDodge(Player player, Player.HurtInfo info) => false;

		// Fires while incoming damage is being calculated, before it's applied -
		// use modifiers.SetMaxDamage(limit) to cap the resulting hit (e.g. to
		// survive a killing blow at 1 HP) rather than trying to undo the damage after the fact.
		public virtual void ModifyHurt(Player player, ref Player.HurtModifiers modifiers) { }

		// Fires right after the player takes damage from any source.
		public virtual void OnHurt(Player player, Player.HurtInfo info) { }

		// Fires right after the player takes damage from a direct NPC hit
		// specifically (contact damage) - gives a reference to the NPC that
		// hit them, unlike the generic OnHurt above. Does NOT fire for
		// damage from NPC-owned projectiles (ranged enemy attacks).
		public virtual void OnHitByNPC(Player player, NPC npc, Player.HurtInfo hurtInfo) { }

		// Fires while a weapon's crit chance is being calculated.
		public virtual void ModifyWeaponCrit(Player player, Item item, ref float crit) { }

		// Allows augments to modify the life restored by healing potions.
		public virtual void GetHealLife(Player player, Item item, bool quickHeal, ref int healValue) { }

		// Fires when the player consumes an item.
		public virtual void OnConsumeItem(Player player, Item item) { }

		// Fires when the player right-clicks an item (such as opening crates/grab bags).
		public virtual void RightClickItem(Player player, Item item) { }

		// Fires when an enemy kill is credited to the player.
		public virtual void OnKillNPC(Player player, NPC npc) { }

		// Fires once per fishing attempt, before vanilla rolls what (if
		// anything) gets caught - bump attempt.fishingLevel to bias toward
		// rarer catches, the same field actual fishing rods/bait modify.
		public virtual void ModifyFishingAttempt(Player player, ref FishingAttempt attempt) { }

		// Fires while the current fishing power is being assembled from the
		// equipped rod and bait, before a cast resolves - add to
		// fishingLevel here to boost it. `bait` is the actual bait Item in
		// use (its own Item.bait field is its raw power contribution).
		public virtual void GetFishingLevel(Player player, Item fishingRod, Item bait, ref float fishingLevel) { }

		// Fires once, the instant any projectile is spawned by this player -
		// covers minions, sentries, thrown weapons, everything. Distinct
		// from OnHitNPCWithProj (which only fires on a landed hit) since
		// some properties (e.g. a sentry's total lifespan) need to be set
		// once at spawn, not reacted to per-hit.
		public virtual void OnProjectileSpawn(Player player, Projectile projectile) { }

		// Fires only for a projectile spawned directly by using a weapon.
		public virtual void OnShootProjectile(Player player, Item item, Projectile projectile) { }

		// Augment instances are recreated fresh every mod load, so any custom
		// persistent stat (e.g. Lucky Find's lifetime coin counter) needs to be
		// explicitly written here and restored via LoadCustomData below -
		// AugmentPlayer handles calling these into/out of a per-augment sub-tag.
		public virtual void SaveCustomData(TagCompound tag) { }
		public virtual void LoadCustomData(TagCompound tag) { }

		// Fires while a spell's mana cost is being calculated, before it's
		// spent - `mult` scales the cost (mult = 0f means free for that cast).
		public virtual void ModifyManaCost(Player player, Item item, ref float reduce, ref float mult) { }

		// Fires after mana is successfully consumed by an item use.
		public virtual void OnConsumeMana(Player player, Item item, int manaConsumed) { }

		// Fires when a ranged weapon is about to consume ammo for a shot -
		// return false to skip consuming that ammo this shot. Default true
		// (consume normally).
		public virtual bool CanConsumeAmmo(Player player, Item weapon, Item ammo) => true;

		// Reforging only ever happens through the local reforge UI acting on
		// Main.LocalPlayer - AugmentGlobalItem forwards these three hooks
		// straight from GlobalItem, passing Main.LocalPlayer along so augment
		// code reads the same as every other hook here despite GlobalItem's
		// own signatures not taking a Player. reforgePrice/canApplyDiscount
		// are the same ref parameters ItemLoader.ReforgePrice exposes -
		// mutate reforgePrice directly to change the price, and always leave
		// this returning true (a false return skips ALL ReforgePrice hooks'
		// effects for that reforge, including vanilla's own happiness
		// discount, not just this one).
		public virtual bool ReforgePrice(Player player, Item item, ref int reforgePrice, ref bool canApplyDiscount) => true;

		// Fires right before vanilla resets and rerolls the item's prefix -
		// item.prefix here is still the OLD prefix, the correct place to
		// snapshot "what it was before this reforge".
		public virtual void PreReforge(Player player, Item item) { }

		// Fires right after the new prefix (and its stat bonuses) are fully applied.
		public virtual void PostReforge(Player player, Item item) { }
	}
}
