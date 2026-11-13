using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Terraria;
using Terraria.ModLoader;

namespace Augments
{
	// Plays the optional showcase clip for a slideshow slide: Assets/Showcase/<plugin id>.ogv inside the mod.
	//
	// HOW IT WORKS
	//   * tModLoader's graphics library (FNA) can only play Ogg Theora video (.ogv), through its VideoPlayer class.
	//   * VideoPlayer wants a Video object, and Video can only be created from a file on disk through a constructor
	//     the library keeps internal. So while the mod loads (Prepare), every clip is copied out of the packed mod
	//     file into a cache folder (Terraria save folder / AugmentsCache), and the Video is created with reflection
	//     the first time its slide is shown, then reused.
	//   * Only one clip plays at a time, muted and looping, and only while its slide is on screen.
	//   * Frames are fetched during the UI update step, never during drawing (see HomeSlideshow.UpdateContent).
	//
	// SAFETY. Nothing here may stop the title screen from working.
	//   * A clip must start with the Ogg signature and contain the Theora marker, or it is skipped. This matters
	//     because an exception handler cannot catch a crash inside the native decoder, and a renamed .mp4 would
	//     otherwise be handed straight to it.
	//   * A clip that fails is remembered and skipped (the slide falls back to its still image or emblem), each
	//     failure is written to the game log once, and after two failures video is switched off for the session.
	internal static class ShowcaseVideo
	{
		private static readonly Dictionary<string, string> cachedFiles = new Dictionary<string, string>(); // id -> cache file
		private static readonly Dictionary<string, Video> videos = new Dictionary<string, Video>();        // one Video per clip, reused
		private static readonly HashSet<string> badClips = new HashSet<string>();

		private static VideoPlayer player;
		private static string currentId;
		private static int failures;
		private static bool disabled;

		// --- Loading (runs while the mod loads, never on the title screen) ---

		public static void Prepare(Mod mod)
		{
			try
			{
				string dir = Path.Combine(Main.SavePath, "AugmentsCache");
				foreach (string asset in mod.GetFileNames())
				{
					string normalized = asset.Replace('\\', '/');
					if (!normalized.StartsWith("Assets/Showcase/", StringComparison.OrdinalIgnoreCase) ||
						!normalized.EndsWith(".ogv", StringComparison.OrdinalIgnoreCase))
						continue;

					string id = Path.GetFileNameWithoutExtension(normalized);
					try
					{
						cachedFiles[id] = Extract(mod, asset, id, dir);
					}
					catch (Exception e)
					{
						badClips.Add(id);
						mod.Logger.Warn("Showcase video '" + id + "' could not be prepared and will be skipped: " + e.Message);
					}
				}
			}
			catch (Exception e)
			{
				mod.Logger.Warn("Showcase videos could not be prepared: " + e.Message);
			}
		}

		// Copies one clip to the cache folder. The file name includes a hash of the contents, so a re-recorded clip
		// gets a new file name and an old file that is still locked by the game can never block it.
		private static string Extract(Mod mod, string asset, string id, string dir)
		{
			byte[] bytes = mod.GetFileBytes(asset);
			if (!LooksLikeTheora(bytes))
				throw new InvalidDataException("not an Ogg Theora (.ogv) file. Convert it with ffmpeg (-c:v libtheora).");

			string hash;
			using (SHA1 sha = SHA1.Create())
				hash = BitConverter.ToString(sha.ComputeHash(bytes), 0, 4).Replace("-", "");

			Directory.CreateDirectory(dir);
			string file = Path.Combine(dir, id + "_" + hash + ".ogv");
			if (!File.Exists(file) || new FileInfo(file).Length != bytes.Length)
				File.WriteAllBytes(file, bytes);

			// Remove older copies of this clip (best effort; a locked file is simply left alone).
			foreach (string old in Directory.GetFiles(dir, id + "_*.ogv"))
			{
				if (!string.Equals(old, file, StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						File.Delete(old);
					}
					catch
					{
					}
				}
			}

			return file;
		}

		private static bool LooksLikeTheora(byte[] bytes)
		{
			if (bytes == null || bytes.Length < 64)
				return false;

			if (bytes[0] != (byte)'O' || bytes[1] != (byte)'g' || bytes[2] != (byte)'g' || bytes[3] != (byte)'S')
				return false;

			// The Theora identification header begins with 0x80 followed by "theora" near the start of the stream.
			int scan = Math.Min(bytes.Length, 512);
			string head = Encoding.ASCII.GetString(bytes, 0, scan);
			return head.Contains("theora");
		}

		// --- Playing ---

		// True when this plugin has a usable clip.
		public static bool HasClip(string id)
		{
			return !disabled && !string.IsNullOrEmpty(id) && !badClips.Contains(id) && cachedFiles.ContainsKey(id);
		}

		// Length of the clip in seconds, or 0 when it is not known yet (it is read from the video once the clip
		// has been started for the first time).
		public static float GetDurationSeconds(string id)
		{
			try
			{
				if (id != null && videos.TryGetValue(id, out Video video))
				{
					double seconds = video.Duration.TotalSeconds;
					return seconds > 0.0 ? (float)seconds : 0f;
				}
			}
			catch
			{
			}

			return 0f;
		}

		// Returns the current frame of the clip for this plugin, or null when there is no clip, video is
		// switched off, or the first frame is not decoded yet. Call it from the update step, not from drawing.
		public static Texture2D GetFrame(string id)
		{
			if (disabled || string.IsNullOrEmpty(id) || badClips.Contains(id) || !cachedFiles.ContainsKey(id))
				return null;

			try
			{
				if (id != currentId)
				{
					Stop();
					Start(id);
				}

				return player?.GetTexture();
			}
			catch (Exception e)
			{
				Fail(id, e);
				return null;
			}
		}

		// Stops whatever is playing. Safe to call at any time, including when nothing is playing.
		public static void Stop()
		{
			try
			{
				if (player != null && currentId != null)
					player.Stop();
			}
			catch
			{
			}

			currentId = null;
		}

		public static void Unload()
		{
			Stop();
			try
			{
				player?.Dispose();
			}
			catch
			{
			}

			player = null;
			videos.Clear();
			cachedFiles.Clear();
			badClips.Clear();
			failures = 0;
			disabled = false;
		}

		private static void Start(string id)
		{
			if (!videos.TryGetValue(id, out Video video))
			{
				video = CreateVideo(cachedFiles[id]);
				videos[id] = video;
			}

			if (player == null)
				player = new VideoPlayer();
			player.IsLooped = true;
			player.IsMuted = true;
			player.Volume = 0f;
			player.Play(video);

			currentId = id;
		}

		// The library only offers an internal constructor (file path, graphics device), so call it by reflection.
		private static Video CreateVideo(string file)
		{
			ConstructorInfo ctor = typeof(Video).GetConstructor(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				new[] { typeof(string), typeof(GraphicsDevice) },
				null);

			if (ctor == null)
				throw new MissingMethodException("The video constructor was not found in this tModLoader version.");

			return (Video)ctor.Invoke(new object[] { file, Main.graphics.GraphicsDevice });
		}

		private static void Fail(string id, Exception e)
		{
			badClips.Add(id);
			videos.Remove(id);
			failures++;
			if (failures >= 2)
				disabled = true;

			currentId = null;
			try
			{
				player?.Stop();
			}
			catch
			{
			}

			ModContent.GetInstance<Augments>().Logger.Warn("Showcase video '" + id + "' failed, showing the still image instead" +
				(disabled ? " (video is now switched off for this session)" : "") + ": " + e);
		}
	}
}
