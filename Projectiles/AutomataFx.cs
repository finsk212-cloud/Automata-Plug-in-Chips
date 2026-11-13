using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace Augments
{
    // A second drawing toolkit in the same YoRHa spirit as DangerCloseVisuals, but built from different shapes so the
    // effects don't all look alike: dashed "hacking minigame" rings, solid squares and cubes, 8-bit pixel art and
    // flickering holograms, instead of hexagons, brackets and beams. Colours are additive (alpha 0).
    internal static class AutomataFx
    {
        public static readonly Color White = new Color(235, 245, 255, 0);
        public static readonly Color Cyan = new Color(110, 215, 255, 0);
        public static readonly Color Pink = new Color(255, 95, 175, 0);
        public static readonly Color Green = new Color(90, 255, 140, 0);
        public static readonly Color Alarm = new Color(255, 60, 60, 0);

        // Text that starts at a point and runs to the right (the other text helper centres on the point). For terminal lines.
        public static void TextLeft(string text, Vector2 worldPosition, Color color, float scale)
        {
            Utils.DrawBorderString(Main.spriteBatch, text, worldPosition - Main.screenPosition, color, scale, 0f, 0.5f);
        }

        // A little gold 8-bit star, built from squares, with a gentle twinkle.
        public static void PixelStar(Vector2 center, float pixel, int tick, Color color)
        {
            string[] rows =
            {
                "...X...",
                "...X...",
                "XXXXXXX",
                ".XXXXX.",
                "..XXX..",
                ".XX.XX.",
                "XX...XX",
            };

            float twinkle = 0.8f + 0.2f * (float)System.Math.Sin(tick * 0.2f);
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] == 'X')
                        Rect(center + new Vector2((c - 3) * pixel, (r - 3) * pixel), pixel + 0.6f, pixel + 0.6f, 0f, color * twinkle);
                }
            }
        }

        // A little 8-bit padlock, built from squares.
        public static void PixelPadlock(Vector2 center, float pixel, Color color)
        {
            string[] rows =
            {
                "..XXX..",
                ".X...X.",
                ".X...X.",
                "XXXXXXX",
                "XXXXXXX",
                "XXX.XXX",
                "XXXXXXX",
                "XXXXXXX",
            };

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] == 'X')
                        Rect(center + new Vector2((c - 3) * pixel, (r - 3.5f) * pixel), pixel + 0.6f, pixel + 0.6f, 0f, color);
                }
            }
        }

        // A filled rectangle (or square) of any size, centred on a point and optionally rotated.
        public static void Rect(Vector2 center, float width, float height, float rotation, Color color)
        {
            Main.EntitySpriteDraw(
                TextureAssets.MagicPixel.Value,
                center - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                rotation,
                new Vector2(0.5f, 0.5f),
                new Vector2(width, height),
                SpriteEffects.None);
        }

        // Cheap repeatable "random" number in 0..1 for a given seed, index and salt, so an effect can scatter pieces
        // the same way on every client and every frame without storing anything.
        public static float Rand(int seed, int index, int salt)
        {
            double v = Math.Sin(seed * 12.9898 + index * 78.233 + salt * 37.719) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        public static void Arc(Vector2 center, float radius, float startAngle, float sweep, float thickness, Color color)
        {
            int segments = Math.Max(2, (int)(sweep * radius / 9f));
            Vector2 previous = center + startAngle.ToRotationVector2() * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = center + (startAngle + sweep * i / segments).ToRotationVector2() * radius;
                DangerCloseVisuals.Line(previous, next, thickness, color);
                previous = next;
            }
        }

        // A ring broken into separate dashes, like the target rings in the hacking minigame. 'fill' is how much of
        // each slice is drawn (0..1).
        public static void DashedRing(Vector2 center, float radius, int dashes, float fill, float rotation, float thickness, Color color)
        {
            float slice = MathHelper.TwoPi / dashes;
            for (int i = 0; i < dashes; i++)
                Arc(center, radius, rotation + slice * i, slice * fill, thickness, color);
        }

        // A tiny 8-bit heart, built from squares.
        public static void PixelHeart(Vector2 center, float pixel, Color color)
        {
            string[] rows =
            {
                ".XX.XX.",
                "XXXXXXX",
                "XXXXXXX",
                ".XXXXX.",
                "..XXX..",
                "...X...",
            };

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] == 'X')
                        Rect(center + new Vector2((c - 3) * pixel, (r - 2.5f) * pixel), pixel + 0.6f, pixel + 0.6f, 0f, color);
                }
            }
        }

        // One half of the pixel heart, for a heart that breaks in two. 'apart' pushes the halves away from each
        // other and 'drop' lets them fall.
        public static void PixelHeartHalf(Vector2 center, float pixel, Color color, bool leftHalf, float apart, float drop)
        {
            string[] rows =
            {
                ".XX.XX.",
                "XXXXXXX",
                "XXXXXXX",
                ".XXXXX.",
                "..XXX..",
                "...X...",
            };

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] != 'X' || (leftHalf ? c > 3 : c < 3))
                        continue;

                    Vector2 at = center + new Vector2((c - 3) * pixel + (leftHalf ? -apart : apart), (r - 2.5f) * pixel + drop);
                    Rect(at, pixel + 0.6f, pixel + 0.6f, 0f, color);
                }
            }
        }

        // A little 8-bit sad face: two eyes, a downturned mouth and a tear.
        public static void PixelSadFace(Vector2 center, float pixel, int tick, Color color, Color tearColor)
        {
            string[] rows =
            {
                ".X...X.",
                ".X...X.",
                ".......",
                "..XXX..",
                ".X...X.",
            };

            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] == 'X')
                        Rect(center + new Vector2((c - 3) * pixel, (r - 2f) * pixel), pixel + 0.6f, pixel + 0.6f, 0f, color);
                }
            }

            // The tear rolls down from the left eye and starts again.
            float fall = (tick * 0.12f) % 1f;
            Rect(center + new Vector2(-2f * pixel, (1f + fall * 3.2f) * pixel), pixel * 0.8f, pixel * 1.2f, 0f, tearColor * (1f - fall * 0.6f));
        }

        // A standing figure drawn as horizontal slices that shift sideways and drop out now and then, like a
        // hologram projected by a Pod. Roughly 44 pixels tall at scale 1.
        public static void Hologram(Vector2 center, float scale, int tick, float jitter, Color color)
        {
            for (int row = 0; row < 15; row++)
            {
                // Some slices flicker out.
                if ((tick / 2 + row * 7) % 9 == 0)
                    continue;

                float y = -21f + row * 3f;
                float shift = (float)Math.Sin(tick * 0.35f + row * 1.7f) * jitter;
                Vector2 at = center + new Vector2(shift, y) * scale;

                if (y < -12f)
                    Rect(at, 11f * scale, 3.4f * scale, 0f, color);                       // head
                else if (y < -9f)
                    Rect(at, 5f * scale, 3.4f * scale, 0f, color);                        // neck
                else if (y < 4f)
                    Rect(at, 19f * scale, 3.4f * scale, 0f, color);                       // torso and arms
                else
                {
                    Rect(at + new Vector2(-5f, 0f) * scale, 6f * scale, 3.4f * scale, 0f, color); // legs
                    Rect(at + new Vector2(5f, 0f) * scale, 6f * scale, 3.4f * scale, 0f, color);
                }
            }
        }
    }
}
