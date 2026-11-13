using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
    // Your highest-damage minion wears a gold star and deals more damage.
    //
    // The tally lives on the owner's game (their hits are the only ones it sees). Whenever the star changes hands, the owner
    // tells everyone which minion wears it (PluginEffects.EmployeeCrowned), and every game draws the star itself, so other
    // players see it too. The announcement is repeated every few seconds so someone who joins late gets it as well.
    public class EmployeeOfTheMonthAugment : Augment
    {
        // Tuning
        public const float DamageBonus = 0.40f;
        private const int WindowTicks = 600;          // damage tallies halve every 10 seconds, so "this month" means "lately"
        private const float TakeoverMargin = 1.15f;   // a challenger must beat the star holder by 15% to take it
        private const int PruneTicks = 60;
        private const int ResendTicks = 300;

        public override string Id => "employee_of_the_month";
        public override string DisplayName => "Employee of the Month";
        public override string Description =>
            $"Your highest-damage minion wears a gold star and deals {AugmentText.BonusDamage("+40% damage")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Every minion is graded on the damage it dealt lately, shots it fired included, before the bonus. " +
            "A performance review happens every 10 seconds: everyone's total is cut in half, so the star can change hands. " +
            "A challenger needs 15% more than the star holder to take it. Sentries, whips and the ghosts raised by Necromancer's Court don't compete. " +
            "A minion made of linked segments, like the Stardust Dragon, counts as one minion: the damage of all its segments adds up, " +
            "the star sits on its head, and every segment gets the bonus. " +
            "The star holder leaves, the star goes to the next best.)");

        public override AugmentRarity Rarity => AugmentRarity.Rare;
        public override AugmentClass Class => AugmentClass.Summon;
        public override Texture2D Icon => TextureAssets.Item[ItemID.GoldCrown].Value;

        // --- What every game knows: which minion of each player wears the star (-1 = none) ---

        private static readonly int[] crowned = CreateCrowned();

        private static int[] CreateCrowned()
        {
            var array = new int[Main.maxPlayers + 1];
            System.Array.Fill(array, -1);
            return array;
        }

        public static void SetCrowned(int playerIndex, int minionIdentity)
        {
            if (playerIndex >= 0 && playerIndex < crowned.Length)
                crowned[playerIndex] = minionIdentity;
        }

        public static int GetCrowned(int playerIndex) =>
            playerIndex >= 0 && playerIndex < crowned.Length ? crowned[playerIndex] : -1;

        // --- What only the owner's game tracks ---

        private static readonly Dictionary<int, float> tally = new();
        private static int top = -1;
        private static readonly PerHitGuard guard = new();

        // Which minion a hit counts for: the minion itself, or the minion that fired the projectile.
        // A minion made of several linked segments (a worm or dragon) counts as ONE minion, identified by its head,
        // so every segment's damage adds up under the head, the star goes on the head, and a hit from ANY segment gets the bonus.
        private static int MinionIdentity(Projectile proj)
        {
            AugmentProjectileTag tag = proj.GetGlobalProjectile<AugmentProjectileTag>();
            if (tag.NecromancersCourtGhost)
                return -1;

            int id = proj.minion ? proj.identity : tag.SourceMinionProjectileIdentity;
            return id < 0 ? id : HeadOf(id);
        }

        // --- Segmented minions: which head each segment belongs to ---
        //
        // Rebuilt at most once per game tick (and only when a hit is processed), from the owner's active minions:
        //   * Stardust Dragon: every owner has one dragon (extra summons just lengthen it), so all of its body and tail
        //     segments belong to the one head (StardustDragon1).
        //   * Other segmented minions (mods): a minion that takes no minion slots and follows another minion of the
        //     same owner through ai[0] is treated as a segment, and its head is the first minion up the chain that does
        //     take slots. A chain that never reaches such a head is ignored, so ordinary minions are never merged by accident.
        private static readonly Dictionary<int, int> headOf = new();
        private static uint headMapTick = uint.MaxValue;

        private static int HeadOf(int minionIdentity)
        {
            if (headMapTick != Main.GameUpdateCount)
            {
                headMapTick = Main.GameUpdateCount;
                RebuildHeadMap();
            }

            return headOf.TryGetValue(minionIdentity, out int head) ? head : minionIdentity;
        }

        private static void RebuildHeadMap()
        {
            headOf.Clear();
            int owner = Main.myPlayer;

            int dragonHead = -1;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner && p.type == ProjectileID.StardustDragon1)
                {
                    dragonHead = p.identity;
                    break;
                }
            }

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner || !p.minion)
                    continue;

                if (p.type >= ProjectileID.StardustDragon2 && p.type <= ProjectileID.StardustDragon4)
                {
                    if (dragonHead >= 0)
                        headOf[p.identity] = dragonHead;
                    continue;
                }

                if (p.minionSlots <= 0f)
                {
                    int head = FollowToHead(p);
                    if (head >= 0)
                        headOf[p.identity] = head;
                }
            }
        }

        // Follows a zero-slot minion up its ai[0] links to the minion that takes slots. Returns -1 if the chain
        // does not lead to one (then the minion is left alone and counts as itself).
        private static int FollowToHead(Projectile start)
        {
            Projectile current = start;
            for (int step = 0; step < 60; step++)
            {
                int index = (int)current.ai[0];
                if (index < 0 || index >= Main.maxProjectiles)
                    return -1;

                Projectile parent = Main.projectile[index];
                if (!parent.active || parent.owner != current.owner || !parent.minion || parent.whoAmI == current.whoAmI)
                    return -1;

                if (parent.minionSlots > 0f)
                    return parent.identity;

                current = parent;
            }

            return -1;
        }

        public override void ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (player.whoAmI != Main.myPlayer || top < 0)
                return;

            if (MinionIdentity(proj) == top)
                modifiers.FinalDamage *= 1f + DamageBonus;
        }

        public override void OnHitNPCWithProj(Player player, Projectile proj, NPC target, NPC.HitInfo hit, AugmentHitSource source, float effectiveness)
        {
            if (player.whoAmI != Main.myPlayer || source == AugmentHitSource.AugmentProc)
                return;

            int id = MinionIdentity(proj);
            if (id < 0)
                return;

            // The same hit can reach this hook twice (Twin Strike on a crit): count it once.
            if (!guard.IsFirst(proj.whoAmI, target.whoAmI))
                return;

            // Grade on the damage before the bonus, or the star holder would simply run away with the title.
            float credited = id == top ? hit.Damage / (1f + DamageBonus) : hit.Damage;
            tally[id] = (tally.TryGetValue(id, out float soFar) ? soFar : 0f) + credited;

            UpdateStar(player);
        }

        // Decides who wears the star, and tells everyone if that changed.
        private static void UpdateStar(Player player)
        {
            int previous = top;

            if (tally.Count == 0)
            {
                top = -1;
            }
            else
            {
                int leader = tally.OrderByDescending(pair => pair.Value).First().Key;
                if (top < 0 || !tally.ContainsKey(top))
                    top = leader;
                else if (leader != top && tally[leader] > tally[top] * TakeoverMargin)
                    top = leader;
            }

            if (top != previous)
                Announce(player);
        }

        private static void Announce(Player player)
        {
            PluginEffects.Show(PluginEffectId.EmployeeCrowned, player, top + 1);
        }

        public override void OnAcquire(Player player)
        {
            Reset(player);
        }

        public override void OnKill(Player player)
        {
            Reset(player);
        }

        private static void Reset(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            tally.Clear();
            top = -1;
            SetCrowned(player.whoAmI, -1);
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            // Performance review: everyone's total halves, and tiny totals are forgotten.
            if (Main.GameUpdateCount % WindowTicks == 0)
            {
                foreach (int key in tally.Keys.ToList())
                {
                    tally[key] *= 0.5f;
                    if (tally[key] < 1f)
                        tally.Remove(key);
                }

                UpdateStar(player);
            }

            // Minions that are gone no longer compete.
            if (Main.GameUpdateCount % PruneTicks == 0 && tally.Count > 0)
            {
                var alive = new HashSet<int>();
                foreach (Projectile p in Main.projectile)
                {
                    if (p.active && p.minion && p.owner == player.whoAmI)
                        alive.Add(p.identity);
                }

                foreach (int key in tally.Keys.ToList())
                {
                    if (!alive.Contains(key))
                        tally.Remove(key);
                }

                UpdateStar(player);
            }

            // Say it again now and then, for anyone who joined after the last change.
            if (Main.GameUpdateCount % ResendTicks == 0 && top >= 0)
                Announce(player);
        }
    }

    // Draws the gold star above whichever of a player's minions currently wears it, on every game.
    public class EmployeeStarDrawer : GlobalProjectile
    {
        public override void PostDraw(Projectile projectile, Color lightColor)
        {
            if (!projectile.minion || projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            if (EmployeeOfTheMonthAugment.GetCrowned(projectile.owner) != projectile.identity)
                return;

            Player owner = Main.player[projectile.owner];
            if (!owner.active || !owner.GetModPlayer<AugmentPlayer>().HasAugment("employee_of_the_month"))
                return;

            int tick = (int)Main.GameUpdateCount;
            float bob = (float)System.Math.Sin(tick * 0.1f) * 2.5f;
            Vector2 at = projectile.Center + new Vector2(0f, -projectile.height / 2f - 18f + bob);
            AutomataFx.PixelStar(at, 2.2f, tick, new Color(255, 215, 80, 0));
        }
    }
}
