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

			// Subtle indicator: one small spark floating above the enemy every half second.
			if (MarkTimer % 30 == 0)
			{
				Dust d = Dust.NewDustPerfect(new Vector2(npc.Center.X, npc.position.Y - 10f), DustID.YellowTorch, new Vector2(0f, -0.4f), 150, default, 0.8f);
				d.noGravity = true;
			}
		}
	}
}
