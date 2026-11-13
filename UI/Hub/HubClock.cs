using System.Diagnostics;

namespace Augments
{
	// A smooth clock for animations. Environment.TickCount64 only advances in steps of about 10 to 16 ms, which at 60 frames a second
	// makes some frames see the same time and others a double step, so motion visibly stutters. A Stopwatch has no such steps.
	internal static class HubClock
	{
		private static readonly Stopwatch watch = Stopwatch.StartNew();

		public static float Seconds => (float)watch.Elapsed.TotalSeconds;
	}
}