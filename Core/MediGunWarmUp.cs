using System;

namespace Augments
{
	// The Medi Gun's warm-up: the longer a tether stays on the same target, the harder it works.
	// The damage tether hits up to +50% harder after 5 seconds on one enemy, and the heal tether heals up to +50% more after 5 seconds on
	// one ally (at once, for an ally under half health). Switching target, or letting go of the beam, starts the warm-up again.
	internal static class MediGunWarmUp
	{
		public const int Ticks = 300;          // 5 seconds
		public const float MaxBonus = 0.5f;    // +50%

		// 1.0 with no warm-up, up to 1 + MaxBonus when fully warm (or straight away when `instant` is true).
		public static float Multiplier(int warmTicks, bool instant = false)
		{
			float warm = instant ? 1f : Math.Min(1f, warmTicks / (float)Ticks);
			return 1f + MaxBonus * warm;
		}

		// The amount with the warm-up applied, never lower than the base amount.
		public static int Scale(int amount, float multiplier)
		{
			return Math.Max(amount, (int)Math.Round(amount * multiplier));
		}
	}
}
