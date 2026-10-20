using System;
using System.Collections.Generic;
using Augments.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	// The slot window that opens when a placed Banner Station is right-clicked.
	public class BannerStationUIState : UIState
	{
		private const int Columns = 10;
		private const int Cell = 46;
		private const int SlotSize = 42;
		private const int MaxVisibleRows = 5;
		private const float PanelWidth = Columns * Cell + 40f;
		private const float HeaderHeight = 56f;
		private const float FooterHeight = 92f;

		private StationPanel panel;
		private StationGrid grid;
		private StationButton depositAll;
		private int openTier = -1;

		public int StationLeft { get; private set; }
		public int StationTop { get; private set; }

		public override void OnInitialize()
		{
			panel = new StationPanel(this);
			panel.HAlign = 0.5f;
			panel.VAlign = 0.3f;
			PanelChrome.Style(panel, zeroPadding: true);
			PanelChrome.AddCloseButton(panel, () => ModContent.GetInstance<AugmentUISystem>().HideBannerStation());

			grid = new StationGrid(this);
			grid.Left.Set(20f, 0f);
			grid.Top.Set(HeaderHeight, 0f);
			grid.Width.Set(Columns * Cell, 0f);
			panel.Append(grid);

			depositAll = new StationButton("Deposit all banners", DepositAllFromInventory);
			depositAll.Left.Set(20f, 0f);
			depositAll.Width.Set(190f, 0f);
			depositAll.Height.Set(28f, 0f);
			panel.Append(depositAll);

			Append(panel);
		}

		public void Open(int left, int top)
		{
			StationLeft = left;
			StationTop = top;
			openTier = -1;
			grid.Scroll = 0;
			Layout();
		}

		public bool TryGetStation(out BannerStationEntity station) =>
			BannerStationEntity.TryGetAt(StationLeft, StationTop, out station);

		// Sizes the window to the station's tier: a short window for Mk I, a scrolling one for the big tiers.
		private void Layout()
		{
			if (!TryGetStation(out BannerStationEntity station))
				return;

			int tier = station.GetTier();
			if (tier == openTier)
				return;
			openTier = tier;

			int rows = Math.Min(MaxVisibleRows, (station.SlotCount + Columns - 1) / Columns);
			float gridHeight = rows * Cell;

			grid.Height.Set(gridHeight, 0f);
			panel.Width.Set(PanelWidth, 0f);
			panel.Height.Set(HeaderHeight + gridHeight + FooterHeight, 0f);

			depositAll.Top.Set(HeaderHeight + gridHeight + FooterHeight - 40f, 0f);
			panel.Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);

			Player player = Main.LocalPlayer;
			if (!TryGetStation(out BannerStationEntity station) || !player.active || player.dead || !Main.playerInventory)
			{
				ModContent.GetInstance<AugmentUISystem>().HideBannerStation();
				return;
			}

			// Walking away closes it, the same way a chest does.
			if (Vector2.Distance(player.Center, station.Center) > 16f * 14f)
			{
				ModContent.GetInstance<AugmentUISystem>().HideBannerStation();
				return;
			}

			Layout();

			if (panel.ContainsPoint(Main.MouseScreen))
				player.mouseInterface = true;
		}

		// Moves one of each banner type from the inventory into the station, until it is full.
		private void DepositAllFromInventory()
		{
			if (!TryGetStation(out BannerStationEntity station))
				return;

			Player player = Main.LocalPlayer;
			int free = station.SlotCount - station.Banners.Count;
			int moved = 0;

			for (int i = 0; i < 50 && free > 0; i++)
			{
				Item item = player.inventory[i];
				if (item.IsAir || station.Banners.Contains(item.type) || !BannerStationData.IsBannerItem(item.type))
					continue;

				// A successful deposit also adds the banner to the local copy, so later slots see it.
				if (!BannerStationNet.Deposit(StationLeft, StationTop, item.type))
					continue;

				item.stack--;
				if (item.stack <= 0)
					item.TurnToAir();

				free--;
				moved++;
			}

			if (moved > 0)
				SoundEngine.PlaySound(SoundID.Grab);
		}

		private sealed class StationPanel : UIPanel
		{
			private readonly BannerStationUIState owner;

			public StationPanel(BannerStationUIState owner)
			{
				this.owner = owner;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				base.DrawSelf(spriteBatch);

				if (!owner.TryGetStation(out BannerStationEntity station))
					return;

				Rectangle rect = GetDimensions().ToRectangle();
				PanelChrome.DrawFrame(spriteBatch, rect);
				PanelChrome.DrawHeaderDivider(spriteBatch, rect, 44);

				int tier = station.GetTier();
				DynamicSpriteFont font = FontAssets.MouseText.Value;

				Utils.DrawBorderString(spriteBatch, "Banner Station " + BannerStationData.TierNames[tier], new Vector2(rect.X + 22, rect.Y + 14), Color.White, 0.95f);

				string count = $"{station.Banners.Count} / {station.SlotCount}";
				Utils.DrawBorderString(spriteBatch, count, new Vector2(rect.Right - 60 - font.MeasureString(count).X * 0.9f, rect.Y + 15), PanelChrome.Accent, 0.9f);

				int footerY = rect.Bottom - 84;
				string range = $"Range: {BannerStationData.RangeTilesX(tier)} x {BannerStationData.RangeTilesY(tier)} tiles around the station";
				Utils.DrawBorderString(spriteBatch, range, new Vector2(rect.X + 22, footerY), Color.LightGray, 0.8f);
				Utils.DrawBorderString(spriteBatch, "Click a slot holding a banner on your cursor to store it.", new Vector2(rect.X + 22, footerY + 18), Color.Gray, 0.75f);
				Utils.DrawBorderString(spriteBatch, "Click a stored banner to take it out.", new Vector2(rect.X + 22, footerY + 34), Color.Gray, 0.75f);
			}
		}

		private sealed class StationGrid : UIElement
		{
			private readonly BannerStationUIState owner;
			public int Scroll;

			public StationGrid(BannerStationUIState owner)
			{
				this.owner = owner;
			}

			private int VisibleRows => Math.Max(1, (int)(GetDimensions().Height / Cell));

			private int TotalRows(BannerStationEntity station) => (station.SlotCount + Columns - 1) / Columns;

			private int ClampScroll(BannerStationEntity station, int value) =>
				Math.Clamp(value, 0, Math.Max(0, TotalRows(station) - VisibleRows));

			private Rectangle SlotRect(Rectangle area, int row, int col) =>
				new Rectangle(area.X + col * Cell + (Cell - SlotSize) / 2, area.Y + row * Cell + (Cell - SlotSize) / 2, SlotSize, SlotSize);

			// Which station slot index is under a screen point, or -1.
			private int SlotAt(BannerStationEntity station, Point point)
			{
				Rectangle area = GetDimensions().ToRectangle();
				for (int row = 0; row < VisibleRows; row++)
				{
					for (int col = 0; col < Columns; col++)
					{
						int index = (row + Scroll) * Columns + col;
						if (index >= station.SlotCount)
							return -1;
						if (SlotRect(area, row, col).Contains(point))
							return index;
					}
				}
				return -1;
			}

			public override void ScrollWheel(UIScrollWheelEvent evt)
			{
				base.ScrollWheel(evt);
				if (owner.TryGetStation(out BannerStationEntity station))
					Scroll = ClampScroll(station, Scroll - Math.Sign(evt.ScrollWheelValue));
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);

				if (!owner.TryGetStation(out BannerStationEntity station))
					return;

				int index = SlotAt(station, Main.MouseScreen.ToPoint());
				if (index < 0)
					return;

				Item cursor = Main.mouseItem;
				if (!cursor.IsAir)
				{
					if (!BannerStationData.IsBannerItem(cursor.type) || !BannerStationNet.Deposit(owner.StationLeft, owner.StationTop, cursor.type))
						return;

					cursor.stack--;
					if (cursor.stack <= 0)
						cursor.TurnToAir();

					SoundEngine.PlaySound(SoundID.Grab);
				}
				else if (index < station.Banners.Count)
				{
					BannerStationNet.Withdraw(owner.StationLeft, owner.StationTop, station.Banners[index]);
					SoundEngine.PlaySound(SoundID.Grab);
				}
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				if (!owner.TryGetStation(out BannerStationEntity station))
					return;

				Scroll = ClampScroll(station, Scroll);

				Rectangle area = GetDimensions().ToRectangle();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Point mouse = Main.MouseScreen.ToPoint();
				int hovered = ContainsPoint(Main.MouseScreen) ? SlotAt(station, mouse) : -1;

				for (int row = 0; row < VisibleRows; row++)
				{
					for (int col = 0; col < Columns; col++)
					{
						int index = (row + Scroll) * Columns + col;
						if (index >= station.SlotCount)
							break;

						Rectangle slot = SlotRect(area, row, col);
						bool filled = index < station.Banners.Count;
						bool hot = index == hovered;

						spriteBatch.Draw(pixel, slot, (hot ? new Color(24, 38, 62) : new Color(14, 22, 38)) * 0.95f);
						Color edge = filled ? PanelChrome.Accent * 0.55f : PanelChrome.Divider;
						spriteBatch.Draw(pixel, new Rectangle(slot.X, slot.Y, slot.Width, 1), edge);
						spriteBatch.Draw(pixel, new Rectangle(slot.X, slot.Bottom - 1, slot.Width, 1), edge);
						spriteBatch.Draw(pixel, new Rectangle(slot.X, slot.Y, 1, slot.Height), edge);
						spriteBatch.Draw(pixel, new Rectangle(slot.Right - 1, slot.Y, 1, slot.Height), edge);

						if (filled)
							DrawBanner(spriteBatch, station.Banners[index], slot);
					}
				}

				if (hovered >= 0 && hovered < station.Banners.Count)
				{
					var hover = new Item();
					hover.SetDefaults(station.Banners[hovered]);
					Main.HoverItem = hover;
					Main.hoverItemName = hover.Name;
				}

				if (TotalRows(station) > VisibleRows)
				{
					int trackX = area.Right + 6;
					float t = Scroll / (float)(TotalRows(station) - VisibleRows);
					int thumbH = Math.Max(14, (int)(area.Height * VisibleRows / (float)TotalRows(station)));
					int thumbY = area.Y + (int)((area.Height - thumbH) * t);
					spriteBatch.Draw(pixel, new Rectangle(trackX, area.Y, 3, area.Height), PanelChrome.Divider);
					spriteBatch.Draw(pixel, new Rectangle(trackX, thumbY, 3, thumbH), PanelChrome.Accent * 0.8f);
				}
			}

			private static void DrawBanner(SpriteBatch spriteBatch, int type, Rectangle slot)
			{
				Main.instance.LoadItem(type);
				Texture2D texture = TextureAssets.Item[type].Value;
				Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(texture) : texture.Frame();
				float scale = Math.Min(1f, 34f / Math.Max(frame.Width, frame.Height));
				spriteBatch.Draw(texture, slot.Center.ToVector2(), frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
			}
		}

		private sealed class StationButton : UIElement
		{
			private readonly string label;
			private readonly Action onClick;
			private bool hovered;

			public StationButton(string label, Action onClick)
			{
				this.label = label;
				this.onClick = onClick;
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				onClick?.Invoke();
			}

			public override void MouseOver(UIMouseEvent evt)
			{
				base.MouseOver(evt);
				hovered = true;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			public override void MouseOut(UIMouseEvent evt)
			{
				base.MouseOut(evt);
				hovered = false;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				Rectangle rect = GetDimensions().ToRectangle();
				Texture2D pixel = TextureAssets.MagicPixel.Value;
				Color edge = hovered ? PanelChrome.Accent : PanelChrome.Accent * 0.45f;

				spriteBatch.Draw(pixel, rect, (hovered ? new Color(24, 44, 72) : new Color(14, 26, 44)) * 0.95f);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), edge);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edge);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), edge);
				spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge);

				Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.85f;
				Utils.DrawBorderString(spriteBatch, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f + 2f), Color.White, 0.85f);
			}
		}
	}
}
