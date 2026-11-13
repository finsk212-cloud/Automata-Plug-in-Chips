using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// Earns Data Fragments for the local player and feeds the Orders. This is the only place the account earns anything.
	//
	//   * Active play: a second counts when the game window has focus, the player is alive and something happened in the last
	//     90 seconds (a key, the mouse, or the character moving). Standing still with the window focused for longer earns nothing.
	//   * Bosses: only boss kills that happen AFTER you enter the world count. What the character already had when it loaded is
	//     recorded first and ignored, so loading a saved or downloaded character pays nothing.
	//   * Everything runs on the local client only; a dedicated server never earns for anyone.
	public class AccountEarningPlayer : ModPlayer
	{
		private const int SecondsPerFragment = 30;     // 2 Data Fragments per active minute
		private const int IdleLimitTicks = 90 * 60;    // no input for 90 seconds stops the clock
		private const int BaselineDelayTicks = 10 * 60; // wait 10 seconds after entering so a multiplayer sync has arrived
		private const int FlushEverySeconds = 60;

		private uint lastInputTick;
		private int tickCounter;
		private int fragmentCarry;
		private int xpCarry;
		private int flushCarry;
		private int ticksInWorld;
		private int lastMouseX;
		private int lastMouseY;
		private double aliveStretchSeconds;
		private Dictionary<int, int> bossBaseline; // boss kill counts when the baseline was taken; null until then

		private bool IsLocal => !Main.dedServ && Player.whoAmI == Main.myPlayer;

		// Used by the Orders page while a world is loaded.
		public static double AliveStretch
		{
			get
			{
				if (Main.gameMenu || Main.dedServ || Main.LocalPlayer == null)
					return 0;
				return Main.LocalPlayer.GetModPlayer<AccountEarningPlayer>().aliveStretchSeconds;
			}
		}

		public override void OnEnterWorld()
		{
			if (!IsLocal)
				return;

			lastInputTick = Main.GameUpdateCount;
			tickCounter = 0;
			fragmentCarry = 0;
			xpCarry = 0;
			flushCarry = 0;
			ticksInWorld = 0;
			aliveStretchSeconds = 0;
			bossBaseline = null;
		}

		public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
		{
			if (IsLocal)
				aliveStretchSeconds = 0;
		}

		public override void PostUpdate()
		{
			if (!IsLocal)
				return;

			ticksInWorld++;

			// Take the boss baseline once, shortly after entering.
			if (bossBaseline == null && ticksInWorld >= BaselineDelayTicks)
				bossBaseline = new Dictionary<int, int>(Player.GetModPlayer<AugmentPlayer>().BossAugmentKills);

			bool input = Main.keyState.GetPressedKeys().Length > 0 || Main.mouseLeft || Main.mouseRight
				|| Main.mouseX != lastMouseX || Main.mouseY != lastMouseY || Player.velocity.LengthSquared() > 0.04f;
			lastMouseX = Main.mouseX;
			lastMouseY = Main.mouseY;
			if (input)
				lastInputTick = Main.GameUpdateCount;

			bool active = Main.hasFocus && !Main.gamePaused && !Player.dead && Main.GameUpdateCount - lastInputTick < IdleLimitTicks;
			if (!active)
				return;

			if (++tickCounter < 60)
				return;
			tickCounter = 0;

			OnActiveSecond();
		}

		private void OnActiveSecond()
		{
			AccountStore.AddPlay(1, OtherPlayersOnline());
			aliveStretchSeconds += 1;

			if (++fragmentCarry >= SecondsPerFragment)
			{
				fragmentCarry = 0;
				AccountStore.Earn(1);
			}

			// Operation XP: the same active seconds, 5 XP a minute (one XP every 12 seconds).
			if (++xpCarry >= 60 / Operations.XpPerActiveMinute)
			{
				xpCarry = 0;
				GainOperationXp(1);
			}

			CreditBossKills();

			List<Mission> finished = Missions.CompleteReached(aliveStretchSeconds);
			if (finished != null)
			{
				foreach (Mission m in finished)
				{
					Main.NewText("ORDER COMPLETE: " + m.Title + "   +" + m.Reward + " DATA FRAGMENTS", 232, 224, 190);
					SoundEngine.PlaySound(SoundID.Item35);
					GainOperationXp(m.Reward); // an order also fills the Operation by as much as it pays in fragments
				}
				AccountStore.Flush();
			}

			if (++flushCarry >= FlushEverySeconds)
			{
				flushCarry = 0;
				AccountStore.Flush();
			}
		}

		// Adds Operation XP and announces it when a new tier is reached.
		private static void GainOperationXp(long amount)
		{
			Operation op = Operations.Current;
			int before = Operations.TierReached(op);
			AccountStore.AddOperationXp(op.Id, amount);
			int after = Operations.TierReached(op);

			if (after > before)
			{
				Main.NewText("CAMPAIGN " + op.Number + ": TIER " + after + " REACHED   OPEN THE CAMPAIGN PAGE ON THE TITLE SCREEN TO CLAIM", 232, 224, 190);
				SoundEngine.PlaySound(SoundID.Item35);
			}
		}

		// A boss counts when its kill count for this character went up since the baseline.
		private void CreditBossKills()
		{
			if (bossBaseline == null)
				return;

			foreach (KeyValuePair<int, int> kill in Player.GetModPlayer<AugmentPlayer>().BossAugmentKills)
			{
				bossBaseline.TryGetValue(kill.Key, out int before);
				if (kill.Value > before)
				{
					AccountStore.NoteBoss(kill.Key.ToString());
					bossBaseline[kill.Key] = kill.Value;
				}
			}
		}

		private static bool OtherPlayersOnline()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				return false;

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (i != Main.myPlayer && p.active && !p.dead)
					return true;
			}
			return false;
		}
	}

	// Loads the account with the mod and writes it when you leave a world or the mod unloads.
	public class AccountSystem : ModSystem
	{
		public override void OnWorldUnload()
		{
			AccountStore.Flush();
		}
	}
}
