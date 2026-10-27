// VierAI-fied fork support (AI generated code).
// Auto-translate phrases in the input box (the phrase wrapped in the game's arrow glyphs U+E040 ... U+E041)
// behave as one block:
//  - the caret cannot sit inside one (clicks and arrow keys jump to the nearer / far edge),
//  - a selection that touches one grows to cover the whole phrase,
//  - Backspace or Delete on any part of a phrase (including either arrow) removes the whole phrase.
// Call Apply from the input's ImGui callback on the CallbackAlways event. Works on ImGui's own buffer.
using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;

namespace VierAIfiedShared;

internal sealed unsafe class AtomicPhrases
{
    // UTF-8 for U+E040 / U+E041.
    private static readonly byte[] OpenBytes = [0xEE, 0x81, 0x80];
    private static readonly byte[] CloseBytes = [0xEE, 0x81, 0x81];

    private byte[] Prev = [];
    private int PrevCursor;

    internal void Reset()
    {
        Prev = [];
        PrevCursor = 0;
    }

    internal void Apply(ref ImGuiInputTextCallbackData data)
    {
        try
        {
            ApplyInner(ref data);
        }
        catch
        {
            Reset();
        }
    }

    private void ApplyInner(ref ImGuiInputTextCallbackData data)
    {
        var len = data.BufTextLen;
        var cur = new byte[len];
        for(var i = 0; i < len; i++)
        {
            var b = data.Buf[i];
            cur[i] = b == (byte)'\n' ? (byte)' ' : b; // XIM's multiline mode wraps by swapping spaces and newlines
        }

        // 1. Backspace / Delete that removed only part of a phrase -> remove the whole phrase.
        var deleting = ImGui.IsKeyDown(ImGuiKey.Backspace) || ImGui.IsKeyDown(ImGuiKey.Delete);
        if(deleting && Prev.Length > 0 && cur.Length < Prev.Length && !Same(cur, Prev))
        {
            var p = 0;
            var max = Math.Min(cur.Length, Prev.Length);
            while(p < max && cur[p] == Prev[p]) p++;
            var s = 0;
            while(s < max - p && cur[cur.Length - 1 - s] == Prev[Prev.Length - 1 - s]) s++;
            var rs = p;
            var re = Prev.Length - s;
            var start = rs;
            var end = re;
            var touched = false;
            foreach(var (o, e) in Spans(Prev))
            {
                if(rs < e && re > o && (rs > o || re < e))
                {
                    touched = true;
                    start = Math.Min(start, o);
                    end = Math.Max(end, e);
                }
            }
            if(touched)
            {
                var inserted = cur.AsSpan(p, cur.Length - s - p);
                var result = new byte[start + inserted.Length + (Prev.Length - end)];
                Prev.AsSpan(0, start).CopyTo(result);
                inserted.CopyTo(result.AsSpan(start));
                Prev.AsSpan(end).CopyTo(result.AsSpan(start + inserted.Length));
                if(result.Length < data.BufSize)
                {
                    for(var i = 0; i < result.Length; i++) data.Buf[i] = result[i];
                    data.Buf[result.Length] = 0;
                    data.BufTextLen = result.Length;
                    data.BufDirty = 1;
                    data.CursorPos = start + inserted.Length;
                    data.SelectionStart = data.SelectionEnd = data.CursorPos;
                    cur = result;
                }
            }
        }

        // 2. Keep the caret and selection outside phrases.
        var spans = Spans(cur);
        if(spans.Count > 0)
        {
            if(data.SelectionStart != data.SelectionEnd)
            {
                var a = data.SelectionStart;
                var b = data.SelectionEnd;
                var lo = Math.Min(a, b);
                var hi = Math.Max(a, b);
                foreach(var (o, e) in spans)
                {
                    if(lo > o && lo < e) lo = o;
                    if(hi > o && hi < e) hi = e;
                }
                var cursorAtA = data.CursorPos == a;
                if(a <= b) { a = lo; b = hi; } else { a = hi; b = lo; }
                data.SelectionStart = a;
                data.SelectionEnd = b;
                data.CursorPos = cursorAtA ? a : b;
            }
            else
            {
                var c = data.CursorPos;
                foreach(var (o, e) in spans)
                {
                    if(c > o && c < e)
                    {
                        if(PrevCursor <= o) c = e;
                        else if(PrevCursor >= e) c = o;
                        else c = c - o <= e - c ? o : e;
                        data.CursorPos = c;
                        data.SelectionStart = data.SelectionEnd = c;
                        break;
                    }
                }
            }
        }

        Prev = cur;
        PrevCursor = data.CursorPos;
    }

    private static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    // Byte ranges [start, end) of complete arrow-wrapped phrases.
    private static List<(int Start, int End)> Spans(byte[] buf)
    {
        var list = new List<(int, int)>();
        var i = 0;
        while(i <= buf.Length - 3)
        {
            if(Is(buf, i, OpenBytes))
            {
                var j = i + 3;
                var found = -1;
                while(j <= buf.Length - 3)
                {
                    if(Is(buf, j, CloseBytes)) { found = j; break; }
                    if(Is(buf, j, OpenBytes)) break;
                    j++;
                }
                if(found >= 0)
                {
                    list.Add((i, found + 3));
                    i = found + 3;
                    continue;
                }
                i = j;
                continue;
            }
            i++;
        }
        return list;
    }

    private static bool Is(byte[] buf, int at, byte[] seq) => buf[at] == seq[0] && buf[at + 1] == seq[1] && buf[at + 2] == seq[2];
}
