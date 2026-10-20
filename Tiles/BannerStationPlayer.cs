using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments.Tiles
{
    // Two jobs for the local player: carry an item's banners into the station it was just used to place,
    // and give the player the banner buffs of every station in range.
    public class BannerStationPlayer : ModPlayer
    {
        private const int ClaimWindowTicks = 180;
        private const int ClaimRetryTicks = 30;
        private const float ClaimReachPixels = 16f * 25f;

        // Written by the held station item every frame it is in hand.
        private static bool heldThisFrame;
        private static int heldTier;
        private static string heldData = "";

        private bool wasHolding;
        private int claimTimer;
        private int claimRetry;
        private int claimTier;
        private string claimData = "";

        public static void RememberHeld(int tier, string data)
        {
            heldThisFrame = true;
            heldTier = tier;
            heldData = data ?? "";
        }

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            ApplyBanners();
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            ApplyBanners();
            TrackPlacement();
        }

        // The item stops being held the frame it is consumed by placing, which opens a short window to find the new station.
        private void TrackPlacement()
        {
            bool holding = heldThisFrame;
            heldThisFrame = false;

            if (holding)
            {
                claimTier = heldTier;
                claimData = heldData;
            }
            else if (wasHolding)
            {
                claimTimer = ClaimWindowTicks;
                claimRetry = 0;
            }
            wasHolding = holding;

            if (claimTimer <= 0)
                return;

            claimTimer--;
            if (claimRetry > 0)
            {
                claimRetry--;
                return;
            }

            BannerStationEntity fresh = FindUnclaimed();
            if (fresh == null)
                return;

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                fresh.ApplyInit(claimTier, BannerStationData.Decode(claimData));
                claimTimer = 0;
            }
            else
            {
                BannerStationNet.SendInit(fresh.Position.X, fresh.Position.Y, claimTier, claimData);
                claimRetry = ClaimRetryTicks;
            }
        }

        // The closest station near the player that nobody has filled in yet and that matches the tier of the item just used.
        private BannerStationEntity FindUnclaimed()
        {
            BannerStationEntity best = null;
            float bestDistance = ClaimReachPixels;

            try
            {
                foreach (TileEntity entity in TileEntity.ByPosition.Values)
                {
                    if (entity is not BannerStationEntity station || station.Initialized || station.GetTier() != claimTier)
                        continue;

                    float distance = Vector2.Distance(Player.Center, station.Center);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = station;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // The entity table changed while a network packet was being applied; the next frame tries again.
            }

            return best;
        }

        // Switches on the real banner buff for every banner held by a station whose range covers the player.
        private void ApplyBanners()
        {
            bool[] active = Main.SceneMetrics.NPCBannerBuff;
            if (active == null)
                return;

            try
            {
                foreach (TileEntity entity in TileEntity.ByPosition.Values)
                {
                    if (entity is not BannerStationEntity station || !station.Initialized || station.Banners.Count == 0 || !station.InRange(Player))
                        continue;

                    Dictionary<int, int> idByItem = BannerStationData.BannerIdByItem;
                    foreach (int itemType in station.Banners)
                    {
                        if (idByItem.TryGetValue(itemType, out int bannerId) && bannerId > 0 && bannerId < active.Length)
                        {
                            active[bannerId] = true;
                            Main.SceneMetrics.hasBanner = true;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // A station was synced mid-frame; the buffs are re-applied next frame.
            }
        }
    }

    public class BannerStationSystem : ModSystem
    {
        public override void AddRecipeGroups()
        {
            var items = new List<int>(BannerStationData.BannerIdByItem.Keys);
            items.Sort();
            if (items.Count == 0)
                return;

            RecipeGroup.RegisterGroup(BannerStationData.AnyBannerGroup, new RecipeGroup(() => "Any Banner", items.ToArray()));
        }

        public override void Unload() => BannerStationData.Reset();
    }
}
