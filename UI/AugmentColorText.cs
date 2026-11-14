using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria.UI.Chat;

namespace Augments
{
    // Word-wraps text containing [c/RRGGBB:...] color tags. A "word" here is
    // a maximal run of non-space characters, same as plain text wrapping -
    // but a single word can be made of several differently-colored pieces
    // (e.g. "HP]," is a colored "HP" piece glued to an uncolored ","), and a
    // tag that spans MULTIPLE space-separated words (e.g. a whole colored
    // sentence) is split into one word per piece so it can still wrap
    // between those words instead of becoming one unbreakable, overflowing
    // blob. Explicit \n in the source text is honored as a hard line break.
    // Shared so every place that renders augment text via ChatManager uses
    // the same proven wrap logic instead of a second copy that can drift.
    public static class AugmentColorText
    {
        private readonly struct ColoredPiece
        {
            public readonly string Text;
            public readonly string ColorHex;

            public ColoredPiece(string text, string colorHex)
            {
                Text = text;
                ColorHex = colorHex;
            }

            public string ToTag()
            {
                return ColorHex == null ? Text : $"[c/{ColorHex}:{Text}]";
            }
        }

        private sealed class Word
        {
            public readonly List<ColoredPiece> Pieces = new List<ColoredPiece>();
            public bool IsLineBreak;
        }

        public static List<string> Wrap(DynamicSpriteFont font, string text, float maxWidth, Vector2 scale)
        {
            var lines = new List<string>();
            var words = SplitIntoWords(text);
            var currentLine = new StringBuilder();
            float currentLineWidth = 0f;
            float spaceWidth = ModText.Size(font, " ", scale).X;

            foreach (var word in words)
            {
                if (word.IsLineBreak)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLineWidth = 0f;
                    continue;
                }

                string plainWord = BuildPlainText(word);
                string taggedWord = BuildTaggedText(word);
                float wordWidth = ModText.Size(font, plainWord, scale).X;

                if (currentLine.Length > 0 && currentLineWidth + spaceWidth + wordWidth > maxWidth)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLineWidth = 0f;
                }

                if (currentLine.Length > 0)
                {
                    currentLine.Append(' ');
                    currentLineWidth += spaceWidth;
                }

                currentLine.Append(taggedWord);
                currentLineWidth += wordWidth;
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString());

            return lines;
        }

        private static string BuildPlainText(Word word)
        {
            var sb = new StringBuilder();
            foreach (var piece in word.Pieces)
                sb.Append(piece.Text);
            return sb.ToString();
        }

        private static string BuildTaggedText(Word word)
        {
            var sb = new StringBuilder();
            foreach (var piece in word.Pieces)
                sb.Append(piece.ToTag());
            return sb.ToString();
        }

        // Walks the raw text once, tracking whichever [c/HEX:...] tag is
        // currently open. Spaces and \n always end the current word (even
        // mid-tag), which is what lets a long colored sentence still wrap
        // between its words. Closing a tag does NOT by itself end a word -
        // that's what keeps punctuation glued directly after a tag (no
        // original space) attached to the same word instead of getting an
        // artificial space inserted before it.
        private static List<Word> SplitIntoWords(string text)
        {
            var words = new List<Word>();
            var current = new Word();
            var pieceText = new StringBuilder();
            string activeColor = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '[' && i + 2 < text.Length && text[i + 1] == 'c' && text[i + 2] == '/')
                {
                    int colonIndex = text.IndexOf(':', i + 3);
                    if (colonIndex >= 0)
                    {
                        FlushPiece(current, pieceText, activeColor);
                        activeColor = text.Substring(i + 3, colonIndex - (i + 3));
                        i = colonIndex;
                        continue;
                    }
                }

                if (c == ']' && activeColor != null)
                {
                    FlushPiece(current, pieceText, activeColor);
                    activeColor = null;
                    continue;
                }

                if (c == '\n' || c == ' ')
                {
                    FlushPiece(current, pieceText, activeColor);
                    if (current.Pieces.Count > 0)
                    {
                        words.Add(current);
                        current = new Word();
                    }
                    if (c == '\n')
                        words.Add(new Word { IsLineBreak = true });
                    continue;
                }

                pieceText.Append(c);
            }

            FlushPiece(current, pieceText, activeColor);
            if (current.Pieces.Count > 0)
                words.Add(current);

            return words;
        }

        private static void FlushPiece(Word word, StringBuilder pieceText, string color)
        {
            if (pieceText.Length == 0)
                return;

            word.Pieces.Add(new ColoredPiece(pieceText.ToString(), color));
            pieceText.Clear();
        }
    }
}
