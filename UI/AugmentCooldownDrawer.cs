using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Augments.Core;

namespace Augments
{
    // High-tech status indicator row for cooldown countdowns and live status values.
    // Centered near top of screen with sleek cybernetic tiles and clean contextual tooltips.
    public static class AugmentCooldownDrawer
    {
        private const float IconSize = 36f;
        private const float Gap = 6f;
        private const float Scale = 0.68f;
        private const float TopOffset = 40f;
        private const float VerticalCenterNudge = 0.12f;
        private const float IconGraphicMaxSize = 24f;

        private readonly struct StatusIcon
        {
            public readonly Augment Augment;
            public readonly string TileText;
            public readonly Color TileTextColor;
            public readonly string Title;
            public readonly string Subtitle;
            public readonly string Description;
            public readonly Color AccentColor;
            public readonly Texture2D Icon;

            public StatusIcon(Augment augment, string tileText, Color tileTextColor, string title, string subtitle, string description, Color accentColor, Texture2D icon)
            {
                Augment = augment;
                TileText = tileText;
                TileTextColor = tileTextColor;
                Title = title;
                Subtitle = subtitle;
                Description = description;
                AccentColor = accentColor;
                Icon = icon;
            }
        }

        public static void DrawCooldowns(SpriteBatch spriteBatch)
        {
            var augmentPlayer = Main.LocalPlayer.GetModPlayer<AugmentPlayer>();
            var icons = new List<StatusIcon>();

            foreach (var augment in augmentPlayer.Owned)
            {
                if (augment.CooldownRemaining > 0)
                {
                    string text = augment.CooldownDisplayInHours
                        ? HoursText(augment.CooldownRemaining)
                        : SecondsText(augment.CooldownRemaining);

                    Color rarColor = AugmentTooltipDrawer.GetRarityColor(augment.Rarity);

                    icons.Add(new StatusIcon(
                        augment,
                        text,
                        AugmentTextColors.Cooldown,
                        augment.DisplayName,
                        $"COOLDOWN  •  {text.ToUpper()} REMAINING",
                        augment.Description,
                        rarColor,
                        augment.Icon));
                }

                if (augment.StatusValue is int value)
                {
                    string statText = $"+{value}{augment.StatusValueSuffix}";
                    Color rarColor = AugmentTooltipDrawer.GetRarityColor(augment.Rarity);

                    icons.Add(new StatusIcon(
                        augment,
                        statText,
                        augment.StatusValueColor,
                        augment.DisplayName,
                        $"ACTIVE STACKS  •  {statText}",
                        augment.Description,
                        augment.StatusValueColor,
                        augment.Icon));
                }
            }

            // Support ally protections
            if (augmentPlayer.LifelineCooldown > 0 && !augmentPlayer.HasAugment("lifeline"))
            {
                var lifeline = AugmentDatabase.GetById("lifeline");
                string text = SecondsText(augmentPlayer.LifelineCooldown);
                icons.Add(new StatusIcon(
                    lifeline,
                    text,
                    AugmentTextColors.Cooldown,
                    "Lifeline",
                    $"ALLY COOLDOWN  •  {text} REMAINING",
                    lifeline?.Description ?? "Prevents lethal damage for an ally and restores health.",
                    AugmentTextColors.Cooldown,
                    lifeline?.Icon));
            }

            if (augmentPlayer.LastRitesCooldown > 0 && !augmentPlayer.HasAugment("last_rites"))
            {
                var lastRites = AugmentDatabase.GetById("last_rites");
                string text = SecondsText(augmentPlayer.LastRitesCooldown);
                icons.Add(new StatusIcon(
                    lastRites,
                    text,
                    AugmentTextColors.Cooldown,
                    "Last Rites",
                    $"ALLY COOLDOWN  •  {text} REMAINING",
                    lastRites?.Description ?? "Grants invulnerability upon taking fatal damage.",
                    AugmentTextColors.Cooldown,
                    lastRites?.Icon));
            }

            // Ally auras
            if (Main.LocalPlayer.FindBuffIndex(ModContent.BuffType<WarCryBuff>()) >= 0)
            {
                icons.Add(new StatusIcon(
                    null,
                    "+10%",
                    AugmentTextColors.BonusDamage,
                    "Warcry",
                    "ALLY AURA  •  +10% DAMAGE",
                    "Damage empowered by a nearby Support player's battle shout.",
                    AugmentTextColors.BonusDamage,
                    null));
            }

            if (augmentPlayer.ReceivedIroncladAura)
            {
                icons.Add(new StatusIcon(
                    null,
                    "+8",
                    AugmentTextColors.Defense,
                    "Ironclad Aura",
                    "ALLY AURA  •  +8 DEFENSE",
                    "Reinforced defense provided by a nearby Support player.",
                    AugmentTextColors.Defense,
                    null));
            }

            if (augmentPlayer.ReceivedSwiftnessAura)
            {
                icons.Add(new StatusIcon(
                    null,
                    "+15%",
                    AugmentTextColors.MovementSpeed,
                    "Swiftness Aura",
                    "ALLY AURA  •  +15% MOVEMENT SPEED",
                    "Accelerated movement speed from a nearby Support player.",
                    AugmentTextColors.MovementSpeed,
                    null));
            }

            if (augmentPlayer.ReceivedManaWell)
            {
                icons.Add(new StatusIcon(
                    null,
                    "+30%",
                    AugmentTextColors.Mana,
                    "Mana Well",
                    "ALLY AURA  •  +30% MANA REGEN",
                    "Supercharged mana regeneration from a nearby Support player.",
                    AugmentTextColors.Mana,
                    null));
            }

            if (icons.Count == 0)
                return;

            var font = FontAssets.MouseText.Value;
            var scale = new Vector2(Scale);

            float totalWidth = icons.Count * IconSize + Gap * (icons.Count - 1);
            float startX = (Main.screenWidth - totalWidth) / 2f;
            float x = startX;

            StatusIcon? hoveredIcon = null;
            Point mouse = Main.MouseScreen.ToPoint();

            for (int i = 0; i < icons.Count; i++)
            {
                var boxRect = new Rectangle((int)x, (int)TopOffset, (int)IconSize, (int)IconSize);
                bool isHovered = boxRect.Contains(mouse);

                DrawTile(spriteBatch, font, scale, icons[i], boxRect, isHovered);

                if (isHovered)
                    hoveredIcon = icons[i];

                x += IconSize + Gap;
            }

            if (hoveredIcon.HasValue)
                DrawHoverTooltip(spriteBatch, font, hoveredIcon.Value);
        }

