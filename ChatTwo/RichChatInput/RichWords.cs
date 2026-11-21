// RichChatInput - shared chat input editor. MIT License, Copyright (c) 2026 VierAI. AI generated code.
// Text rules for the editor: words, auto-translate phrases, soft-wrap break points, paste cleaning, UTF-8 offsets.
// No ImGui in this file, so the tests can use it.
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace RichChatInput;

internal static class RichWords
{
    internal enum Kind { Space, Word, Punct }

    // Punctuation that ends a word. Anything else that is not white space (letters, digits, ', -, /, @, CJK, game glyphs)
    // is part of a word, so "don't", "/tell" and "Name@World" stay one word.
    private const string PunctChars = ",;:.!?()[]{}<>|\"\u3001\u3002\u300C\u300D\u300E\u300F\uFF08\uFF09\uFF01\uFF1F\uFF1A\uFF1B\uFF0C\uFF0E";

    /// <summary>The game's auto-translate phrase markers: a phrase is everything from PhraseOpen to PhraseClose.</summary>
    internal const char PhraseOpen = '\uE040', PhraseClose = '\uE041';

    internal static Kind KindOf(char c)
    {
        if (char.IsWhiteSpace(c)) return Kind.Space;
        return PunctChars.IndexOf(c) >= 0 ? Kind.Punct : Kind.Word;
    }

    private static bool IsMarker(char c) => c == PhraseOpen || c == PhraseClose;

    /// <summary>The auto-translate phrase [Start, End) that contains character i, or null.</summary>
    internal static (int Start, int End)? PhraseAt(string s, int i)
    {
        if (i < 0 || i >= s.Length) return null;
        var open = s.LastIndexOf(PhraseOpen, i);
        if (open < 0) return null;
        var close = s.IndexOf(PhraseClose, open + 1);
        if (close < i) return null; // also covers close == -1
        return (open, close + 1);
    }

    /// <summary>The run of same-kind characters that contains character i (0 &lt;= i &lt; length), as [Start, End).
    /// An auto-translate phrase is one run. A surrogate pair is never split.</summary>
    internal static (int Start, int End) RunAt(string s, int i)
    {
        if (s.Length == 0) return (0, 0);
        i = Math.Clamp(i, 0, s.Length - 1);
        if (PhraseAt(s, i) is { } p) return p;
        var k = KindOf(s[i]);
        int a = i, b = i + 1;
        while (a > 0 && KindOf(s[a - 1]) == k && !IsMarker(s[a - 1])) a--;
        while (b < s.Length && KindOf(s[b]) == k && !IsMarker(s[b])) b++;
        return (a, b);
    }

    /// <summary>Selection for a word drag. anchor = the double-clicked word [anchorStart, anchorEnd); pos = the caret under
    /// the mouse (0..length). Returns the fixed end, the moving end and the caret (= moving end). Right of the anchor the
    /// selection ends after the run left of pos; left of it, it starts at the run right of pos; over the anchor it is the
    /// anchor itself. Feeding the caret back in gives the same result.</summary>
    internal static (int Fixed, int Moving, int Caret) Extend(string s, int anchorStart, int anchorEnd, int pos)
    {
        var len = s.Length;
        anchorStart = Math.Clamp(anchorStart, 0, len); anchorEnd = Math.Clamp(anchorEnd, anchorStart, len);
        pos = Math.Clamp(pos, 0, len);
        if (pos > anchorEnd)
        {
            var end = Math.Max(anchorEnd, RunAt(s, pos - 1).End);
            return (anchorStart, end, end);
        }
        if (pos < anchorStart)
        {
            var start = Math.Min(anchorStart, RunAt(s, pos).Start);
            return (anchorEnd, start, start);
        }
        return (anchorStart, anchorEnd, anchorEnd);
    }

    /// <summary>Ctrl+Delete: where the deletion that starts at caret i ends. On spaces only the spaces go; on a word,
    /// punctuation run or auto-translate phrase, the rest of it and the spaces after it go.</summary>
    internal static int DeleteWordRightEnd(string s, int i)
    {
        if (i < 0) i = 0;
        if (i >= s.Length) return s.Length;
        var run = RunAt(s, i);
        if (KindOf(s[i]) == Kind.Space && PhraseAt(s, i) == null) return run.End;
        var end = run.End;
        while (end < s.Length && KindOf(s[end]) == Kind.Space) end++;
        return end;
    }

    /// <summary>Line breaks shown in a soft-wrapped input are spaces in the text itself.</summary>
    internal static string Normalize(string s) =>
        s.IndexOf('\n') < 0 && s.IndexOf('\r') < 0 ? s : s.Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>Greedy soft wrap: indexes of the spaces that become line breaks so that no line is wider than width
    /// (a single word wider than that keeps its own line). measure gives the width of a piece of text.</summary>
    internal static List<int> WrapBreaks(string s, float width, Func<string, float> measure)
    {
        var breaks = new List<int>();
        if (s.Length == 0 || width <= 0) return breaks;
        var spaceW = measure(" ");
        float line = 0; var empty = true; var lastSpace = -1; var wordStart = 0;
        for (var i = 0; i <= s.Length; i++)
        {
            if (i < s.Length && s[i] != ' ') continue;
            var w = i > wordStart ? measure(s.Substring(wordStart, i - wordStart)) : 0f;
            if (!empty && lastSpace >= 0 && line + spaceW + w > width) { breaks.Add(lastSpace); line = w; }
            else line += (empty ? 0f : spaceW) + w;
            empty = false;
            lastSpace = i; wordStart = i + 1;
        }
        return breaks;
    }

