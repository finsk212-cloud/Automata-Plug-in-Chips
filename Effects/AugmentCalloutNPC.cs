using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace Augments
{
	// Mark applied by the Callout chip. Teammates of the marking player deal bonus damage to a marked enemy.
	public class AugmentCalloutNPC : GlobalNPC
	{
		public const int DurationTicks = 300;
		public const float TeammateDamageBonus = 0.10f;

		public override bool InstancePerEntity => true;

		public int MarkTimer;
		public int MarkedByPlayer = -1;

		public bool IsMarked => MarkTimer > 0;

		public void ApplyMark(int playerIndex, int durationTicks = DurationTicks)
		{
			MarkTimer = Math.Max(MarkTimer, durationTicks);
			MarkedByPlayer = playerIndex;
		}

		public override void PostAI(NPC npc)
		{
			if (MarkTimer <= 0)
			{
				MarkedByPlayer = -1;
				return;
			}

			MarkTimer--;
		}

		// Small gold diamond bobbing above the marked enemy. It stays on for the whole mark and blinks in the last second.
		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!IsMarked || npc.friendly || npc.townNPC || npc.dontTakeDamage)
				return;

			if (MarkTimer < 60 && (MarkTimer / 6) % 2 == 0)
				return;

			float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 2f;
			Vector2 pos = new Vector2(npc.Center.X, npc.position.Y) - screenPos + new Vector2(0f, -16f + bob);
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle one = new Rectangle(0, 0, 1, 1);
			Vector2 origin = new Vector2(0.5f, 0.5f);

			spriteBatch.Draw(pixel, pos, one, new Color(20, 20, 20) * 0.6f, MathHelper.PiOver4, origin, 11f, SpriteEffects.None, 0f);
			spriteBatch.Draw(pixel, pos, one, new Color(255, 205, 70) * 0.85f, MathHelper.PiOver4, origin, 8f, SpriteEffects.None, 0f);
		}
	}
}
