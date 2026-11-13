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
		Paragraph
	}

	public class PatchBlock
	{
		public PatchKind Kind;
		public string Title; // bold part of a bullet, may be empty
		public string Body;
		public int Level;    // indent depth of a Sub bullet
	}

	// The in-game patch notes, read from the mod's CHANGELOG.md once while the mod loads and kept in memory.
	// Reading it here, off the title screen's draw path, means a missing or odd file can only ever produce a
	// short error message on the patch notes page.
	public static class PatchNotesData
	{
		public static readonly List<PatchBlock> Blocks = new List<PatchBlock>();
		public static string Error;
		public static string VersionLabel = "";

		private static readonly Regex BoldLead = new Regex(@"^\*\*(.+?)\*\*\s*(?:[:\-]\s*)?(.*)$", RegexOptions.Compiled);

		public static void Load(Mod mod)
		{
			Blocks.Clear();
			Error = null;
			VersionLabel = "";

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

		private static void Parse(string text)
		{
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				string trimmed = line.TrimStart();
				if (trimmed.Length == 0)
					continue;

				if (trimmed.StartsWith("### "))
				{
					Blocks.Add(new PatchBlock { Kind = PatchKind.Section, Body = Clean(trimmed.Substring(4)) });
				}
				else if (trimmed.StartsWith("## "))
				{
					string v = Clean(trimmed.Substring(3));
					if (VersionLabel.Length == 0)
						VersionLabel = v.ToUpperInvariant();
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
