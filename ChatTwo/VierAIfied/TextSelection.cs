// VierAI-fied (AI generated code): character-level click-and-drag selection over wrapped chat text.
// Pure bookkeeping (no ImGui calls; text width comes in as a delegate) so it can be unit tested.
// The draw code records one Seg per drawn piece of text (a wrapped line of one chunk) with its screen box and its
// character offset in the message's plain text (sender text + content text). Positions are (message id, char offset),
// so a selection survives scrolling and works across lines and messages.
using System.Text.RegularExpressions;

namespace VierAIfiedShared;

public sealed class TextSelection
{
    /// <summary>A drawn piece of text. Start = offset in the message's plain text; Src[SrcStart..SrcStart+Len] is what was drawn.</summary>
    public readonly record struct Seg(Guid Id, int Start, string Src, int SrcStart, int Len, float X, float Y, float W, float H);
    public readonly record struct Pos(Guid Id, int Char);

    private List<Seg> segs = new(128);
    private List<Seg> lastSegs = new(128);
    public IReadOnlyList<Seg> Segs => segs;
    /// <summary>Last frame's segments, for decisions taken before this frame's text is drawn (window NoMove).</summary>
    public IReadOnlyList<Seg> LastSegs => lastSegs;

    public Pos? Anchor { get; private set; }
    public Pos? End { get; private set; }
    public bool HasSelection => Anchor is { } a && End is { } e && a != e;

    public bool PressedOnText { get; private set; }
    public bool Dragging { get; private set; }
    /// <summary>True while a left release must not activate a link (a drag happened, or the press was elsewhere).</summary>
    public bool BlockRelease { get; private set; } = true;
    private Pos? pressPos;
    private bool inRange;

    public void BeginFrame()
    {
        (segs, lastSegs) = (lastSegs, segs);
        segs.Clear();
        inRange = false;
    }

    public void AddSeg(Seg s) => segs.Add(s);

    public void Clear() { Anchor = null; End = null; }

    /// <summary>Test hook / programmatic selection.</summary>
    public void Set(Pos a, Pos e) { Anchor = a; End = e; }

    /// <summary>Call once per message in display order (including rows that are not drawn).
    /// Returns whether part of it is selected; [from, to) is the selected char range (to = int.MaxValue: to the end).</summary>
    public bool Step(Guid id, out int from, out int to)
    {
        from = 0; to = int.MaxValue;
        if (Anchor is not { } a || End is not { } e || a == e)
            return false;
        var isA = id == a.Id; var isE = id == e.Id;
        if (isA && isE) { from = Math.Min(a.Char, e.Char); to = Math.Max(a.Char, e.Char); return true; }
        if (isA || isE)
        {
            var c = isA ? a.Char : e.Char;
            if (!inRange) { inRange = true; from = c; }
            else { inRange = false; to = c; }
            return true;
        }
        return inRange;
    }

    /// <summary>X of the boundary before char i (0..Len) of a segment, scaled so the full text spans exactly W.</summary>
    public static float XAt(Seg s, int i, Func<string, float> measure)
    {
        if (i <= 0 || s.Len <= 0) return s.X;
        if (i >= s.Len) return s.X + s.W;
        var full = measure(s.Src.Substring(s.SrcStart, s.Len));
        var part = measure(s.Src.Substring(s.SrcStart, i));
        return s.X + (full > 0 ? part / full * s.W : part);
    }

    /// <summary>Char offset (in the message plain text) nearest to x inside one segment.</summary>
    public static int CharIn(Seg s, float x, Func<string, float> measure)
    {
        if (x <= s.X || s.Len == 0) return s.Start;
        if (x >= s.X + s.W) return s.Start + s.Len;
        var full = measure(s.Src.Substring(s.SrcStart, s.Len));
        var k = full > 0 ? s.W / full : 1f;
        float prev = 0;
        for (var i = 1; i <= s.Len; i++)
        {
            var cur = measure(s.Src.Substring(s.SrcStart, i)) * k;
            if (x < s.X + (prev + cur) / 2) return s.Start + i - 1;
            prev = cur;
        }
        return s.Start + s.Len;
    }

    /// <summary>Exact hit: the segment box contains the point (no padding). -1 if none.</summary>
    public static int SegAt(IReadOnlyList<Seg> segs, float x, float y)
    {
        for (var i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            if (y >= s.Y && y < s.Y + s.H && x >= s.X && x < s.X + s.W) return i;
        }
        return -1;
    }

    /// <summary>On or near text: inside a segment box grown by padX / padY.</summary>
    public static bool NearText(IReadOnlyList<Seg> segs, float x, float y, float padX, float padY)
    {
        for (var i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            if (y >= s.Y - padY && y < s.Y + s.H + padY && x >= s.X - padX && x <= s.X + s.W + padX) return true;
        }
        return false;
    }