    /// <summary>s with the given spaces turned into '\n' (same length, same UTF-8 byte offsets).</summary>
    internal static string ApplyBreaks(string s, List<int> breaks)
    {
        if (breaks.Count == 0) return s;
        var c = s.ToCharArray();
        foreach (var b in breaks) if (b >= 0 && b < c.Length && c[b] == ' ') c[b] = '\n';
        return new string(c);
    }

    /// <summary>0-based line of UTF-8 byte offset off in a display text (lines end at '\n').</summary>
    internal static int LineOfByte(byte[] display, int off)
    {
        off = Math.Clamp(off, 0, display.Length);
        var n = 0;
        for (var i = 0; i < off; i++) if (display[i] == (byte)'\n') n++;
        return n;
    }

    internal static int LineCount(byte[] display)
    {
        var n = 1;
        foreach (var b in display) if (b == (byte)'\n') n++;
        return n;
    }

    /// <summary>Pasted text for a one-line chat input: line breaks and tabs become spaces, other control characters
    /// and characters the caller does not allow are dropped.</summary>
    internal static string CleanPaste(string s, Func<char, bool>? allowed)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\r') { if (i + 1 < s.Length && s[i + 1] == '\n') i++; c = ' '; }
            else if (c == '\n' || c == '\t') c = ' ';
            else if (char.IsControl(c)) continue;
            if (allowed != null && !char.IsSurrogate(c) && !allowed(c)) continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>The longest start of s that fits in maxBytes of UTF-8 without splitting a character.</summary>
    internal static string FitBytes(string s, int maxBytes)
    {
        if (maxBytes <= 0) return "";
        if (Encoding.UTF8.GetByteCount(s) <= maxBytes) return s;
        var n = 0; var i = 0;
        while (i < s.Length)
        {
            var step = char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;
            var b = Encoding.UTF8.GetByteCount(s.AsSpan(i, step));
            if (n + b > maxBytes) break;
            n += b; i += step;
        }
        return s[..i];
    }

    /// <summary>UTF-8 byte offset to char index (clamped; an offset inside a character rounds down).</summary>
    internal static int CharIndex(byte[] utf8, int byteOffset)
    {
        byteOffset = Math.Clamp(byteOffset, 0, utf8.Length);
        while (byteOffset > 0 && byteOffset < utf8.Length && (utf8[byteOffset] & 0xC0) == 0x80) byteOffset--;
        return Encoding.UTF8.GetCharCount(utf8, 0, byteOffset);
    }

    /// <summary>Char index to UTF-8 byte offset.</summary>
    internal static int ByteOffset(string s, int charIndex) =>
        Encoding.UTF8.GetByteCount(s.AsSpan(0, Math.Clamp(charIndex, 0, s.Length)));

    /// <summary>True when b is a with exactly one character typed (not a space): such edits are merged into one undo step.</summary>
    internal static bool IsTypingStep(string a, string b)
    {
        if (b.Length != a.Length + 1) return false;
        var p = 0;
        while (p < a.Length && a[p] == b[p]) p++;
        if (a.AsSpan(p).SequenceEqual(b.AsSpan(p + 1))) return !char.IsWhiteSpace(b[p]);
        return false;
    }

    /// <summary>Words to spell check in s, as [Start, Length) char spans: letter runs (with inner apostrophes) of two or
    /// more letters. Skipped: whole tokens that hold digits, '@', '_', '/', "://" or '.' between letters (commands,
    /// names, links, numbers), all-capital words, auto-translate phrases, and the token touching skipCaret.</summary>
    internal static List<(int Start, int Length)> SpellWords(string s, int skipCaret = -1)
    {
        var res = new List<(int, int)>();
        var i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (s[i] == PhraseOpen) { var close = s.IndexOf(PhraseClose, i + 1); i = close < 0 ? s.Length : close + 1; continue; }
            var t0 = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != PhraseOpen) i++;
            var t1 = i;
            if (skipCaret >= t0 && skipCaret <= t1) continue;
            var skip = false;
            for (var k = t0; k < t1 && !skip; k++)
            {
                var c = s[k];
                if (char.IsDigit(c) || c == '@' || c == '_' || c == '/' || c == '\\' || IsMarker(c)) skip = true;
                else if (c == '.' && k > t0 && k + 1 < t1 && char.IsLetter(s[k - 1]) && char.IsLetter(s[k + 1])) skip = true;
            }
            if (skip) continue;
            var k2 = t0;
            while (k2 < t1)
            {
                if (!char.IsLetter(s[k2])) { k2++; continue; }
                var w0 = k2;
                while (k2 < t1 && (char.IsLetter(s[k2]) || ((s[k2] == '\'' || s[k2] == '\u2019') && k2 + 1 < t1 && char.IsLetter(s[k2 + 1])))) k2++;
                var len = k2 - w0;
                if (len >= 2 && !IsAllUpper(s, w0, len)) res.Add((w0, len));
            }
        }
        return res;
    }

    private static bool IsAllUpper(string s, int start, int len)
    {
        for (var i = start; i < start + len; i++) if (char.IsLetter(s[i]) && !char.IsUpper(s[i])) return false;
        return true;
    }
}