        private static void DrawTile(SpriteBatch spriteBatch, DynamicSpriteFont font, Vector2 scale, StatusIcon icon, Rectangle boxRect, bool isHovered)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // 1. Sleek high-tech dark navy background
            Color bgNavy = PanelChrome.Tone(new Color(10, 16, 28)) * 0.90f;
            spriteBatch.Draw(pixel, boxRect, bgNavy);

            // 2. Subtle ambient accent fill
            spriteBatch.Draw(pixel, boxRect, icon.AccentColor * 0.08f);

            // 3. 1px crisp outer border (brightens on hover)
            Color borderColor = isHovered ? Color.White * 0.75f : icon.AccentColor * 0.50f;
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, boxRect.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Bottom - 1, boxRect.Width, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, 1, boxRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 1, boxRect.Y, 1, boxRect.Height), borderColor);

            // 4. Subtle corner micro-accents
            Color cornerCol = isHovered ? Color.White * 0.95f : icon.AccentColor * 0.85f;
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, 2, 1), cornerCol);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 2, boxRect.Y, 2, 1), cornerCol);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Bottom - 1, 2, 1), cornerCol);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 2, boxRect.Bottom - 1, 2, 1), cornerCol);

            // 5. Centered icon graphic
            if (icon.Icon != null)
            {
                float iconScale = IconGraphicMaxSize / Math.Max(icon.Icon.Width, icon.Icon.Height);
                Vector2 iconSize = new Vector2(icon.Icon.Width, icon.Icon.Height) * iconScale;
                Vector2 iconPos = new Vector2(
                    boxRect.X + (boxRect.Width - iconSize.X) / 2f,
                    boxRect.Y + (boxRect.Height - iconSize.Y) / 2f
                );

                Color iconTint = isHovered ? Color.White : Color.White * 0.85f;
                spriteBatch.Draw(icon.Icon, iconPos, null, iconTint, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            }

            // 6. Text with shadow
            Vector2 textSize = ChatManager.GetStringSize(font, icon.TileText, scale);
            Vector2 textPos = new Vector2(
                boxRect.X + (boxRect.Width - textSize.X) / 2f,
                boxRect.Y + (boxRect.Height - textSize.Y) / 2f + textSize.Y * VerticalCenterNudge
            );

            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch, font, icon.TileText, textPos,
                icon.TileTextColor, 0f, Vector2.Zero, scale
            );
        }

        private static void DrawHoverTooltip(SpriteBatch spriteBatch, DynamicSpriteFont font, StatusIcon icon)
        {
            const float padding = 10f;
            const float maxContentWidth = 300f;
            const float lineSpacing = -2f;

            var titleScale = new Vector2(0.88f);
            var subScale = new Vector2(0.70f);
            var descScale = new Vector2(0.76f);

            var titleLines = AugmentColorText.Wrap(font, icon.Title, maxContentWidth, titleScale);
            Vector2 subSize = ChatManager.GetStringSize(font, icon.Subtitle, subScale);

            var descLines = !string.IsNullOrEmpty(icon.Description)
                ? AugmentColorText.Wrap(font, icon.Description, maxContentWidth, descScale)
                : new List<string>();

            // Calculate max content width
            float contentW = subSize.X;
            foreach (var l in titleLines)
            {
                float w = ChatManager.GetStringSize(font, l, titleScale).X;
                if (w > contentW) contentW = w;
            }
            foreach (var l in descLines)
            {
                float w = ChatManager.GetStringSize(font, l, descScale).X;
                if (w > contentW) contentW = w;
            }
            contentW = Math.Clamp(contentW, 220f, maxContentWidth);

            // Calculate total height
            float totalH = 0f;
            foreach (var l in titleLines)
                totalH += ChatManager.GetStringSize(font, l, titleScale).Y + lineSpacing;

            totalH += 2f;
            totalH += subSize.Y;

            if (descLines.Count > 0)
            {
                totalH += 6f;
                foreach (var l in descLines)
                    totalH += ChatManager.GetStringSize(font, l, descScale).Y + lineSpacing;
            }

            float boxW = contentW + padding * 2f;
            float boxH = totalH + padding * 2f;

            Vector2 boxPos = GetSmartTooltipPosition(boxW, boxH);
            var boxRect = new Rectangle((int)boxPos.X, (int)boxPos.Y, (int)boxW, (int)boxH);

            DrawTooltipChassis(spriteBatch, boxRect, icon.AccentColor);

            float curY = boxRect.Y + padding;
            float curX = boxRect.X + padding;

            // Title
            foreach (var l in titleLines)
            {
                ChatManager.DrawColorCodedStringWithShadow(
                    spriteBatch, font, l, new Vector2(curX, curY), icon.AccentColor, 0f, Vector2.Zero, titleScale
                );
                curY += ChatManager.GetStringSize(font, l, titleScale).Y + lineSpacing;
            }

            curY += 2f;

            // Clean inline subtitle text
            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch, font, icon.Subtitle, new Vector2(curX, curY), icon.TileTextColor, 0f, Vector2.Zero, subScale
            );
            curY += subSize.Y;

            // Description
            if (descLines.Count > 0)
            {
                curY += 6f;
                foreach (var l in descLines)
                {
                    ChatManager.DrawColorCodedStringWithShadow(
                        spriteBatch, font, l, new Vector2(curX, curY), new Color(225, 235, 248), 0f, Vector2.Zero, descScale
                    );
                    curY += ChatManager.GetStringSize(font, l, descScale).Y + lineSpacing;
                }
            }
        }

        private static Vector2 GetSmartTooltipPosition(float boxWidth, float boxHeight)
        {
            const float margin = 12f;
            const float cursorGap = 18f;

            float posX;
            if (Main.MouseScreen.X + cursorGap + boxWidth <= Main.screenWidth - margin)
                posX = Main.MouseScreen.X + cursorGap;
            else if (Main.MouseScreen.X - cursorGap - boxWidth >= margin)
                posX = Main.MouseScreen.X - cursorGap - boxWidth;
            else
                posX = Math.Clamp(Main.MouseScreen.X - boxWidth / 2f, margin, Main.screenWidth - boxWidth - margin);

            float posY = Main.MouseScreen.Y + cursorGap;
            if (posY + boxHeight > Main.screenHeight - margin)
                posY = Main.MouseScreen.Y - cursorGap - boxHeight;
            if (posY < margin)
                posY = margin;

            posX = Math.Clamp(posX, margin, Math.Max(margin, Main.screenWidth - boxWidth - margin));
            posY = Math.Clamp(posY, margin, Math.Max(margin, Main.screenHeight - boxHeight - margin));

            return new Vector2(posX, posY);
        }

        private static void DrawTooltipChassis(SpriteBatch spriteBatch, Rectangle boxRect, Color accentColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // 1. Ambient drop shadow (2px expansion)
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X - 2, boxRect.Y - 2, boxRect.Width + 4, boxRect.Height + 4), new Color(0, 0, 0, 160));

            // 2. High-tech cyber navy background fill (#0A101C)
            Color bgNavy = PanelChrome.Tone(new Color(10, 16, 28, 248));
            spriteBatch.Draw(pixel, boxRect, bgNavy);

            // 3. Subtle ambient accent underglow
            spriteBatch.Draw(pixel, boxRect, accentColor * 0.045f);

            // 4. 1px outer border tinted with accent
            Color outerBorder = Color.Lerp(PanelChrome.Tone(new Color(30, 41, 59)), accentColor, 0.40f) * 0.90f;
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, boxRect.Width, 1), outerBorder);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Bottom - 1, boxRect.Width, 1), outerBorder);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, 1, boxRect.Height), outerBorder);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 1, boxRect.Y, 1, boxRect.Height), outerBorder);

            // 5. 1px inner hairline accent
            Color innerHairline = Color.White * 0.06f;
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X + 1, boxRect.Y + 1, boxRect.Width - 2, 1), innerHairline);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X + 1, boxRect.Bottom - 2, boxRect.Width - 2, 1), innerHairline);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X + 1, boxRect.Y + 1, 1, boxRect.Height - 2), innerHairline);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 2, boxRect.Y + 1, 1, boxRect.Height - 2), innerHairline);

            // 6. Flush corner accent notches (4x2 / 2x4)
            Color cornerColor = accentColor * 0.92f;
            // Top-Left
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, 4, 2), cornerColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Y, 2, 4), cornerColor);
            // Top-Right
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 4, boxRect.Y, 4, 2), cornerColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 2, boxRect.Y, 2, 4), cornerColor);
            // Bottom-Left
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Bottom - 2, 4, 2), cornerColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.X, boxRect.Bottom - 4, 2, 4), cornerColor);
            // Bottom-Right
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 4, boxRect.Bottom - 2, 4, 2), cornerColor);
            spriteBatch.Draw(pixel, new Rectangle(boxRect.Right - 2, boxRect.Bottom - 4, 2, 4), cornerColor);
        }

        private static string SecondsText(int cooldownRemainingTicks)
        {
            int seconds = (int)Math.Ceiling(cooldownRemainingTicks / 60f);
            return $"{seconds}s";
        }

        private static string HoursText(int cooldownRemainingTicks)
        {
            int hours = (int)Math.Ceiling(cooldownRemainingTicks / 3600f);
            return $"{hours}";
        }
    }
}
