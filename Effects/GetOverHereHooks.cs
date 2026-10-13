using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
    // The Get over here! visual, on every client: a grappling hook flies from the owner to the
    // teammate, latches on, and the chain stays taut between them while the teammate is hauled
    // in (the hauling itself is real movement, see AugmentPlayer.PreUpdateMovement).
    public class GetOverHereHooks : ModSystem
    {
        // Ticks the hook stays on the teammate before we check whether they have arrived.
        private const int LatchTicks = 6;
        // Close enough to the owner to count as landed.
        private const float ArriveDistance = 48f;

        private class Hook
        {
            public int Owner;
            public int Target;
            public int Age;
            public int FlightTicks;
        }

        private static readonly List<Hook> hooks = new List<Hook>();
        private static Asset<Texture2D> chainAsset;
        private static bool chainTried;

        public static void Add(int owner, int target)
        {
            if (Main.dedServ)
                return;

            Player from = Main.player[owner];
            Player to = Main.player[target];
            hooks.Add(new Hook
            {
                Owner = owner,
                Target = target,
                FlightTicks = GetOverHereAugment.ThrowTicks(Vector2.Distance(from.Center, to.Center)),
            });
        }

        public override void Unload()
        {
            hooks.Clear();
            chainAsset = null;
            chainTried = false;
        }

        public override void OnWorldUnload()
        {
            hooks.Clear();
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || hooks.Count == 0)
                return;

            for (int i = hooks.Count - 1; i >= 0; i--)
            {
                Hook hook = hooks[i];
                Player owner = Main.player[hook.Owner];
                Player target = Main.player[hook.Target];
                if (!owner.active || !target.active || owner.dead || target.dead)
                {
                    hooks.RemoveAt(i);
                    continue;
                }

                hook.Age++;
                bool latched = hook.Age >= hook.FlightTicks;

                if (!latched)
                {
                    // Sparks trailing behind the flying hook.
                    Dust spark = Dust.NewDustPerfect(TipPosition(hook, owner, target), DustID.Silver, Main.rand.NextVector2Circular(1f, 1f), 0, default, 1f);
                    spark.noGravity = true;
                    continue;
                }

                if (hook.Age == hook.FlightTicks)
                    SpawnLatchBurst(target.Center);

                // Dust streaming off the teammate, against the direction they are travelling.
                Vector2 toOwner = owner.Center - target.Center;
                if (toOwner != Vector2.Zero)
                {
                    toOwner.Normalize();
                    Dust trail = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(8f, 12f), DustID.Smoke, -toOwner * 2.5f, 120, default, 1.1f);
                    trail.noGravity = true;
                    if (Main.rand.NextBool(2))
                    {
                        Dust glint = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 14f), DustID.Silver, -toOwner * 3f, 0, default, 0.9f);
                        glint.noGravity = true;
                    }
                }

                bool arrived = hook.Age >= hook.FlightTicks + LatchTicks && Vector2.Distance(owner.Center, target.Center) <= ArriveDistance;
                bool expired = hook.Age > hook.FlightTicks + GetOverHereAugment.PullMaxTicks + 12;
                if (arrived || expired)
                {
                    SpawnLandingPuff(target.Center);
                    hooks.RemoveAt(i);
                }
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.dedServ)
                return;

            // Bottom of the interface stack: world-space, so the hook sits above the players but
            // under every piece of UI.
            layers.Insert(0, new LegacyGameInterfaceLayer(
                "Augments: Get Over Here",
                delegate
                {
                    DrawHooks(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }

        // Where the hook is right now: flying from the owner toward the teammate, then stuck on them.
        private static Vector2 TipPosition(Hook hook, Player owner, Player target)
        {
            float t = hook.FlightTicks <= 0 ? 1f : Math.Min(1f, hook.Age / (float)hook.FlightTicks);
            return Vector2.Lerp(owner.Center, target.Center, t);
        }

        private static void DrawHooks(SpriteBatch spriteBatch)
        {
            if (hooks.Count == 0)
                return;

            Main.instance.LoadProjectile(ProjectileID.Hook);
            Texture2D hookTexture = TextureAssets.Projectile[ProjectileID.Hook].Value;
            Texture2D chainTexture = GetChainTexture();

            foreach (Hook hook in hooks)
            {
                Player owner = Main.player[hook.Owner];
                Player target = Main.player[hook.Target];
                if (!owner.active || !target.active)
                    continue;

                Vector2 start = owner.Center;
                Vector2 tip = TipPosition(hook, owner, target);
                Vector2 direction = tip - start;
                float length = direction.Length();
                if (length < 1f)
                    continue;

                float rotation = direction.ToRotation() + MathHelper.PiOver2;
                DrawChain(spriteBatch, chainTexture, start, tip, rotation);

                Color light = Lighting.GetColor((int)(tip.X / 16f), (int)(tip.Y / 16f));
                spriteBatch.Draw(hookTexture, tip - Main.screenPosition, null, light, rotation, hookTexture.Size() / 2f, 1f, SpriteEffects.None, 0f);
            }
        }

        private static void DrawChain(SpriteBatch spriteBatch, Texture2D chain, Vector2 start, Vector2 end, float rotation)
        {
            Vector2 direction = end - start;
            float length = direction.Length();
            direction /= length;

            // Vanilla chain sprite when it loads; otherwise a plain grey line so the pull still reads.
            if (chain == null)
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1), new Color(190, 190, 200),
                    direction.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0f);
                return;
            }

            float step = Math.Max(4f, chain.Height);
            int segments = Math.Min(220, (int)(length / step));
            for (int i = 0; i <= segments; i++)
            {
                Vector2 position = start + direction * (i * step + step / 2f);
                if (Vector2.Distance(start, position) > length)
                    break;

                Color light = Lighting.GetColor((int)(position.X / 16f), (int)(position.Y / 16f));
                spriteBatch.Draw(chain, position - Main.screenPosition, null, light, rotation, chain.Size() / 2f, 1f, SpriteEffects.None, 0f);
            }
        }

        private static Texture2D GetChainTexture()
        {
            if (!chainTried)
            {
                chainTried = true;
                try
                {
                    chainAsset = ModContent.Request<Texture2D>("Terraria/Images/Chain", AssetRequestMode.ImmediateLoad);
                }
                catch (Exception)
                {
                    chainAsset = null;
                }
            }

            return chainAsset != null && chainAsset.IsLoaded ? chainAsset.Value : null;
        }

        private static void SpawnLatchBurst(Vector2 position)
        {
            for (int i = 0; i < 14; i++)
            {
                Dust spark = Dust.NewDustPerfect(position, DustID.Silver, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.2f);
                spark.noGravity = true;
            }
        }

        private static void SpawnLandingPuff(Vector2 position)
        {
            for (int i = 0; i < 14; i++)
            {
                Dust puff = Dust.NewDustPerfect(position, DustID.Smoke, Main.rand.NextVector2Circular(3f, 3f), 100, default, 1.3f);
                puff.noGravity = true;
            }
        }
    }
}
