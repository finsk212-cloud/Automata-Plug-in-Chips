namespace Augments
{
	// The turn on and turn off of one panel, for CrtScreenEffect. The Plugins menu, the Shop and Combat Analytics each own one.
	//
	// `Open` goes from 0 (screen off) to 1 (fully on). Show() turns the panel on, Hide() turns it off, and Tick() advances the animation.
	// When a panel has finished turning off, Tick() returns true once so the caller can take the panel away.
	public sealed class PanelScreen
	{
		private const float OnSeconds = 0.30f;
		private const float OffSeconds = 0.38f;

		// +1 while turning on, -1 while turning off, 0 when settled.
		private int dir;

		public float Open { get; private set; } = 1f;

		// True from Show() until the turn off has finished.
		public bool Shown { get; private set; }

		// Turning off right now. The panel is still drawn (by the effect) but no longer counts as open.
		public bool Closing => dir < 0;

		// Fully on and not animating: draw the panel normally.
		public bool Settled => dir == 0 && Open >= 1f;

		public void Show()
		{
			if (Shown && dir >= 0)
				return;

			// If it was still turning off, it turns back on from where it is.
			if (!Shown)
				Open = 0f;
			Shown = true;
			dir = 1;
		}

		public void Hide()
		{
			if (!Shown || dir < 0)
				return;

			dir = -1;
		}

		// Closes at once, with no effect (for when another panel takes its place).
		public void HideInstant()
		{
			Shown = false;
			dir = 0;
			Open = 0f;
		}

		// Returns true once, on the frame the turn off finishes.
		public bool Tick(float dt)
		{
			if (dir == 0)
				return false;

			if (dir > 0)
			{
				Open += dt / OnSeconds;
				if (Open >= 1f)
				{
					Open = 1f;
					dir = 0;
				}

				return false;
			}

			Open -= dt / OffSeconds;
			if (Open <= 0f)
			{
				Open = 0f;
				dir = 0;
				Shown = false;
				return true;
			}

			return false;
		}
	}
}
