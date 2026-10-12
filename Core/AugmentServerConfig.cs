using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Augments
{
	// Server-side so the host/world owner decides: on a multiplayer server the
	// server's value applies to everyone, in singleplayer it is the player's own.
	public class AugmentServerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static AugmentServerConfig Instance => ModContent.GetInstance<AugmentServerConfig>();

		[DefaultValue(false)]
		public bool EnableDeveloperTools;

		// Damage scaling multipliers for chips with a fixed damage number. The highest
		// stage reached in the world applies, so every player on the server shares it.
		[Header("VanillaDamageScaling")]

		[DefaultValue(1f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleStart;

		[DefaultValue(1.25f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleSkeletron;

		[DefaultValue(2f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleWallOfFlesh;

		[DefaultValue(3f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleMechs;

		[DefaultValue(3.5f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScalePlantera;

		[DefaultValue(4f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleGolem;

		[DefaultValue(5f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleMoonLord;

		[Header("CalamityDamageScaling")]

		[DefaultValue(6f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleProvidence;

		[DefaultValue(7.5f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleDevourerOfGods;

		[DefaultValue(9f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleYharon;

		[DefaultValue(10f)]
		[Range(0.25f, 20f)]
		[Increment(0.25f)]
		[Slider]
		public float ScaleExoMechsOrSupremeCalamitas;
	}
}
