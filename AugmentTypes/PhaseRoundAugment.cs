using Microsoft.Xna.Framework;
using Terraria;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Augments.Core;

namespace Augments
{
    public class PhaseRoundAugment : Augment
    {
        public override string Id          => "phase_round";
        public override string DisplayName => "Phase Round";
        public override string Description =>
            $"Arrows and bullets can {AugmentText.Trigger("pass through walls")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            $"A shot is removed after {AugmentText.Duration("1 second")} spent inside blocks.\n" +
            AugmentText.Note("(Rockets, grenades and other special projectiles are not affected. Guns and bows from other mods work too, including Calamity's, even when they fire their own special shots.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass  Class  => AugmentClass.Ranged;

        public override void OnProjectileSpawn(Player player, Projectile projectile)
        {
            if (projectile.owner != player.whoAmI)
                return;

            if (!PhaseRoundProjectile.CanPhase(projectile))
                return;

            MakePhase(projectile);
        }

        // Shots that come straight out of a gun or bow count even when the mod gave them a custom projectile (a different AI style),
        // which is how most Calamity guns work. The weapon is what tells us it is a bullet or an arrow.
        public override void OnShootProjectile(Player player, Item item, Projectile projectile)
        {
            if (projectile.owner != player.whoAmI || projectile.GetGlobalProjectile<PhaseRoundProjectile>().Active)
                return;

            if (!PhaseRoundProjectile.IsGunOrBow(item) || !PhaseRoundProjectile.CanPhaseFromWeapon(projectile))
                return;

            MakePhase(projectile);
        }

        private static void MakePhase(Projectile projectile)
        {
            projectile.GetGlobalProjectile<PhaseRoundProjectile>().Active = true;
            projectile.tileCollide = false;
            projectile.netUpdate = true; // send the phasing flag so every client draws the shot passing through walls
        }
    }

    // Marks projectiles that are phasing and removes them after too long inside blocks.
    public class PhaseRoundProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool Active;
        private int ticksInsideTiles;

        private const int MaxTicksInsideTiles = 60;

        // Plain arrows and bullets only (the same AI style); skips rockets, grenades, boomerangs, minions and proc projectiles.
        public static bool CanPhase(Projectile p)
        {
            if (!p.friendly || p.hostile || p.minion || p.sentry || !p.tileCollide)
                return false;

            if (!p.CountsAsClass(DamageClass.Ranged))
                return false;

            if (p.aiStyle != ProjAIStyleID.Arrow)
                return false;

            return !p.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage;
        }

        // A weapon that fires bullets or arrows: anything that uses that ammo (vanilla and modded), plus modded ranged guns and bows
        // that bring their own shots, found by name. Launchers and cannons are left out on purpose.
        public static bool IsGunOrBow(Item item)
        {
            if (item == null || item.damage <= 0 || !item.CountsAsClass(DamageClass.Ranged))
                return false;

            if (item.useAmmo == AmmoID.Rocket || item.useAmmo == AmmoID.Gel || item.useAmmo == AmmoID.Dart)
                return false;

            if (item.useAmmo == AmmoID.Bullet || item.useAmmo == AmmoID.Arrow)
                return true;

            if (item.ModItem == null)
                return false;

            string name = item.ModItem.Name;
            if (NameHas(name, "Launcher") || NameHas(name, "Cannon"))
                return false;

            return NameHas(name, "Gun") || NameHas(name, "Rifle") || NameHas(name, "Pistol") || NameHas(name, "Revolver")
                || NameHas(name, "Uzi") || NameHas(name, "Musket") || NameHas(name, "Bow");
        }

        private static bool NameHas(string name, string part) =>
            name.Contains(part, System.StringComparison.OrdinalIgnoreCase);

        // For a shot that came out of a gun or bow: any AI style is fine, but skip the kinds that are not really a flying bullet
        // (explosives, boomerangs, flails, yoyos, spears, drills, held or hidden helpers, minions and proc projectiles).
        public static bool CanPhaseFromWeapon(Projectile p)
        {
            if (!p.friendly || p.hostile || p.minion || p.sentry || !p.tileCollide || p.ownerHitCheck)
                return false;

            if (p.aiStyle == ProjAIStyleID.Explosive || p.aiStyle == ProjAIStyleID.Boomerang || p.aiStyle == ProjAIStyleID.Flail
                || p.aiStyle == ProjAIStyleID.Yoyo || p.aiStyle == ProjAIStyleID.Spear || p.aiStyle == ProjAIStyleID.Drill)
                return false;

            return !p.GetGlobalProjectile<AugmentProjectileTag>().IsAugmentProcDamage;
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(Active);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            Active = bitReader.ReadBit();
            if (Active)
                projectile.tileCollide = false;
        }

        // Some mods' projectiles switch tile collision back on in their own AI, so it is held off every tick.
        public override bool PreAI(Projectile projectile)
        {
            if (Active)
                projectile.tileCollide = false;
            return true;
        }

        public override void PostAI(Projectile projectile)
        {
            if (!Active)
                return;

            projectile.tileCollide = false;

            if (!Collision.SolidCollision(projectile.position, projectile.width, projectile.height))
                return;

            ticksInsideTiles++;

            if (ticksInsideTiles > MaxTicksInsideTiles)
                projectile.Kill();
        }
    }
}
