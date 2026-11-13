using System;
using Augments.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace Augments
{
	// The Autopilot plugin's Pod upgrade ("Pod 153"). It is not a separate drone: it is the behaviour of the Pod on the owner's shoulder.
	// Every machine (including the server) runs the same targeting from synced player data, so no packets are needed; only the server (or
	// singleplayer) applies the healing, through SupportEffects.ServerHealPlayer, which handles syncing.
	internal sealed class PodAutopilot
	{
		private const float HealRange = 700f;
		private const int HealIntervalTicks = 12; // 5 pulses per second
		private const int HealPerPulse = 1;       // +1 HP each pulse = 5 HP per second
		private const int RetargetIntervalTicks = 6;

		// Floating heal text is grouped: one "+5" per second instead of five "+1".
		private const int PulsesPerText = 5;

		// The Pod stays on its patient for at least a second, and only switches when someone else is clearly worse off, so it does not
		// flicker between two equally hurt players.
		private const int MinHoldTicks = 60;
		private const float SwitchMargin = 0.15f;

		private int targetIndex = -1;
		private int lastTargetIndex = -1;
		private int stickyTicks;
		private int pulseTimer;
		private int pulsesSinceText;
		private int healedSinceText;
		private int clock;
		private Vector2 wireEnd;
		private bool wireReady;

		// True while the Pod is healing someone (the lens turns green and the wire is out).
		internal bool Healing => targetIndex >= 0;

		internal void Reset()
		{
			targetIndex = -1;
			lastTargetIndex = -1;
			stickyTicks = 0;
			pulseTimer = 0;
			pulsesSinceText = 0;
			healedSinceText = 0;
			wireReady = false;
		}

		// The wire starts again from the Pod (after a teleport or respawn) instead of streaking across the screen.
		internal void ResetWire()
		{
			wireReady = false;
		}

		// Once per game tick, on every machine, while the owner has the Autopilot plugin.
		internal void Update(Player owner)
		{
			clock++;

			if (clock % RetargetIntervalTicks == 0)
				targetIndex = PickTarget(owner);
			else if (targetIndex >= 0 && !IsValidTarget(owner, Main.player[targetIndex]))
				targetIndex = -1;

			stickyTicks = targetIndex >= 0 ? stickyTicks + 1 : 0;

			if (targetIndex != lastTargetIndex)
			{
				lastTargetIndex = targetIndex;
				pulsesSinceText = 0;
				healedSinceText = 0;
				pulseTimer = 0;
			}

			if (targetIndex >= 0)
			{
				pulseTimer++;
				if (pulseTimer >= HealIntervalTicks)
				{
					pulseTimer = 0;
					HealPulse(owner);
				}
			}
		}

		// Client only: moves the loose end of the wire toward the patient (or lets it swing under the Pod when idle).
		internal void UpdateWire(Vector2 podCenter)
		{
			Vector2 hang = podCenter + new Vector2((float)Math.Sin(clock * 0.05f) * 6f, 20f);
			Vector2 desired = targetIndex >= 0 ? Main.player[targetIndex].Center + new Vector2(0f, -4f) : hang;
			wireEnd = wireReady ? Vector2.Lerp(wireEnd, desired, 0.18f) : desired;
			wireReady = true;

			// A small spark where the wire plugs in, once per pulse.
			if (targetIndex >= 0 && clock % HealIntervalTicks == 0)
			{
				Dust dust = Dust.NewDustPerfect(wireEnd, DustID.GreenFairy, Main.rand.NextVector2Circular(1f, 1f), 100, default, 0.9f);
				dust.noGravity = true;
			}
		}

		// Runs once per heal pulse on every machine.
		private void HealPulse(Player owner)
		{
			Player target = Main.player[targetIndex];
			if (!IsValidTarget(owner, target))
				return;

			// The server (or singleplayer) applies the actual heal. Floating text is suppressed on four pulses and shown as a single
			// grouped "+5" on the fifth.
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				pulsesSinceText++;
				bool showText = pulsesSinceText >= PulsesPerText;
				int display = showText ? healedSinceText + HealPerPulse : 0;

				int healed = SupportEffects.ServerHealPlayer(target, HealPerPulse, display);
				if (showText)
				{
					pulsesSinceText = 0;
					healedSinceText = 0;
				}
				else
				{
					healedSinceText += healed;
				}
			}

			// The owner's client records the healing for the analytics panel.
			if (owner.whoAmI == Main.myPlayer)
				AugmentDamageTracker.RecordHealing(AutopilotAugment.ChipId, HealPerPulse);
		}

		private int PickTarget(Player owner)
		{
			int best = FindTarget(owner);
			if (best == targetIndex)
				return best;

			if (targetIndex >= 0 && best >= 0 && IsValidTarget(owner, Main.player[targetIndex]))
			{
				float currentRatio = LifeRatio(Main.player[targetIndex]);
				float bestRatio = LifeRatio(Main.player[best]);
				if (stickyTicks < MinHoldTicks || bestRatio > currentRatio - SwitchMargin)
					return targetIndex;
			}

			stickyTicks = 0;
			return best;
		}

		private static float LifeRatio(Player player)
		{
			int maxLife = Math.Max(player.statLifeMax, player.statLifeMax2);
			return maxLife > 0 ? (float)player.statLife / maxLife : 1f;
		}

		private static bool IsValidTarget(Player owner, Player candidate)
		{
			if (candidate == null || !candidate.active || candidate.dead || candidate.ghost)
				return false;
			if (!SupportEffects.AreAllies(owner, candidate))
				return false;
			if (Vector2.DistanceSquared(candidate.Center, owner.Center) > HealRange * HealRange)
				return false;

			int maxLife = Math.Max(candidate.statLifeMax, candidate.statLifeMax2);
			return maxLife > 0 && candidate.statLife < maxLife;
		}

		// Lowest health percentage wins, so a 100 HP player at 20 HP is preferred over a 500 HP player at 300 HP. Players only, never NPCs.
		private static int FindTarget(Player owner)
		{
			int best = -1;
			float bestRatio = 1f;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player candidate = Main.player[i];
				if (!IsValidTarget(owner, candidate))
					continue;

				float ratio = LifeRatio(candidate);
				if (ratio < bestRatio)
				{
					bestRatio = ratio;
					best = i;
				}
			}

			return best;
		}

		// --- Drawing (client only) ---

		// The wire from under the Pod to its patient, queued as part of the owner's draw so it sorts with the player.
		internal void QueueWire(ref PlayerDrawSet drawInfo, Vector2 podCenter, float alpha)
		{
			if (!wireReady || alpha <= 0f)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Vector2 start = podCenter + new Vector2(0f, 9f);
			Vector2 end = wireEnd;
			float distance = Vector2.Distance(start, end);
			if (distance > 1500f)
				return;

			float sag = MathHelper.Clamp(distance * 0.35f, 8f, 60f);
			Vector2 control = (start + end) * 0.5f + new Vector2(0f, sag);
			int samples = Math.Clamp((int)(distance / 2.5f) + 8, 8, 300);

			// Dark casing first, bright core on top.
			for (int i = 0; i <= samples; i++)
				Queue(ref drawInfo, pixel, Bezier(start, control, end, i / (float)samples), 3, new Color(14, 70, 38) * alpha);
			for (int i = 0; i <= samples; i++)
				Queue(ref drawInfo, pixel, Bezier(start, control, end, i / (float)samples), 2, new Color(80, 255, 140) * alpha);

			// Plug at the loose end.
			Queue(ref drawInfo, pixel, end, 5, new Color(14, 70, 38) * alpha);
			Queue(ref drawInfo, pixel, end, 3, new Color(150, 255, 190) * alpha);

			// A bright pulse travels down the wire on every heal tick.
			if (targetIndex >= 0)
			{
				float u = (clock % HealIntervalTicks) / (float)HealIntervalTicks;
				Queue(ref drawInfo, pixel, Bezier(start, control, end, u), 4, new Color(210, 255, 225) * alpha);
			}
		}

		private static void Queue(ref PlayerDrawSet drawInfo, Texture2D pixel, Vector2 worldCenter, int size, Color color)
		{
			Vector2 screen = worldCenter - Main.screenPosition;
			drawInfo.DrawDataCache.Add(new DrawData(pixel, new Rectangle((int)MathF.Round(screen.X - size * 0.5f), (int)MathF.Round(screen.Y - size * 0.5f), size, size), color));
		}

		private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float u)
		{
			float inv = 1f - u;
			return inv * inv * a + 2f * inv * u * b + u * u * c;
		}
	}
}
