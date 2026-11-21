// RichChatInput - shared chat input editor. MIT License, Copyright (c) 2026 VierAI. AI generated code.
// Editor state that needs no ImGui: undo/redo, sent-message history, drafts, length bar colours, the spell checker hook.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace RichChatInput;

/// <summary>Text (line breaks already turned back into spaces) with caret and selection as UTF-8 byte offsets.</summary>
internal readonly record struct RichSnap(string Text, int Cursor, int SelStart, int SelEnd);

/// <summary>Undo/redo. Typing one character after another (no space, under 1.2 s apart) is one step.</summary>
internal sealed class RichUndo
{
    private readonly List<RichSnap> items = new();
    private int idx = -1;
    private long lastTime;
    private bool lastTyping;
    internal const int Max = 200;
    internal const long MergeMs = 1200;

    internal RichSnap? Current => idx >= 0 ? items[idx] : null;
    internal int Count => items.Count;
    internal bool CanUndo => idx > 0;
    internal bool CanRedo => idx >= 0 && idx < items.Count - 1;

    internal void Reset(RichSnap s) { items.Clear(); items.Add(s); idx = 0; lastTyping = false; }

    internal void Record(RichSnap s, long nowMs)
    {
        if (idx < 0) { Reset(s); return; }
        var cur = items[idx];
        if (cur.Text == s.Text) { items[idx] = s; return; } // caret moves are kept with the step
        var typing = RichWords.IsTypingStep(cur.Text, s.Text);
        if (typing && lastTyping && idx > 0 && nowMs - lastTime < MergeMs) items[idx] = s;
        else
        {
            if (idx < items.Count - 1) items.RemoveRange(idx + 1, items.Count - idx - 1);
            items.Add(s); idx++;
            if (items.Count > Max) { items.RemoveAt(0); idx--; }
        }
        lastTyping = typing; lastTime = nowMs;
    }

    internal RichSnap? Undo() { if (!CanUndo) return null; idx--; lastTyping = false; return items[idx]; }
    internal RichSnap? Redo() { if (!CanRedo) return null; idx++; lastTyping = false; return items[idx]; }
}

/// <summary>Sent messages for Up/Down. Index -1 = not walking the history (editing the draft).</summary>
internal interface IRichHistory
{
    IReadOnlyList<string> Items { get; }
    int Index { get; set; }
}

/// <summary>A plain in-memory history (oldest first). Add on send; it also ends any walk.</summary>
internal sealed class RichHistory : IRichHistory
{
    private readonly List<string> items = new();
    internal int Limit = 100;
    public IReadOnlyList<string> Items => items;
    public int Index { get; set; } = -1;

    internal void Add(string text)
    {
        Index = -1;
        text = text.Trim();
        if (text.Length == 0) return;
        if (items.Count > 0 && items[^1] == text) return;
        items.Add(text);
        if (items.Count > Limit) items.RemoveAt(0);
    }

    internal void Clear() { items.Clear(); Index = -1; }
}

/// <summary>Walks a history: returns the text to show, or null when nothing changes. draft keeps what was being typed.</summary>
internal static class RichHistoryWalk
{
    internal static string? Step(IRichHistory h, int dir, string current, ref string? draft)
    {
        var list = h.Items;
        var i = Math.Min(h.Index, list.Count - 1);
        if (dir < 0)
        {
            if (list.Count == 0) return null;
            if (i < 0) { draft = current; i = list.Count - 1; }
            else if (i > 0) i--;
            else return null;
        }
        else
        {
            if (i < 0) return null;
            i++;
            if (i >= list.Count) i = -1;
        }
        h.Index = i;
        if (i >= 0) return list[i];
        var d = draft ?? "";
        draft = null;
        return d;
    }
}

/// <summary>Drafts by key, kept across window close and (with a file) plugin reloads and game restarts. Saved at most
/// every 1.5 s, and on Flush.</summary>
internal sealed class RichDrafts
{
    private readonly string? path;
    private readonly Dictionary<string, string> map = new();
    private bool dirty;
    private long changedAt;
    internal const int MaxChars = 20000;

    internal RichDrafts(string? path)
    {
        this.path = path;
        if (path == null || !File.Exists(path)) return;
        try
        {
            var m = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (m != null) foreach (var kv in m) map[kv.Key] = kv.Value;
        }
        catch { /* a broken file only loses the drafts */ }
    }

    internal string? Get(string key) => map.TryGetValue(key, out var v) ? v : null;

    internal void Set(string key, string text, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(text)) { if (!map.Remove(key)) return; }
        else
        {
            if (text.Length > MaxChars) text = text[..MaxChars];
            if (map.TryGetValue(key, out var old) && old == text) return;
            map[key] = text;
        }
        dirty = true; changedAt = nowMs;
    }

    internal void Tick(long nowMs) { if (dirty && nowMs - changedAt >= 1500) Flush(); }

    internal void Flush()
    {
        if (!dirty) return;
        dirty = false;
        if (path == null) return;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(map));
            File.Move(tmp, path, true);
        }
        catch { /* best effort */ }
    }
}

/// <summary>Length bar colour: white up to 50 %, to yellow by 75 %, to red by 90 %, red from there on.</summary>
internal static class RichBarColour
{
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f), Yellow = new(1f, 1f, 0f, 1f), Red = new(1f, 0f, 0f, 1f);
    internal const float CounterFrom = 0.8f;

    internal static Vector4 For(float f)
    {
        if (f <= 0.5f) return White;
        if (f <= 0.75f) return Vector4.Lerp(White, Yellow, (f - 0.5f) / 0.25f);
        if (f < 0.9f) return Vector4.Lerp(Yellow, Red, (f - 0.75f) / 0.15f);
        return Red;
    }

    internal static bool ShowCounter(int used, int max) => max > 0 && used >= max * CounterFrom;
}

/// <summary>Spell checker the editor asks for underlines and suggestions. The plugin supplies it (for example a Hunspell
/// dictionary); the editor itself has no dictionary and no dependency.</summary>
internal interface IRichSpellChecker
{
    bool Ready { get; }
    bool Check(string word);
    IReadOnlyList<string> Suggest(string word);
    void Add(string word);
}