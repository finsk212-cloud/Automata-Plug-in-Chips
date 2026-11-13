using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // When two or more pellets of the same shot hit the same enemy, they burst on it.
    //
    // "The same shot" means projectiles made by one use of a weapon (a shotgun blast): each one is stamped with a shot number
    // when it is fired. Hits are counted on the shooter's game, where hit hooks run. The burst is a real projectile
    // (GroupHugBurstProjectile), so it is synced like any other: the shooter's game deals the damage and every game draws it and
    // plays the sound from its first tick. Nothing needs relaying.
    public class GroupHugAugment : Augment
    {
        // Tuning
        public const float DamageFraction = 0.75f;   // share of the two pellets' combined damage
        private const int WindowTicks = 30;          // pellets that fly farther may land a moment later

        public override string Id => "pellet_burst";
        public override string DisplayName => "Group Hug";
        public override string Description =>
            $"When 2 or more pellets of one shot hit the same enemy, they burst for {AugmentText.BonusDamage("75% of their damage")} in a small area around it.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Shotguns and any ranged weapon that fires several projectiles at once count. The burst is about 4 tiles wide and goes off once per enemy per shot, " +
            "on the second pellet that lands within half a second. Its damage is 75% of those two hits together. Bursts never set off more bursts.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Boomstick].Value;

        // Shot numbering: everything fired in the same tick by the owner is one shot.
        private static int currentShot;
        private static uint currentShotTick = uint.MaxValue;

        private sealed class HitState
        {
            public int Shot;
            public int Count;
            public uint FirstTick;
            public int Damage;
            public bool Burst;
        }

        private static readonly Dictionary<int, HitState> states = new();
        private static readonly PerHitGuard guard = new();

        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (player.whoAmI != Main.myPlayer || projectile.minion || projectile.sentry || !projectile.CountsAsClass(DamageClass.Ranged))
                return;

            if (currentShotTick != Main.GameUpdateCount)
            {
                currentShotTick = Main.GameUpdateCount;
                currentShot++;
            }

            projectile.GetGlobalProjectile<GroupHugTag>().Shot = currentShot;
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (player.whoAmI != Main.myPlayer || source == AugmentHitSource.AugmentProc)
                return;

            int shot = proj.GetGlobalProjectile<GroupHugTag>().Shot;
            if (shot == 0 || !proj.CountsAsClass(DamageClass.Ranged))
                return;

            // The same hit can reach this hook twice (Twin Strike on a crit): count it once.
            if (!guard.IsFirst(proj.whoAmI, target.whoAmI))
                return;

            uint now = Main.GameUpdateCount;
            if (!states.TryGetValue(target.whoAmI, out HitState state) || state.Shot != shot || now - state.FirstTick > WindowTicks)
            {
                state = new HitState { Shot = shot, Count = 0, FirstTick = now, Damage = 0, Burst = false };
                states[target.whoAmI] = state;
            }

            state.Count++;
            state.Damage += hit.Damage;

            if (state.Count < 2 || state.Burst)
                return;

            state.Burst = true;
            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<GroupHugBurstProjectile>(),
                System.Math.Max(1, (int)(state.Damage * DamageFraction)),
                3f,
                player.whoAmI);
        }
    }

    // Remembers which shot a projectile came from.
    public class GroupHugTag : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public int Shot;
    }
}
