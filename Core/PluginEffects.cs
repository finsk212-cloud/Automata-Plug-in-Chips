using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Augments
{
    // Sounds and visuals that belong to a player but have no projectile to ride on, so other players would
    // never see or hear them. The caster plays the effect on their own game right away, and the same effect is
    // relayed through the server (see AugmentNet.SendPluginEffect) so every other player's game plays it too.
    //
    // Effects that happen AT a projectile (Danger Close's marker, the Rod of Discount's afterimage, the portal,
    // Star Power's aura and Run as Administrator's aura) do not need this: they play on the projectile's own
    // first tick, on every game.
    internal enum PluginEffectId : byte
    {
        PayToWinPaid = 1,       // param: mana paid
        LeftoversPop = 2,       // param: slot of the enemy that received the leftover damage
        PogoBounce = 3,         // param: current kill streak (0 = just a bounce)
        EmployeeCrowned = 4,    // param: identity of the crowned minion + 1 (0 = nobody)
        GunHeat = 5             // param: heat percent (0-100), plus 1000 while the gun is jammed
    }

    internal static class PluginEffects
    {
        // Which plugin a player must own for the server to relay the effect. Null means "not a known effect".
        public static string RequiredAugment(PluginEffectId effect) => effect switch
        {
            PluginEffectId.PayToWinPaid => "mana_buffer",
            PluginEffectId.LeftoversPop => "leftovers",
            PluginEffectId.PogoBounce => "pogo_stick",
            PluginEffectId.EmployeeCrowned => "employee_of_the_month",
            PluginEffectId.GunHeat => "gun_go_brrr",
            _ => null
        };

        // Caster side: show it here, then tell everyone else.
        public static void Show(PluginEffectId effect, Player player, int param = 0)
        {
            PlayLocal(effect, player, param);
            AugmentNet.SendPluginEffect(effect, param);
        }

        public static void PlayLocal(PluginEffectId effect, Player player, int param)
        {
            if (Main.dedServ || player == null || !player.active)
                return;

            switch (effect)
            {
                case PluginEffectId.PayToWinPaid:
                    CombatText.NewText(player.Hitbox, new Color(96, 165, 250), "-" + param);
                    for (int i = 0; i < 8; i++)
                    {
                        Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.GemSapphire, 0f, -1.5f, 100, default, 1f);
                        d.noGravity = true;
                    }
                    break;

                case PluginEffectId.LeftoversPop:
                    if (param >= 0 && param < Main.maxNPCs)
                    {
                        NPC npc = Main.npc[param];
                        CombatText.NewText(npc.Hitbox, new Color(255, 215, 90), "LEFTOVERS");
                        for (int i = 0; i < 10; i++)
                        {
                            Dust d = Dust.NewDustPerfect(npc.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.1f);
                            d.noGravity = true;
                        }
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.5f }, npc.Center);
                    }
                    break;

                case PluginEffectId.PogoBounce:
                    for (int i = 0; i < 8; i++)
                    {
                        Dust d = Dust.NewDustPerfect(player.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f), DustID.WhiteTorch, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0.5f, 2f)), 100, default, 1f);
                        d.noGravity = true;
                    }
                    SoundEngine.PlaySound(SoundID.DoubleJump with { Volume = 0.8f, Pitch = System.Math.Min(1f, 0.1f * param) }, player.Bottom);
                    if (param > 0)
                        CombatText.NewText(player.Hitbox, new Color(255, 215, 90), "x" + param);
                    break;

                case PluginEffectId.EmployeeCrowned:
                    EmployeeOfTheMonthAugment.SetCrowned(player.whoAmI, param - 1);
                    break;

                case PluginEffectId.GunHeat:
                    GunGoBrrrAugment.ReceiveLevel(player, param);
                    break;
            }
        }
    }
}
