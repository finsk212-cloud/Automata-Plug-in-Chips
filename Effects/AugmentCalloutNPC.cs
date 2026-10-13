using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
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

			if (npc.friendly || npc.townNPC)
				return;

			if (MarkTimer % 20 == 0)
			{
				Vector2 top = new Vector2(npc.Center.X, npc.position.Y - 14f);
				for (int i = 0; i < 3; i++)
				{
					Dust d = Dust.NewDustPerfect(top + new Vector2((i - 1) * 6f, 0f), DustID.GoldCoin, Vector2.Zero, 0, default, 1f);
					d.noGravity = true;
				}
			}
		}
	}
}