    /// <summary>Position under the point. clamp = false: only an exact segment hit. clamp = true (dragging): the nearest
    /// visual line (above the first line = start of the first segment, below the last = end of the last), then the
    /// nearest char on that line (left of the line = its start, right of it = its end).</summary>
    public static Pos? Hit(IReadOnlyList<Seg> segs, float x, float y, Func<string, float> measure, bool clamp)
    {
        if (segs.Count == 0) return null;
        var exact = SegAt(segs, x, y);
        if (exact >= 0) { var s = segs[exact]; return new Pos(s.Id, CharIn(s, x, measure)); }
        if (!clamp) return null;

        float minY = float.MaxValue, maxY = float.MinValue;
        int firstI = 0, lastI = 0;
        for (var i = 0; i < segs.Count; i++)
        {
            if (segs[i].Y < minY) { minY = segs[i].Y; firstI = i; }
            if (segs[i].Y + segs[i].H > maxY || (segs[i].Y + segs[i].H == maxY && i > lastI)) { maxY = segs[i].Y + segs[i].H; lastI = i; }
        }
        if (y < minY) { var f = segs[firstI]; return new Pos(f.Id, f.Start); }
        if (y >= maxY) { var l = segs[lastI]; return new Pos(l.Id, l.Start + l.Len); }

        // nearest line by vertical distance (0 inside the band)
        var best = -1; var bestD = float.MaxValue;
        for (var i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            var d = y < s.Y ? s.Y - y : y >= s.Y + s.H ? y - (s.Y + s.H) : 0f;
            if (d < bestD) { bestD = d; best = i; }
        }
        var band = segs[best];
        // segments on that line: same Y band; pick the nearest horizontally
        int left = -1, right = -1;
        for (var i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            if (MathF.Abs(s.Y - band.Y) > 0.5f) continue;
            if (left < 0 || s.X < segs[left].X) left = i;
            if (right < 0 || s.X + s.W > segs[right].X + segs[right].W) right = i;
            if (x >= s.X && x < s.X + s.W) return new Pos(s.Id, CharIn(s, x, measure));
        }
        if (x < segs[left].X) return new Pos(segs[left].Id, segs[left].Start);
        if (x >= segs[right].X + segs[right].W) return new Pos(segs[right].Id, segs[right].Start + segs[right].Len);
        // in a gap between two pieces on the line: nearest edge
        Pos? bp = null; var bd = float.MaxValue;
        for (var i = 0; i < segs.Count; i++)
        {
            var s = segs[i];
            if (MathF.Abs(s.Y - band.Y) > 0.5f) continue;
            var dl = MathF.Abs(x - s.X); var dr = MathF.Abs(x - (s.X + s.W));
            if (dl < bd) { bd = dl; bp = new Pos(s.Id, s.Start); }
            if (dr < bd) { bd = dr; bp = new Pos(s.Id, s.Start + s.Len); }
        }
        return bp;
    }

    /// <summary>Feed the mouse once per frame after the text was drawn. hit = clamped position under the mouse.</summary>
    public void UpdateMouse(bool hovered, bool leftClicked, bool leftDragging, bool leftReleased, bool nearText, Pos? hit)
    {
        if (leftClicked)
        {
            PressedOnText = hovered && nearText;
            Dragging = false;
            BlockRelease = !hovered;
            if (hovered) Clear();
            if (PressedOnText) pressPos = hit;
        }
        if (PressedOnText && leftDragging)
        {
            if (!Dragging) { Dragging = true; Anchor = pressPos; }
            BlockRelease = true;
            End = hit ?? End;
            Anchor ??= End;
        }
        if (leftReleased)
        {
            PressedOnText = false;
            Dragging = false;
            BlockRelease = true;
        }
    }

    /// <summary>Plain text of the selection: one line per message, partial first and last messages. linePrefix (for
    /// example "Name: ") is added to a message only when the selection spans several messages and starts at its beginning.</summary>
    public string BuildText<T>(IReadOnlyList<T> items, Func<T, Guid> id, Func<T, string> text, out int lines, Func<T, string>? linePrefix = null)
    {
        lines = 0;
        if (Anchor is not { } a || End is not { } e || a == e) return string.Empty;
        int ia = -1, ie = -1;
        for (var i = 0; i < items.Count; i++)
        {
            var g = id(items[i]);
            if (g == a.Id) ia = i;
            if (g == e.Id) ie = i;
        }
        if (ia < 0 || ie < 0) return string.Empty;
        var (p, q) = ia < ie || (ia == ie && a.Char <= e.Char) ? (a, e) : (e, a);
        var (from, to) = ia <= ie ? (ia, ie) : (ie, ia);
        var sb = new System.Text.StringBuilder();
        for (var i = from; i <= to; i++)
        {
            var t = text(items[i]);
            var s = i == from ? Math.Clamp(p.Char, 0, t.Length) : 0;
            var en = i == to ? Math.Clamp(q.Char, 0, t.Length) : t.Length;
            if (i > from) sb.Append('\n');
            if (linePrefix != null && from != to && s == 0 && en > s) sb.Append(linePrefix(items[i]));
            if (en > s) sb.Append(t, s, en - s);
        }
        lines = to - from + 1;
        return sb.ToString();
    }

    private static readonly Regex UrlRx = new(@"(?:https?://|www\.)[^\s<>""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The URL containing char ch of text (several URLs per line are fine), or null.</summary>
    public static string? UrlAt(string text, int ch)
    {
        foreach (Match m in UrlRx.Matches(text))
        {
            var v = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '\'', '>');
            // keep a closing paren that belongs to the URL, e.g. wiki links with (disambiguation)
            if (m.Value.Length > v.Length && m.Value[v.Length] == ')' && v.Contains('(')) v += ")";
            if (ch >= m.Index && ch <= m.Index + v.Length && v.Length > 0) return v;
        }
        return null;
    }
}