using System;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Augments
{
	public enum TelemetryOpenMode
	{
		[Description("Click the arrow tab to toggle open/close (Prevents accidental opening during combat)")]
		ClickToToggle,

		[Description("Hover mouse over the arrow tab to smoothly slide open")]
		HoverToOpen
	}

	public class AugmentConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static AugmentConfig Instance => Terraria.ModLoader.ModContent.GetInstance<AugmentConfig>();

		[Header("HiddenStatsSettings")]

		[DefaultValue(true)]
		public bool EnableHiddenStatsPanel;

		[DefaultValue(TelemetryOpenMode.ClickToToggle)]
		public TelemetryOpenMode DrawerOpenMode;

		[DefaultValue(false)]
		public bool DockOnRightSide;

		[Header("AdvisorySettings")]

		[DefaultValue(true)]
		public bool EnableAdvisoryTips;

		[DefaultValue(7)]
		[Range(1, 30)]
		public int AdvisoryTipIntervalMinutes;

		[Header("SoundSettings")]

		[DefaultValue(true)]
		public bool EnableGetOverHereSfx;

		[Header("TitleMenuSettings")]

		[DefaultValue(true)]
		public bool UseAutomataTitleMenu;

		// The Earth backdrop, title card, corner text and menu buttons (moved here from the Automata: Music mod).
		[DefaultValue(true)]
		public bool BunkerMenuTheme;

		// Makes the Automata title screen the active menu theme automatically when the mod is enabled.
		[DefaultValue(true)]
		public bool SetAsDefaultMenuTheme;
	}
}
