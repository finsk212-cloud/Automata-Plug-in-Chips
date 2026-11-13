using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
    // Holding fire with a ranged weapon builds heat, and heat is attack speed. At full heat the gun jams for 2 seconds.
    //
    // The heat belongs to the shooter's game: that is where the weapon is used, so that is where the speed bonus and the jam
    // are enforced. Everyone else only needs to SEE the bar, so the shooter sends the heat level now and then through the
    // plugin-effect relay (PluginEffectId.GunHeat) and every game draws the bar from the last level it heard.
    public class GunGoBrrrAugment : Augment
    {
        // Tuning
        public const float MaxAttackSpeedBonus = 0.60f;
        private const float HeatPerTick = 100f / (6f * 60f);   // full heat after 6 seconds of holding fire
        private const float CoolPerTick = 0.8f;                // fully cool about 2 seconds after letting go
        private const int CoolDelayTicks = 8;                  // heat doesn't start dropping the instant a shot ends
        private const int JamTicks = 120;                      // 2 seconds
        private const int SendEveryTicks = 6;
        private const int ResendTicks = 180;
        private const int HideAfterTicks = 300;                // other players' bars vanish if the shooter goes quiet

        public override string Id => "gun_go_brrr";
        public override string DisplayName => "Gun Go Brrr";
        public override string Description =>
            $"The longer you hold fire, the more heat builds, up to {AugmentText.AttackSpeed("+60% attack speed")}.\n" +
            $"At max heat your gun jams for {AugmentText.Duration("2 seconds")}.";

        public override string GetDetailedDescription(Player player) =>
            Description + "\n" +
            AugmentText.Note("(Heat fills over 6 seconds of holding fire with a ranged weapon, and the bonus follows the heat: half full is +30%. " +
            "Letting go cools the gun down in about 2 seconds. While it is jammed you can't use ranged weapons, and the bar drains back to empty. " +
            "Other players can see your heat bar above your head. Knowing when to stop is the whole game.)");

        public override AugmentRarity Rarity => AugmentRarity.Epic;
        public override AugmentClass Class => AugmentClass.Ranged;
        public override Texture2D Icon => TextureAssets.Item[ItemID.Minishark].Value;

        // --- The shooter's own state ---

        private static float heat;            // 0..100
        private static int jamTimer;          // ticks of jam left
        private static int idleTicks;         // ticks since the last tick of firing
        private static int lastSentCode = -1;
        private static uint lastSentTick;

        // --- What every game knows about every player's bar (filled in from the relay; the shooter's own game uses the fields above) ---

        private static readonly int[] shownPercent = new int[Main.maxPlayers + 1];
        private static readonly bool[] shownJam = new bool[Main.maxPlayers + 1];
        private static readonly uint[] shownStamp = new uint[Main.maxPlayers + 1];

        public override void OnAcquire(Player player)
        {
            ResetOwn(player);
        }

        public override void OnKill(Player player)
        {
            ResetOwn(player);
        }

        private static void ResetOwn(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            heat = 0f;
            jamTimer = 0;
            idleTicks = 0;
            SendLevel(true);
        }

        private static bool IsGun(Item item) =>
            item != null && !item.IsAir && item.damage > 0 && item.CountsAsClass(DamageClass.Ranged);

        public override void UpdateEquips(Player player)
        {
            if (player.whoAmI != Main.myPlayer || jamTimer > 0)
                return;

            player.GetAttackSpeed(DamageClass.Ranged) += MaxAttackSpeedBonus * heat / 100f;
        }

        public override bool CanUseItem(Player player, Item item)
        {
            return !(player.whoAmI == Main.myPlayer && jamTimer > 0 && IsGun(item));
        }

        public override void OnUpdate(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return;

            if (player.dead)
            {
                ResetOwn(player);
                return;
            }

            if (jamTimer > 0)
            {
                // Jammed: the bar drains back down, the gun smokes, and when it is done the gun is cool again.
                jamTimer--;
                heat = 100f * jamTimer / JamTicks;

                // Guns that keep a held projectile out while you hold the button (Vortex Beater, Phantasm...) stop too.
                if (IsGun(player.HeldItem))
                {
                    player.channel = false;
                    player.itemAnimation = 0;
                    player.itemTime = 0;
                }

                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(player.Center + new Vector2(player.direction * 14f, -4f), DustID.Smoke,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.6f, -0.6f)), 120, default, 1.1f);
                    d.noGravity = true;
                }

                if (jamTimer == 0)
                {
                    heat = 0f;
                    idleTicks = 0;
                }
            }
            else
            {
                bool firing = player.controlUseItem && (player.itemAnimation > 0 || player.itemTime > 0) && IsGun(player.HeldItem);
                if (firing)
                {
                    idleTicks = 0;
                    heat = System.Math.Min(100f, heat + HeatPerTick);
                    if (heat >= 100f)
                        Jam(player);
                }
                else
                {
                    idleTicks++;
                    if (idleTicks > CoolDelayTicks)
                        heat = System.Math.Max(0f, heat - CoolPerTick);
                }
            }

            SendLevel(false);
        }

        private static void Jam(Player player)
        {
            jamTimer = JamTicks;
            heat = 100f;
            PlayJamEffect(player);
            SendLevel(true);
        }

        private static void PlayJamEffect(Player player)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
            for (int i = 0; i < 12; i++)
            {
                Dust d = Dust.NewDustPerfect(player.Center + new Vector2(player.direction * 14f, -4f), DustID.Smoke,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.3f);
                d.noGravity = true;
            }
            CombatText.NewText(player.Hitbox, new Color(255, 90, 70), "JAMMED");
        }

        // Tell the other players where the bar is, when it has moved enough (or every few seconds as a reminder).
        private static void SendLevel(bool force)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            int code = (int)heat + (jamTimer > 0 ? 1000 : 0);
            bool changed = code != lastSentCode && (force || System.Math.Abs((code % 1000) - (lastSentCode % 1000)) >= 4 || (code >= 1000) != (lastSentCode >= 1000));
            bool dueNow = Main.GameUpdateCount - lastSentTick >= SendEveryTicks;
            bool reminder = code != 0 && Main.GameUpdateCount - lastSentTick >= ResendTicks;
            bool cooledOff = code == 0 && lastSentCode > 0;   // always tell everyone the bar is empty again

            if (force || cooledOff || (changed && dueNow) || reminder)
            {
                lastSentCode = code;
                lastSentTick = Main.GameUpdateCount;
                AugmentNet.SendPluginEffect(PluginEffectId.GunHeat, code);
            }
        }

        // Another player's heat level arrived (code = percent, plus 1000 while jammed).
        public static void ReceiveLevel(Player shooter, int code)
        {
            int index = shooter.whoAmI;
            if (index < 0 || index >= shownPercent.Length || index == Main.myPlayer)
                return;

            bool jammed = code >= 1000;
            if (jammed && !shownJam[index])
                PlayJamEffect(shooter);

            shownPercent[index] = code % 1000;
            shownJam[index] = jammed;
            shownStamp[index] = Main.GameUpdateCount;
        }

        // --- Drawing: a segmented heat bar over the head of every player who has the plugin ---

        public static void DrawAllBars()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead || !p.GetModPlayer<AugmentPlayer>().HasAugment("gun_go_brrr"))
                    continue;

                int percent;
                bool jammed;
                if (i == Main.myPlayer)
                {
                    percent = (int)heat;
                    jammed = jamTimer > 0;
                }
                else
                {
                    if (Main.GameUpdateCount - shownStamp[i] > HideAfterTicks)
                        continue;
                    percent = shownPercent[i];
                    jammed = shownJam[i];
                }

                if (percent < 3 && !jammed)
                    continue;

                DrawBar(p.Top + new Vector2(0f, -30f), percent, jammed);
            }
        }

        private static void DrawBar(Vector2 center, int percent, bool jammed)
        {
            const int cells = 12;
            const float cellWidth = 4f;
            const float cellStep = 5f;
            const float height = 6f;
            float totalWidth = cells * cellStep - 1f;
            int tick = (int)Main.GameUpdateCount;

            Vector2 left = center - new Vector2(totalWidth * 0.5f - cellWidth * 0.5f, 0f);
            float filled = percent / 100f * cells;
            bool flashOn = (tick / 6) % 2 == 0;

            for (int i = 0; i < cells; i++)
            {
                Vector2 at = left + new Vector2(i * cellStep, 0f);

                // The empty slot.
                AutomataFx.Rect(at, cellWidth, height, 0f, AutomataFx.White * 0.10f);

                float amount = MathHelper.Clamp(filled - i, 0f, 1f);
                if (amount <= 0f)
                    continue;

                float t = i / (float)(cells - 1);
                Color color = t < 0.5f
                    ? Color.Lerp(AutomataFx.Cyan, DangerCloseVisuals.Amber, t * 2f)
                    : Color.Lerp(DangerCloseVisuals.Amber, AutomataFx.Alarm, (t - 0.5f) * 2f);

                if (jammed)
                    color = flashOn ? AutomataFx.Alarm : AutomataFx.Alarm * 0.45f;

                AutomataFx.Rect(at, cellWidth, height, 0f, color * amount);
            }

            // Frame corners.
            Color frame = DangerCloseVisuals.Beige * 0.8f;
            float halfW = totalWidth * 0.5f + 2f;
            float halfH = height * 0.5f + 2f;
            DangerCloseVisuals.Bracket(center + new Vector2(-halfW, -halfH), 1f, 1f, 4f, 1f, frame);
            DangerCloseVisuals.Bracket(center + new Vector2(halfW, -halfH), -1f, 1f, 4f, 1f, frame);
            DangerCloseVisuals.Bracket(center + new Vector2(-halfW, halfH), 1f, -1f, 4f, 1f, frame);
            DangerCloseVisuals.Bracket(center + new Vector2(halfW, halfH), -1f, -1f, 4f, 1f, frame);

            // The label over the bar.
            if (jammed)
                DangerCloseVisuals.Text("JAMMED", center + new Vector2(0f, -14f), flashOn ? DangerCloseVisuals.Red : DangerCloseVisuals.Red * 0.5f, 0.7f);
            else
                DangerCloseVisuals.Text("+" + (int)(MaxAttackSpeedBonus * percent) + "%", center + new Vector2(0f, -14f), AutomataFx.Cyan, 0.6f);
        }
    }

    public class GunGoBrrrSystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer(
                "Augments: Gun Go Brrr",
                delegate
                {
                    GunGoBrrrAugment.DrawAllBars();
                    return true;
                },
                InterfaceScaleType.Game));
        }
    }
}
