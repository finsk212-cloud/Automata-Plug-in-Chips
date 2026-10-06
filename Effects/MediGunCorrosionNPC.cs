using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	public class MediGunCorrosionNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		public bool Corroded;

		private static Texture2D shieldTexture;

		// Clean, solid shield with darker red edge (R), yellow middle (Y),
		// and a wide, highly visible 1.5–2px crack (C = deep fissure, D = crack depth, H = crack highlight).
		private static readonly string[] ShieldMask = new string[]
		{
			"   .....CC...   ",
			"  .RRRRRCDRRR.  ",
			" .RRRRRCDRRRRR. ",
			".RRYYYCDHYYYYRR.",
			".RRYYYCDHYYYYRR.",
			".RRYYYYCDHYYYRR.",
			".RRYYYYCDHYYYRR.",
			".RRYYYCDHYYYYRR.",
			".RRYYYCDHYYYYRR.",
			" .RRYYYCDHYYRR. ",
			" .RRYYYYCDHYRR. ",
			"  .RRYYCDHYRR.  ",
			"   .RRYCDYRR.   ",
			"    .RRCDRR.    ",
			"     .RCDR.     ",
			"      ....      ",
			"       ..       ",
		};

		public override void ResetEffects(NPC npc)
		{
			Corroded = false;
		}

		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (Corroded)
			{
				if (npc.lifeRegen > 0)
					npc.lifeRegen = 0;

				if (Main.rand.NextBool(5))
				{
					Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.CrimsonTorch);
					d.noGravity = true;
					d.scale = 0.9f;
				}
			}
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (Corroded)
			{
				modifiers.ArmorPenetration += 5f;
			}
		}

		public override void Unload()
		{
			shieldTexture = null;
		}

		private static void EnsureTextures()
		{
			if (shieldTexture != null && !shieldTexture.IsDisposed)
				return;

			if (Main.graphics?.GraphicsDevice == null)
				return;

			int width = 16;
			int height = 17;
			shieldTexture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
			Color[] data = new Color[width * height];

			Color darkOutline = new Color(30, 5, 8, 255);
			Color darkerRedEdge = new Color(160, 22, 30, 255);
			Color yellowMiddle = new Color(255, 215, 30, 255);
			Color darkCrack = new Color(18, 4, 6, 255);
			Color crackDepth = new Color(42, 8, 12, 255);
			Color crackLight = new Color(255, 255, 235, 240);

			for (int y = 0; y < height; y++)
			{
				string row = ShieldMask[y];
				for (int x = 0; x < width; x++)
				{
					char c = row[x];
					Color col = Color.Transparent;
					if (c == '.') col = darkOutline;
					else if (c == 'R') col = darkerRedEdge;
					else if (c == 'Y') col = yellowMiddle;
					else if (c == 'C') col = darkCrack;
					else if (c == 'D') col = crackDepth;
					else if (c == 'H') col = crackLight;

					data[y * width + x] = col;
				}
			}

			shieldTexture.SetData(data);
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!npc.active || npc.life <= 0)
				return;

			bool hasCorrosion = Corroded || npc.HasBuff<Buffs.MediGunCorrosionBuff>();
			bool hasIchor = npc.ichor || npc.HasBuff(BuffID.Ichor);
			bool hasBetsy = npc.betsysCurse || npc.HasBuff(BuffID.BetsysCurse);

			if (!hasCorrosion && !hasIchor && !hasBetsy)
				return;

			EnsureTextures();

			if (shieldTexture == null)
				return;

			// Subtle gentle floating bob
			float bob = (float)Math.Sin((Main.GameUpdateCount + npc.whoAmI * 13) * 0.08f) * 2f;
			Vector2 worldPos = new Vector2(npc.Center.X, npc.position.Y - 20f + bob);
			Vector2 drawPos = worldPos - screenPos;
			drawPos = new Vector2((float)Math.Round(drawPos.X), (float)Math.Round(drawPos.Y));

			Vector2 origin = new Vector2(shieldTexture.Width * 0.5f, shieldTexture.Height * 0.5f);

			// Check remaining buff time for subtle blink when expiring in the last 1.5s
			int remainingTicks = int.MaxValue;
			int bIndex = -1;
			if (hasBetsy) bIndex = npc.FindBuffIndex(BuffID.BetsysCurse);
			else if (hasIchor) bIndex = npc.FindBuffIndex(BuffID.Ichor);
			else if (hasCorrosion) bIndex = npc.FindBuffIndex(ModContent.BuffType<Buffs.MediGunCorrosionBuff>());

			if (bIndex >= 0 && bIndex < npc.buffTime.Length)
			{
				remainingTicks = npc.buffTime[bIndex];
			}

			float alpha = 1f;
			if (remainingTicks <= 90)
			{
				alpha = 0.45f + 0.55f * (float)Math.Sin(Main.GameUpdateCount * 0.35f);
			}

			float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.5f + 0.5f;

			// 1. Deep red glow / aura behind the shield
			Color auraColor = new Color(175, 20, 30) * (0.50f + pulse * 0.15f) * alpha;
			float glowDist = 1.6f + pulse * 0.4f;
			spriteBatch.Draw(shieldTexture, drawPos + new Vector2(-glowDist, 0f), null, auraColor, 0f, origin, 1f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shieldTexture, drawPos + new Vector2(glowDist, 0f), null, auraColor, 0f, origin, 1f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shieldTexture, drawPos + new Vector2(0f, -glowDist), null, auraColor, 0f, origin, 1f, SpriteEffects.None, 0f);
			spriteBatch.Draw(shieldTexture, drawPos + new Vector2(0f, glowDist), null, auraColor, 0f, origin, 1f, SpriteEffects.None, 0f);

			// 2. Subtle drop shadow for crisp definition
			spriteBatch.Draw(shieldTexture, drawPos + new Vector2(0f, 1f), null, Color.Black * (0.7f * alpha), 0f, origin, 1f, SpriteEffects.None, 0f);

			// 3. Sharp foreground shield (darker red edge, yellow middle, wide 1.5-2px crack)
			spriteBatch.Draw(shieldTexture, drawPos, null, Color.White * alpha, 0f, origin, 1f, SpriteEffects.None, 0f);
		}
	}
}
