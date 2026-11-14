using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
    // Small drawing toolkit for Danger Close: thin lines and hexagon outlines in a YoRHa HUD style
    // (off-white, warning red, amber). Colours are additive (alpha 0), like the other glowing chip effects.
    internal static class DangerCloseVisuals
    {
        public static readonly Color Beige = new Color(222, 214, 186, 0);
        public static readonly Color Red = new Color(240, 60, 45, 0);
        public static readonly Color Amber = new Color(255, 170, 50, 0);

        public static void Line(Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            Main.EntitySpriteDraw(
                TextureAssets.MagicPixel.Value,
                from - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                delta.ToRotation(),
                new Vector2(0f, 0.5f),
                new Vector2(delta.Length() + 1f, thickness),
                SpriteEffects.None);
        }

        // A regular polygon outline: 6 sides is the hexagon the whole effect is built from, 4 is a diamond.
        public static void Polygon(Vector2 center, float radius, int sides, float rotation, float thickness, Color color)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = rotation + MathHelper.TwoPi * i / sides;
                float a1 = rotation + MathHelper.TwoPi * (i + 1) / sides;
                Line(center + a0.ToRotationVector2() * radius, center + a1.ToRotationVector2() * radius, thickness, color);
            }
        }

        public static void Hex(Vector2 center, float radius, float rotation, float thickness, Color color) =>
            Polygon(center, radius, 6, rotation, thickness, color);

        // An "L" bracket for one corner of a square HUD frame. dirX/dirY point from the corner into the frame.
        public static void Bracket(Vector2 corner, float dirX, float dirY, float arm, float thickness, Color color)
        {
            Line(corner, corner + new Vector2(dirX * arm, 0f), thickness, color);
            Line(corner, corner + new Vector2(0f, dirY * arm), thickness, color);
        }

        public static void Text(string text, Vector2 worldPosition, Color color, float scale)
        {
            ModText.DrawBorder(Main.spriteBatch, text, worldPosition - Main.screenPosition, color, scale, 0.5f, 0.5f);
        }
    }
}
