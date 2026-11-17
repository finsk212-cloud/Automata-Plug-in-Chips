using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace Augments
{
	public enum PatchKind
	{
		Version,  // "## Version 1.3.0"
		Section,  // "### 1. New Feature: ..."
		Item,     // top-level bullet
		Sub,      // indented bullet
		Paragraph,
		Card,     // "#### Name | Rarity | TAG": one plugin change (see below)
		Quote,    // "> reason", the one-line why under a card
		Stat      // a bullet inside a card, such as "- Bug damage: 18 => 15"
	}

	public class PatchBlock
	{
		public PatchKind Kind;
		public string Title; // bold part of a bullet, may be empty
		public string Body;
		public int Level;    // indent depth of a Sub bullet

		// Plugin change cards, in the style of League of Legends item changes:
		//   #### Queen's Swarm | Rare | NERF
		//   > It was doing far too much damage.
		//   - Bug damage: 18 => 15          (the old value is dimmed and crossed out, then an arrow, then the new value)
		//   - [+] Now counts every boss part  ([-] nerf, [+] buff, [~] adjusted; without one, the card's tag decides)
		public string Rarity; // Card: Common, Rare, Epic or Legendary
		public string Tag;    // Card: NERF, BUFF, ADJUSTED...
		public int Dir;       // Card and Stat: -1 nerf, +1 buff, 0 adjusted
	}

	// The in-game patch notes, read from the mod's CHANGELOG.md once while the mod loads and kept in memory.
	// Reading it here, off the title screen's draw path, means a missing or odd file can only ever produce a
	// short error message on the patch notes page.
	public static class PatchNotesData
	{
		public static readonly List<PatchBlock> Blocks = new List<PatchBlock>();
		public static string Error;
		public static string VersionLabel = "";

		// The newest version in the file (the first "## Version" line), and whether this account has opened the patch notes since it came out.
		// What was last read is kept in the account file, so it is remembered for every character.
		public static string LatestVersionId = "";
		private const string ReadSlot = "patch_notes_read";

		public static bool IsUnread
			=> Error == null && Blocks.Count > 0 && LatestVersionId.Length > 0 && AccountStore.Equipped(ReadSlot, "") != LatestVersionId;

		public static void MarkRead()
		{
			if (IsUnread)
				AccountStore.Equip(ReadSlot, LatestVersionId);
		}

		private static readonly Regex BoldLead = new Regex(@"^\*\*(.+?)\*\*\s*(?:[:\-]\s*)?(.*)$", RegexOptions.Compiled);

		public static void Load(Mod mod)
		{
			Blocks.Clear();
			Error = null;
			VersionLabel = "";
			LatestVersionId = "";

						try
			{
				if (!mod.FileExists("CHANGELOG.md"))
				{
					Error = "CHANGELOG.md is not inside this build of the mod.";
					return;
				}

				string text = Encoding.UTF8.GetString(mod.GetFileBytes("CHANGELOG.md"));
				Parse(text);
				if (Blocks.Count == 0)
					Error = "The patch notes file is empty.";
			}
			catch (Exception e)
			{
				Blocks.Clear();
				Error = "The patch notes could not be read.";
				mod.Logger.Warn("Patch notes failed to load: " + e);
			}
		}

		private static int TagDirection(string tag)
		{
			switch (tag)
			{
				case "NERF": return -1;
				case "BUFF": return 1;
				default: return 0;
			}
		}

		private static void Parse(string text)
		{
			bool inCard = false;
			int cardDir = 0;

			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				string trimmed = line.TrimStart();
				if (trimmed.Length == 0)
					continue;

				if (trimmed.StartsWith("#### "))
				{
					string[] parts = trimmed.Substring(5).Split('|');
					string tag = parts.Length > 2 ? Clean(parts[2]).ToUpperInvariant() : "";
					cardDir = TagDirection(tag);
					inCard = true;
					Blocks.Add(new PatchBlock
					{
						Kind = PatchKind.Card,
						Title = Clean(parts[0]),
						Rarity = parts.Length > 1 ? Clean(parts[1]) : "",
						Tag = tag,
						Dir = cardDir
					});
				}
				else if (inCard && trimmed.StartsWith("> "))
				{
					Blocks.Add(new PatchBlock { Kind = PatchKind.Quote, Body = Clean(trimmed.Substring(2)) });
				}
				else if (inCard && (trimmed.StartsWith("- ") || trimmed.StartsWith("* ")))
				{
					string content = trimmed.Substring(2).Trim();
					int dir = cardDir;
					if (content.StartsWith("[+]")) { dir = 1; content = content.Substring(3); }
					else if (content.StartsWith("[-]")) { dir = -1; content = content.Substring(3); }
					else if (content.StartsWith("[~]")) { dir = 0; content = content.Substring(3); }
					Blocks.Add(new PatchBlock { Kind = PatchKind.Stat, Body = Clean(content), Dir = dir });
				}
				else if (trimmed.StartsWith("### "))
				{
					inCard = false;
					Blocks.Add(new PatchBlock { Kind = PatchKind.Section, Body = Clean(trimmed.Substring(4)) });
				}
				else if (trimmed.StartsWith("## "))
				{
					inCard = false;
					string v = Clean(trimmed.Substring(3));
					if (VersionLabel.Length == 0)
					{
						VersionLabel = v.ToUpperInvariant();
						LatestVersionId = v;
					}
					Blocks.Add(new PatchBlock { Kind = PatchKind.Version, Body = v });
				}
				else if (trimmed.StartsWith("# "))
				{
					// The document title is skipped; the page has its own heading.
				}
				else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
				{
					int indent = line.Length - trimmed.Length;
					string content = trimmed.Substring(2).Trim();
					string title = "";
					string body = content;
					Match m = BoldLead.Match(content);
					if (m.Success)
					{
						title = m.Groups[1].Value;
						body = m.Groups[2].Value;
					}

					Blocks.Add(new PatchBlock
					{
						Kind = indent == 0 ? PatchKind.Item : PatchKind.Sub,
						Level = Math.Min(3, Math.Max(1, indent / 2)),
						Title = Clean(title),
						Body = Clean(body)
					});
				}
				else
				{
					Blocks.Add(new PatchBlock { Kind = PatchKind.Paragraph, Body = Clean(trimmed) });
				}
			}
		}

		// Removes markdown marks and replaces characters the in-game font cannot draw.
		internal static string Clean(string s)
		{
			if (string.IsNullOrEmpty(s))
				return "";

			s = s.Replace("**", "").Replace("`", "").Replace("__", "");
			var sb = new StringBuilder(s.Length);
			foreach (char c in s)
			{
				switch (c)
				{
					case '—': case '–': sb.Append('-'); break;
					case '→': sb.Append("->"); break;
					case '×': sb.Append('x'); break;
					case '≥': sb.Append(">="); break;
					case '≤': sb.Append("<="); break;
					case '‘': case '’': sb.Append('\''); break;
					case '“': case '”': sb.Append('"'); break;
					case '•': sb.Append('-'); break;
					case '…': sb.Append("..."); break;
					default:
						if (c >= 32 && c < 127)
							sb.Append(c);
						break;
				}
			}

			return sb.ToString().Trim();
		}
	}

	// Scrollbar in the hub's colours. It reuses PanelScrollbar's handle geometry and dragging, and only draws differently.
	internal class HubScrollbar : PanelScrollbar
	{
		// When set and it returns false, the bar is hidden and cannot be clicked.
		public Func<bool> VisibleWhen;

		public override bool ContainsPoint(Vector2 point)
		{
			return (VisibleWhen == null || VisibleWhen()) && base.ContainsPoint(point);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			// A hidden parent still draws its children, so the bar must check for itself (this was the stray
			// scrollbar floating on the HOME screen).
			if (VisibleWhen != null && !VisibleWhen())
				return;

			UpdateDrag(); // makes the handle follow the mouse while it is held

			if (!CanScroll)
				return;

			CalculatedStyle dims = GetDimensions();
			var track = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
			bool hot = IsMouseHovering || IsDragging;

			HubDraw.Fill(spriteBatch, track, HubTheme.PanelAlt);
			HubDraw.Border(spriteBatch, track, HubTheme.LineSoft);
			HubDraw.Fill(spriteBatch, GetHandle(), hot ? HubTheme.Accent : HubTheme.Line);
		}
	}
}
