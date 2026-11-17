using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace Augments
{
	// The Pod 042 pet: a Pod that floats beside every player's shoulder, in the skin they chose, visible to everyone so skins can be
	// shown off. It is purely cosmetic: it has no hitbox and does nothing in the game's rules.
	//
	//   * Each player has two synced values, "Pod on" and "skin". Only the owner's own game works them out (from the account file);
	//     every other game, and the server, only receive them in a packet. A modified client could claim any skin; that is harmless
	//     because it is only a colour.
	//   * The server keeps the values it relays, so a player who joins later still sees everyone's Pod.
	//   * Position is worked out on each machine from where the player is, so nothing about movement is sent over the network.
	//   * Tips from Pod 042 show as a speech bubble next to the owner's own Pod (see AugmentAdvisoryHUD). They are never sent to anyone,
	//     so other players see the Pod but not its tips.
	public class PodPetPlayer : ModPlayer
	{
		public bool PodOn = true;
		public byte SkinIndex;

		internal Vector2 podPos;
		internal bool podReady;
		private float bobClock;

		// The owner's setting. Saved in the account file (so it follows you between characters) and changed with /pod, the keybind,
		// or the button on the Cosmetics page.
		internal static bool SettingOn => AccountStore.Equipped("pod", "on") != "off";

		internal static void SetSetting(bool on)
		{
			AccountStore.Equip("pod", on ? "on" : "off");
		}

		internal bool Visible => PodOn && podReady && Player.active && !Player.dead && !Player.ghost && !Main.gameMenu;

		private void RefreshLocal()
		{
			PodOn = SettingOn;
			SkinIndex = (byte)Cosmetic.EquippedPodIndex;
		}

		// --- Upgrades ---
		// Plugins that upgrade the Pod (see PodUpgrades) change this same Pod instead of adding another one: an LED on each arm, a new
		// name, and new abilities. The Autopilot plugin is the first.
		internal readonly PodAutopilot Autopilot = new PodAutopilot();
		private readonly List<PodUpgrade> activeUpgrades = new List<PodUpgrade>();
		private readonly List<Color> moduleColors = new List<Color>();
		internal bool AutopilotOn;
		internal IReadOnlyList<Color> ModuleColors => moduleColors;
		internal float AnimClock;
		internal int BlinkTicks;
		private int nextBlink = 150;

		// Runs on every machine, the server included: a Pod's abilities (healing) are worked out from the owner's installed plugins,
		// which everyone knows, and applied by the server or singleplayer only.
		private void TickUpgrades()
		{
			PodUpgrades.Active(Player, activeUpgrades);

			moduleColors.Clear();
			AutopilotOn = false;
			foreach (PodUpgrade upgrade in activeUpgrades)
			{
				moduleColors.Add(upgrade.Color);
				if (upgrade.ChipId == AutopilotAugment.ChipId)
					AutopilotOn = true;
			}

			if (AutopilotOn && Player.active && !Player.dead && !Player.ghost)
				Autopilot.Update(Player);
			else
				Autopilot.Reset();
		}

		public override void PostUpdate()
		{
			TickUpgrades();

			if (Main.dedServ)
				return;

			if (Player.whoAmI == Main.myPlayer)
				RefreshLocal();

			if (!Player.active)
			{
				podReady = false;
				return;
			}

			// It bobs gently, and a little faster while its tip is being "spoken".
			float talk = Player.whoAmI == Main.myPlayer && AugmentAdvisoryHUD.IsTalking ? 1f : 0f;
			AnimClock += 1f;
			if (BlinkTicks > 0)
				BlinkTicks--;
			else if (--nextBlink <= 0)
			{
				BlinkTicks = 7; // the lens shutters now and then
				nextBlink = Main.rand.Next(150, 420);
			}
			bobClock += 0.06f + 0.05f * talk;
			float bob = (float)Math.Sin(bobClock) * (3f + 2f * talk);

			// Next to the shoulder on the side the player is not facing, a little above the head line.
			Vector2 target = Player.Center + new Vector2(-Player.direction * 20f, (-30f + bob) * Player.gravDir);
			if (!podReady || Vector2.DistanceSquared(podPos, target) > 400f * 400f)
			{
				podPos = target; // first frame, or a teleport: do not fly across the map
				Autopilot.ResetWire();
				podReady = true;
			}
			else
			{
				podPos = Vector2.Lerp(podPos, target, 0.14f);
			}

			if (AutopilotOn && Visible)
			{
				Autopilot.UpdateWire(podPos);
				Lighting.AddLight(podPos, 0.05f, 0.28f, 0.14f); // the medic upgrade glows green
			}
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Main.dedServ || Player.whoAmI != Main.myPlayer)
				return;

			if (Augments.TogglePodKeybind?.JustPressed == true)
				Toggle();
		}

		internal static void Toggle()
		{
			bool on = !SettingOn;
			SetSetting(on);
			if (!Main.dedServ && !Main.gameMenu)
				Main.NewText(on ? "Pod 042 is back." : "Pod 042 is hidden.", 148, 163, 184);
		}

		// Where the local player's Pod is, in the units the HUD draws in; false if there is no Pod to show.
		internal static bool TryGetLocalPodScreenPos(out Vector2 uiPos)
		{
			uiPos = default;
			if (Main.dedServ || Main.gameMenu)
				return false;

			Player p = Main.LocalPlayer;
			if (p == null || !p.active)
				return false;

			PodPetPlayer pet = p.GetModPlayer<PodPetPlayer>();
			if (!pet.Visible)
				return false;

			Vector2 screen = Vector2.Transform(pet.podPos - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
			uiPos = screen / Main.UIScale;
			return true;
		}

		// --- Multiplayer ---

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Player.whoAmI == Main.myPlayer)
				RefreshLocal(); // the first sync can happen before the first update

			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)AugmentPacketType.PodPetSync);
			packet.Write((byte)Player.whoAmI);
			packet.Write(PodOn);
			packet.Write(SkinIndex);
			packet.Send(toWho, fromWho);
		}

		public override void CopyClientState(ModPlayer targetCopy)
		{
			var clone = (PodPetPlayer)targetCopy;
			clone.PodOn = PodOn;
			clone.SkinIndex = SkinIndex;
		}

		public override void SendClientChanges(ModPlayer clientPlayer)
		{
			var clone = (PodPetPlayer)clientPlayer;
			if (clone.PodOn != PodOn || clone.SkinIndex != SkinIndex)
				SyncPlayer(-1, Main.myPlayer, false);
		}

		internal static void HandleSync(BinaryReader reader, int whoAmI)
		{
			byte claimed = reader.ReadByte();
			bool on = reader.ReadBoolean();
			byte skin = reader.ReadByte();

			// The server only ever believes a client about itself.
			int index = Main.netMode == NetmodeID.Server ? whoAmI : claimed;
			if (index < 0 || index >= Main.maxPlayers || skin >= Cosmetic.PodSkins.Length)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer)
				return; // our own state is ours alone

			PodPetPlayer pet = Main.player[index].GetModPlayer<PodPetPlayer>();
			pet.PodOn = on;
			pet.SkinIndex = skin;

			// Pass it on to everyone else. The server keeps the values too, so a late joiner is told them when they arrive.
			if (Main.netMode == NetmodeID.Server)
				pet.SyncPlayer(-1, whoAmI, false);
		}
	}

	// Draws each player's Pod as part of the player, so it is positioned and zoomed like the rest of the world. It is skipped for the
	// ghost copies drawn behind fast movement, and for the character previews in menus.
	public class PodPetDrawLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.LastVanillaLayer);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			return !Main.gameMenu && drawInfo.shadow == 0f && !drawInfo.drawPlayer.invis;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			Player player = drawInfo.drawPlayer;
			PodPetPlayer pet = player.GetModPlayer<PodPetPlayer>();
			if (!pet.Visible)
				return;

			Texture2D tex = PodSprites.Get(pet.SkinIndex, pet.ModuleColors);
			if (tex == null)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Vector2 screen = new Vector2((float)Math.Round(pet.podPos.X - Main.screenPosition.X), (float)Math.Round(pet.podPos.Y - Main.screenPosition.Y));
			float tilt = MathHelper.Clamp(player.velocity.X * 0.02f, -0.15f, 0.15f);

			// Lit by the world, but never fully dark, so it stays easy to see (and to show off).
			Color light = Lighting.GetColor(pet.podPos.ToTileCoordinates());
			Color color = Color.Lerp(light, Color.White, 0.6f);

			Cosmetic.PodSkinInfo skinInfo = Cosmetic.PodSkins[Math.Min(pet.SkinIndex, Cosmetic.PodSkins.Length - 1)];
			string style = skinInfo.Style ?? "";
			PodSprites.Palette palette = PodSprites.MakePalette(skinInfo);
			float t = pet.AnimClock;

			// Where a point of the sprite is on screen (the sprite tilts a little with movement).
			Vector2 At(Vector2 local) => screen + (local - PodSprites.Origin).RotatedBy(tilt);

			List<DrawData> cache = drawInfo.DrawDataCache; // (a ref parameter cannot be used inside a local function)
			void Dot(Vector2 centre, int w, int h, Color c)
			{
				cache.Add(new DrawData(pixel, new Rectangle((int)Math.Round(centre.X - w * 0.5f), (int)Math.Round(centre.Y - h * 0.5f), w, h), c));
			}

			// 1. The Autopilot upgrade's wire, under everything.
			if (pet.AutopilotOn)
				pet.Autopilot.QueueWire(ref drawInfo, pet.podPos, 1f);

			// Glory to Mankind pod: a soft gold halo that breathes, behind everything.
			if (style == "glory")
			{
				float breath = 0.5f + 0.5f * (float)Math.Sin(t * 0.033f);
				Vector2 mid = At(PodSprites.Origin);
				Texture2D glow = PodSprites.Halo();
				if (glow != null)
					cache.Add(new DrawData(glow, mid, null, new Color(255, 214, 90) * (0.42f + 0.18f * breath), 0f, new Vector2(glow.Width / 2f, glow.Height / 2f), 0.75f, SpriteEffects.None, 0));
			}
			// 2. Thruster flame, flickering, behind the body.
			float flicker = 0.5f + 0.5f * (float)Math.Sin(t * 0.9f) * (float)Math.Cos(t * 0.37f);
			int flameLen = 2 + (int)Math.Round(flicker * 3f + Math.Min(1.5f, Math.Abs(player.velocity.X) * 0.15f));
			Vector2 nozzle = At(PodSprites.Nozzle);
			Dot(nozzle + new Vector2(0f, flameLen * 0.5f), 4, flameLen + 1, palette.Thruster * 0.25f);
			Dot(nozzle + new Vector2(0f, flameLen * 0.5f), 2, flameLen, Color.Lerp(palette.Thruster, Color.White, 0.5f) * 0.9f);

			// 3. The body.
			drawInfo.DrawDataCache.Add(new DrawData(tex, screen, null, color, tilt, PodSprites.Origin, 1f, SpriteEffects.None, 0));

			// Glory to Mankind pod: a band of light sweeps across the gold, and sparkles twinkle round it.
			if (style == "glory")
			{
				float band = (t * 0.2f) % 46f - 12f;
				foreach (Vector2 cell in PodSprites.GlintCells)
				{
					float d = Math.Abs(cell.X + cell.Y * 0.7f - band);
					if (d < 2.2f)
						Dot(At(cell + new Vector2(0.5f, 0.5f)), 1, 1, Color.White * ((1f - d / 2.2f) * 0.8f));
				}

				for (int i = 0; i < PodSprites.SparkleSpots.Length; i++)
				{
					float phase = (t * 0.0217f + i * 0.37f) % 1f;
					if (phase >= 0.28f)
						continue;

					float a = (float)Math.Sin(phase / 0.28f * Math.PI);
					Vector2 p = At(PodSprites.SparkleSpots[i]);
					Dot(p, 1, 1, new Color(255, 246, 200) * a);
					Dot(p, 3, 1, new Color(255, 230, 140) * (a * 0.7f));
					Dot(p, 1, 3, new Color(255, 230, 140) * (a * 0.7f));
				}
			}
			// Hologram pod: a bright scan line travels down the projection.
			if (style == "hologram")
			{
				float scan = (t * 0.12f) % 26f - 4f;
				if (scan > 3f && scan < 16f)
					Dot(At(new Vector2(10f, scan)), 12, 1, new Color(190, 255, 250) * 0.55f);
			}

			// 4. The lens glows and pulses (green while the Autopilot upgrade is healing someone), and shutters shut now and then.
			Vector2 lens = At(PodSprites.Lens);
			bool healing = pet.AutopilotOn && pet.Autopilot.Healing;
			Color lensColor = healing ? new Color(110, 255, 160) : palette.EyeBright;
			float pulse = 0.5f + 0.5f * (float)Math.Sin(t * (healing ? 0.22f : 0.07f));
			Dot(lens, 6, 7, lensColor * (0.10f + 0.12f * pulse));
			float lensFlicker = style == "scarred" && (int)(t / 9f) % 7 == 0 ? 0.2f : 1f; // the scarred pod's lens stutters now and then
			Dot(lens, 2, 3, lensColor * ((0.45f + 0.35f * pulse) * lensFlicker));
			if (pet.BlinkTicks > 0)
			{
				Dot(lens, 4, 5, palette.Seam);
				Dot(lens, 4, 1, palette.Signal);
			}

			// 5. The antenna's signal light blinks.
			// The scarred pod's warning light blinks faster, as if something is wrong.
			bool signalOn = style == "scarred" ? (int)(t / 14f) % 2 == 0 : (int)(t / 24f) % 3 == 0;
			if (signalOn)
				Dot(At(PodSprites.Antenna), 2, 2, Color.Lerp(palette.Signal, Color.White, 0.35f));
		}
	}

	// /pod to show or hide your Pod, /pod skin <name> to wear a skin you own.
	public class PodCommand : ModCommand
	{
		public override string Command => "pod";
		public override CommandType Type => CommandType.Chat;
		public override string Usage => "/pod [on|off|skin <standard|amber|frost|verdant|violet|gold>]";
		public override string Description => "Show or hide your Pod 042 pet, or change its skin.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length == 0)
			{
				PodPetPlayer.Toggle();
				return;
			}

			string first = args[0].ToLowerInvariant();
			if (first == "on" || first == "off")
			{
				PodPetPlayer.SetSetting(first == "on");
				caller.Reply(first == "on" ? "Pod 042 is back." : "Pod 042 is hidden.");
				return;
			}

			if (first == "skin" && args.Length >= 2)
			{
				string wanted = args[1].ToLowerInvariant();
				if (wanted == "standard")
					wanted = "default";

				foreach (Cosmetic.PodSkinInfo skin in Cosmetic.PodSkins)
				{
					if (skin.Id != wanted)
						continue;

					if (wanted != "default" && !AccountStore.IsUnlocked("podskin:" + wanted))
					{
						caller.Reply("You have not unlocked that skin yet. Skins come from Campaign rewards and hidden endings.", Color.OrangeRed);
						return;
					}

					AccountStore.Equip("podskin", wanted);
					caller.Reply("Pod skin: " + skin.Name + ".");
					return;
				}

				caller.Reply("Skins: standard, amber, frost, verdant, violet, gold.", Color.OrangeRed);
				return;
			}

			caller.Reply("Use: " + Usage, Color.OrangeRed);
		}
	}
}
