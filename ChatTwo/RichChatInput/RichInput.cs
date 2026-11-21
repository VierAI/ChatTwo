// RichChatInput - shared chat input editor. MIT License, Copyright (c) 2026 VierAI. AI generated code.
// One ImGui chat input with: word-anchored double-click drag, triple-click selects the whole message, Shift+click extends
// the selection, Ctrl+Delete deletes a word, undo/redo (Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z), a Cut/Copy/Paste/Select All menu,
// Up/Down history (only from the first/last line), drafts, drag-and-drop of the selection, spell check underlines (the
// plugin supplies the checker), soft wrap, and a length bar plus counter (only when the plugin passes a maximum).
//
// Two ways to use it:
//  - Draw(...): the editor draws the input (single line, or soft wrapped). Plugin callback logic goes in the hook.
//  - Engine: the plugin draws its own InputText with EngineFlags, calls Begin before it, PreAlways/PostAlways at the
//    start/end of its CallbackAlways, and AfterItem right after it.
// Enter, channel prefixes, IME, focus rules, hard length caps and key captures stay with the plugin.
#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace RichChatInput;

internal sealed unsafe class RichInput
{
    // ---- options (the plugin may change these any frame) ----
    /// <summary>Soft wrap: a multiline widget whose line breaks are spaces in the text. Enter still submits.</summary>
    internal bool Wrap;
    internal int MaxLines = 4;
    internal bool UndoRedo = true;
    internal bool ContextMenu = true;
    internal bool DragDrop = true;
    internal bool ReadOnly { get; set; }
    /// <summary>Up/Down recall. Null: the plugin keeps its own (or none).</summary>
    internal IRichHistory? History;
    /// <summary>Set for a frame while something else (a popup) uses the arrow keys.</summary>
    internal bool HistoryBlocked { get; set; }
    /// <summary>Characters allowed when pasting from the menu (null: all printable).</summary>
    internal Func<char, bool>? CharAllowed { get; set; }
    internal IRichSpellChecker? Spell;
    internal bool SpellEnabled = true;
    internal RichDrafts? Drafts;
    internal string? DraftKey;

    // ---- results ----
    /// <summary>Height of the input as last laid out (one frame height when not wrapped).</summary>
    internal float Height { get; private set; }
    /// <summary>Extra height over a one-line input, for layouts that reserve room below a log.</summary>
    internal float ExtraHeight => MathF.Max(0f, Height - ImGui.GetFrameHeight());
    /// <summary>The text as shown, with soft line breaks ('\n' where a space wraps).</summary>
    internal string DisplayText => display;
    internal bool IsActive { get; private set; }
    /// <summary>Caret as a char index into the text, from the last active frame.</summary>
    internal int CursorChar { get; private set; }
    internal Vector2 RectMin => rectMin;
    internal Vector2 RectMax => rectMax;
    internal bool CounterShown { get; private set; }
    internal float BarHeight => 3f * ImGuiHelpers.GlobalScale;
    /// <summary>Height of the length bar row (taller while the counter shows).</summary>
    internal float BarRowHeight => CounterShown ? MathF.Max(BarHeight, ImGui.GetTextLineHeight()) : BarHeight;

    private readonly ImGui.ImGuiInputTextCallbackDelegate callback;
    private ImGui.ImGuiInputTextCallbackDelegate? hook;
    private ImGuiInputTextFlags hookFlags;
    private string label = "", display = "";
    private float width = 100f;
    private Vector2 rectMin, rectMax;
    private uint itemId;
    private string? childName;
    private bool focusNext;
    private string? pendingExternal;

    // last active frame
    private RichSnap prev;
    private bool prevValid;
    private byte[] prevDisplay = Array.Empty<byte>();
    private int lastAlwaysFrame = -10;
    private readonly RichUndo undo = new();
    private string? histDraft;

    // word drag (double-click then drag)
    private bool wdArmed;
    private int wdA, wdB, wdBytes;
    // drag and drop of the selection
    private bool ddArmed, ddMoved;
    private int ddA, ddB, ddClick;
    private Vector2 ddMouse;
    private string ddText = "";

    // context menu
    private enum Op { None, Cut, Paste, SelectAll, Replace }
    private Op pendingOp;
    private int opA, opB, opFrame;
    private string opText = "", opBase = "";
    private bool menuReq;
    private string menuText = "", menuClip = "";
    private int menuA, menuB;
    private string? menuWord;
    private int menuWordA, menuWordB;
    private IReadOnlyList<string>? menuSugg;

    // spell check
    private string spellFor = "\0";
    private int spellCaret = -2;
    private List<(int Start, int Length)> spellSpans = new();
    private readonly Dictionary<string, bool> spellCache = new();

    // soft wrap cache
    private string wrapKey = "\0", wrapVal = "";
    private float wrapW = -1f;

    // drafts
    private string? draftLoaded;
    private string draftSaved = "";

    internal RichInput() { callback = Callback; }

    /// <summary>Flags an engine-mode caller adds to its own InputText.</summary>
    internal ImGuiInputTextFlags EngineFlags =>
        ImGuiInputTextFlags.CallbackAlways | (UndoRedo ? ImGuiInputTextFlags.NoUndoRedo : ImGuiInputTextFlags.None);

    /// <summary>Focus the input on the next frame.</summary>
    internal void Focus() => focusNext = true;

    // ======================================================================================================== Draw mode

    /// <summary>Draws the input. flags may add CallbackCompletion/CallbackCharFilter/CallbackAlways/EnterReturnsTrue for
    /// the hook; returns what ImGui returns (true on Enter with EnterReturnsTrue). After it the input is the last item.</summary>
    internal bool Draw(string id, ref string text, float width, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None,
        ImGui.ImGuiInputTextCallbackDelegate? hook = null, int maxBytes = 2000, string? hint = null)
    {
        this.hook = hook; hookFlags = flags; this.width = MathF.Max(1f, width);
        DraftBefore(ref text);
        Begin(id, Wrap);
        var f = (flags & ~ImGuiInputTextFlags.CallbackHistory) | ImGuiInputTextFlags.CallbackAlways;
        if (UndoRedo) f |= ImGuiInputTextFlags.NoUndoRedo;
        if (ReadOnly) f |= ImGuiInputTextFlags.ReadOnly;
        bool ret;
        if (Wrap)
        {
            f |= ImGuiInputTextFlags.CallbackCharFilter | ImGuiInputTextFlags.CtrlEnterForNewLine;
            var shown = RichWords.Normalize(display);
            if (shown != text && IsActive) pendingExternal = text;
            if (shown != text || MathF.Abs(this.width - wrapW) > 0.5f) display = WrapText(text);
            var lines = Math.Clamp(RichWords.LineCount(Encoding.UTF8.GetBytes(display)), 1, Math.Max(1, MaxLines));
            Height = lines * ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2f;
            ret = ImGui.InputTextMultiline(id, ref display, maxBytes, new Vector2(this.width, Height), f, callback);
            text = RichWords.Normalize(display);
        }
        else
        {
            if (History != null) f |= ImGuiInputTextFlags.CallbackHistory;
            Height = ImGui.GetFrameHeight();
            ImGui.SetNextItemWidth(this.width);
            ret = hint != null
                ? ImGui.InputTextWithHint(id, hint, ref text, maxBytes, f, callback)
                : ImGui.InputText(id, ref text, maxBytes, f, callback);
            display = text;
        }
        AfterItem(text);
        if (Wrap && !string.IsNullOrEmpty(hint) && text.Length == 0 && !IsActive)
            ImGui.GetWindowDrawList().AddText(rectMin + ImGui.GetStyle().FramePadding, ImGui.GetColorU32(ImGuiCol.TextDisabled), hint);
        DraftAfter(text);
        return ret;
    }

    private int Callback(scoped ref ImGuiInputTextCallbackData d)
    {
        switch (d.EventFlag)
        {
            case ImGuiInputTextFlags.CallbackCharFilter:
                if (Wrap && (d.EventChar == '\n' || d.EventChar == '\r' || d.EventChar == '\t')) d.EventChar = ' ';
                return hook != null && (hookFlags & ImGuiInputTextFlags.CallbackCharFilter) != 0 ? hook(ref d) : 0;
            case ImGuiInputTextFlags.CallbackHistory:
                return OnHistoryKey(ref d);
            case ImGuiInputTextFlags.CallbackAlways:
                PreAlways(ref d);
                if (hook != null && (hookFlags & ImGuiInputTextFlags.CallbackAlways) != 0) hook(ref d);
                if (Wrap) WrapBuffer(ref d);
                PostAlways(ref d);
                return 0;
            default:
                return hook != null ? hook(ref d) : 0;
        }
    }

    private string WrapText(string t)
    {
        if (t == wrapKey && MathF.Abs(width - wrapW) < 0.5f) return wrapVal;
        var st = ImGui.GetStyle();
        var inner = width - st.FramePadding.X * 2f - 2f;
        Func<string, float> measure = s => ImGui.CalcTextSize(s).X;
        var br = RichWords.WrapBreaks(t, inner, measure);
        if (br.Count + 1 > MaxLines) br = RichWords.WrapBreaks(t, inner - st.ScrollbarSize, measure);
        wrapKey = t; wrapW = width; wrapVal = RichWords.ApplyBreaks(t, br);
        return wrapVal;
    }

    private void WrapBuffer(scoped ref ImGuiInputTextCallbackData d)
    {
        var raw = Bytes(ref d);
        var s = Encoding.UTF8.GetString(raw);
        var w = WrapText(RichWords.Normalize(s));
        if (w == s) return;
        var wb = Encoding.UTF8.GetBytes(w);
        if (wb.Length != raw.Length) return;
        for (var i = 0; i < wb.Length; i++) d.Buf[i] = wb[i];
        d.BufDirty = 1;
    }

    // ====================================================================================================== Engine mode

    /// <summary>Call right before the input widget.</summary>
    internal void Begin(string id, bool wrapped)
    {
        label = id; Wrap = wrapped;
        if (focusNext && !ReadOnly) ImGui.SetKeyboardFocusHere();
        focusNext = false;
    }

    /// <summary>Start of CallbackAlways: menu actions, undo/redo, Ctrl+Delete, mouse (Shift+click, triple-click, word
    /// drag, drag and drop) and, when wrapped, Up/Down history.</summary>
    internal void PreAlways(scoped ref ImGuiInputTextCallbackData d)
    {
        var frame = ImGui.GetFrameCount();
        var cont = prevValid && IsActive && frame - lastAlwaysFrame <= 2;
        var io = ImGui.GetIO();
        var cur = RichWords.Normalize(Encoding.UTF8.GetString(Bytes(ref d)));
        if (!cont)
        {
            wdArmed = ddArmed = false; histDraft = null;
            if (UndoRedo && undo.Current?.Text != cur) undo.Reset(new RichSnap(cur, d.CursorPos, d.SelectionStart, d.SelectionEnd));
        }
        if (pendingExternal != null)
        {
            var t = pendingExternal; pendingExternal = null;
            if (t != cur) { var n = Encoding.UTF8.GetByteCount(t); SetText(ref d, t, n, n, n); return; }
        }
        if (pendingOp != Op.None) { ApplyOp(ref d, cur); return; }
        var ctrl = io.KeyCtrl && !io.KeyAlt; var shift = io.KeyShift;
        if (UndoRedo && ctrl && !ReadOnly)
        {
            RichSnap? s = null;
            if (ImGui.IsKeyPressed(ImGuiKey.Z, true)) s = shift ? undo.Redo() : undo.Undo();
            else if (!shift && ImGui.IsKeyPressed(ImGuiKey.Y, true)) s = undo.Redo();
            if (s is { } st) { wdArmed = ddArmed = false; SetText(ref d, st.Text, st.Cursor, st.SelStart, st.SelEnd); return; }
        }
        if (ctrl && !shift && !ReadOnly && cont && prev.SelStart == prev.SelEnd && ImGui.IsKeyPressed(ImGuiKey.Delete, true))
        {
            // ImGui has deleted one character; redo it from last frame's text as a whole-word delete.
            var pb = Encoding.UTF8.GetBytes(prev.Text);
            var ci = RichWords.CharIndex(pb, prev.Cursor);
            var end = RichWords.DeleteWordRightEnd(prev.Text, ci);
            if (end > ci) { var c = RichWords.ByteOffset(prev.Text, ci); SetText(ref d, prev.Text.Remove(ci, end - ci), c, c, c); }
            return;
        }
        if (Mouse(ref d, cur, cont, io)) return;
        if (Wrap && History != null && !HistoryBlocked && !ReadOnly && cont && !io.KeyCtrl && !io.KeyShift && !io.KeyAlt)
        {
            var up = ImGui.IsKeyPressed(ImGuiKey.UpArrow, true);
            var down = !up && ImGui.IsKeyPressed(ImGuiKey.DownArrow, true);
            if (up || down)
            {
                var line = RichWords.LineOfByte(prevDisplay, prev.Cursor);
                var lines = RichWords.LineCount(prevDisplay);
                if ((up && line == 0) || (down && line == lines - 1))
                {
                    var t = RichHistoryWalk.Step(History, up ? -1 : 1, cur, ref histDraft);
                    if (t != null) { var n = Encoding.UTF8.GetByteCount(t); SetText(ref d, t, n, n, n); }
                }
            }
        }
    }

    /// <summary>End of CallbackAlways (after the plugin's own edits): remembers the frame and records undo.</summary>
    internal void PostAlways(scoped ref ImGuiInputTextCallbackData d)
    {
        var raw = Bytes(ref d);
        var t = RichWords.Normalize(Encoding.UTF8.GetString(raw));
        prev = new RichSnap(t, d.CursorPos, d.SelectionStart, d.SelectionEnd);
        prevDisplay = raw; prevValid = true;
        lastAlwaysFrame = ImGui.GetFrameCount();
        CursorChar = RichWords.CharIndex(raw, d.CursorPos);
        if (UndoRedo) undo.Record(prev, Environment.TickCount64);
    }

    /// <summary>CallbackHistory of a single-line input (Up/Down). Returns 0.</summary>
    internal int OnHistoryKey(scoped ref ImGuiInputTextCallbackData d)
    {
        if (History == null || HistoryBlocked || ReadOnly) return 0;
        var dir = d.EventKey == ImGuiKey.UpArrow ? -1 : d.EventKey == ImGuiKey.DownArrow ? 1 : 0;
        if (dir == 0) return 0;
        var t = RichHistoryWalk.Step(History, dir, RichWords.Normalize(Encoding.UTF8.GetString(Bytes(ref d))), ref histDraft);
        if (t != null) { var n = Encoding.UTF8.GetByteCount(t); SetText(ref d, t, n, n, n); }
        return 0;
    }

    /// <summary>Call right after the input widget (it must be the last item). text = the input's text now.</summary>
    internal void AfterItem(string text)
    {
        rectMin = ImGui.GetItemRectMin(); rectMax = ImGui.GetItemRectMax();
        itemId = ImGui.GetID(label); // same id stack as the widget
        IsActive = ImGui.IsItemActive();
        if (!IsActive) { pendingExternal = null; wdArmed = ddArmed = false; }
        if (Wrap)
        {
            var parent = ImGuiP.GetCurrentWindow();
            var pname = parent.IsNull ? null : Marshal.PtrToStringUTF8((nint)parent.Name);
            childName = pname == null ? null : $"{pname}/{label}_{itemId:X8}";
        }
        else childName = null;
        var hovered = ImGui.IsItemHovered() || (ImGui.IsMouseHoveringRect(rectMin, rectMax) && ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows));
        if (ContextMenu && hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right)) CaptureMenu(text);
        if (Spell != null && SpellEnabled) DrawSpell(text);
    }

    // ============================================================================================================ Mouse

    private bool Mouse(scoped ref ImGuiInputTextCallbackData d, string cur, bool cont, ImGuiIOPtr io)
    {
        var shift = io.KeyShift;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            wdArmed = ddArmed = false;
            var m = ImGui.GetMousePos();
            var over = m.X >= rectMin.X && m.Y >= rectMin.Y && m.X < rectMax.X && m.Y < rectMax.Y;
            if (!over) return false;
            var n = ImGui.GetMouseClickedCount(ImGuiMouseButton.Left);
            var same = prevValid && prev.Text == cur;
            if (n == 1 && shift && same)
            {
                // ImGui moved the caret and dropped the selection; keep last frame's anchor.
                var anchor = prev.SelStart == prev.SelEnd ? prev.Cursor : prev.Cursor == prev.SelEnd ? prev.SelStart : prev.SelEnd;
                d.SelectionStart = anchor; d.SelectionEnd = d.CursorPos;
                return true;
            }
            if (n == 1 && !shift && DragDrop && !ReadOnly && cont && same && prev.SelStart != prev.SelEnd)
            {
                var a = Math.Min(prev.SelStart, prev.SelEnd); var b = Math.Max(prev.SelStart, prev.SelEnd);
                if (d.CursorPos > a && d.CursorPos < b)
                {
                    ddArmed = true; ddMoved = false; ddA = prev.SelStart; ddB = prev.SelEnd; ddClick = d.CursorPos; ddMouse = m; ddText = cur;
                    d.SelectionStart = ddA; d.SelectionEnd = ddB;
                    return true;
                }
            }
            if (n >= 3 && n % 2 == 1 && Wrap)
            {
                d.SelectionStart = 0; d.SelectionEnd = d.CursorPos = d.BufTextLen; // the whole message, not one wrapped row
                return true;
            }
            if (n >= 2 && n % 2 == 0 && !shift && d.SelectionStart != d.SelectionEnd)
            {
                var bytes = Bytes(ref d);
                wdA = RichWords.CharIndex(bytes, Math.Min(d.SelectionStart, d.SelectionEnd));
                wdB = RichWords.CharIndex(bytes, Math.Max(d.SelectionStart, d.SelectionEnd));
                wdBytes = d.BufTextLen; wdArmed = true;
                return true;
            }
            return false;
        }
        if (ddArmed)
        {
            if (cur != ddText) { ddArmed = false; return false; }
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (!ddMoved && Vector2.Distance(ImGui.GetMousePos(), ddMouse) > MathF.Max(2f, io.MouseDragThreshold)) ddMoved = true;
                d.SelectionStart = ddA; d.SelectionEnd = ddB; // the caret (under the mouse) shows where it will drop
                return true;
            }
            ddArmed = false;
            int a = Math.Min(ddA, ddB), b = Math.Max(ddA, ddB), drop = d.CursorPos;
            if (!ddMoved) { d.CursorPos = d.SelectionStart = d.SelectionEnd = ddClick; return true; }
            if (drop >= a && drop <= b) { d.SelectionStart = ddA; d.SelectionEnd = ddB; d.CursorPos = ddB; return true; }
            var all = Encoding.UTF8.GetBytes(cur);
            var piece = all[a..b];
            var copy = io.KeyCtrl; // Ctrl on release copies instead of moving
            var rest = copy ? all : Concat(all[..a], all[b..]);
            var at = copy || drop < a ? drop : drop - (b - a);
            at = Math.Clamp(at, 0, rest.Length);
            var nb = Concat(Concat(rest[..at], piece), rest[at..]);
            SetText(ref d, Encoding.UTF8.GetString(nb), at + piece.Length, at, at + piece.Length);
            return true;
        }
        if (wdArmed)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || d.BufTextLen != wdBytes) { wdArmed = false; return false; }
            var bytes = Bytes(ref d);
            var s = RichWords.Normalize(Encoding.UTF8.GetString(bytes));
            var (fix, mov, caret) = RichWords.Extend(s, wdA, wdB, RichWords.CharIndex(bytes, d.CursorPos));
            d.SelectionStart = RichWords.ByteOffset(s, fix);
            d.SelectionEnd = RichWords.ByteOffset(s, mov);
            d.CursorPos = RichWords.ByteOffset(s, caret);
            return true;
        }
        return false;
    }

    // ============================================================================================================= Menu

    private void CaptureMenu(string text)
    {
        var norm = RichWords.Normalize(text);
        menuText = norm;
        var live = prevValid && prev.Text == norm && ImGui.GetFrameCount() - lastAlwaysFrame <= 2;
        menuA = live ? Math.Min(prev.SelStart, prev.SelEnd) : 0;
        menuB = live ? Math.Max(prev.SelStart, prev.SelEnd) : 0;
        menuWord = null; menuSugg = null;
        if (Spell is { Ready: true } && SpellEnabled)
        {
            var ci = HitTest(ImGui.GetMousePos());
            foreach (var (s0, len) in spellSpans)
                if (ci >= s0 && ci < s0 + len && s0 + len <= norm.Length)
                {
                    menuWord = norm.Substring(s0, len);
                    menuWordA = RichWords.ByteOffset(norm, s0); menuWordB = RichWords.ByteOffset(norm, s0 + len);
                    break;
                }
        }
        menuReq = true;
    }

    /// <summary>Draws the right-click menu. suppress: another menu (for example the plugin's own spell menu) took this
    /// right-click. extra: the plugin's own items, shown under a separator.</summary>
    internal void DrawMenu(bool suppress = false, Action? extra = null)
    {
        var id = "##rcimenu" + label;
        if (menuReq)
        {
            menuReq = false;
            if (!suppress && ContextMenu) { menuClip = ImGui.GetClipboardText() ?? ""; ImGui.OpenPopup(id); }
        }
        if (!ImGui.BeginPopup(id)) return;
        if (menuWord != null && Spell != null)
        {
            menuSugg ??= Spell.Suggest(menuWord);
            if (menuSugg.Count == 0) ImGui.TextDisabled("No suggestions");
            for (var i = 0; i < Math.Min(6, menuSugg.Count); i++)
                if (ImGui.MenuItem(menuSugg[i] + "##rcis" + i)) Queue(Op.Replace, menuWordA, menuWordB, menuSugg[i]);
            if (ImGui.MenuItem("Add to dictionary")) { Spell.Add(menuWord); spellCache.Clear(); spellFor = "\0"; }
            ImGui.Separator();
        }
        var hasSel = menuB > menuA;
        if (ImGui.MenuItem("Cut", "Ctrl+X", false, hasSel && !ReadOnly)) { ImGui.SetClipboardText(MenuSelection()); Queue(Op.Cut, menuA, menuB, ""); }
        if (ImGui.MenuItem("Copy", "Ctrl+C", false, hasSel)) ImGui.SetClipboardText(MenuSelection());
        if (ImGui.MenuItem("Paste", "Ctrl+V", false, !ReadOnly && menuClip.Length > 0)) Queue(Op.Paste, menuA, menuB, "");
        if (ImGui.MenuItem("Select All", "Ctrl+A", false, menuText.Length > 0)) Queue(Op.SelectAll, 0, 0, "");
        if (extra != null) { ImGui.Separator(); extra(); }
        ImGui.EndPopup();
    }

    private string MenuSelection()
    {
        var b = Encoding.UTF8.GetBytes(menuText);
        int a0 = Math.Clamp(menuA, 0, b.Length), b0 = Math.Clamp(menuB, a0, b.Length);
        return Encoding.UTF8.GetString(b, a0, b0 - a0);
    }

    private void Queue(Op op, int a, int b, string text)
    {
        pendingOp = op; opA = a; opB = b; opText = text; opBase = menuText; opFrame = ImGui.GetFrameCount();
        focusNext = true;
    }

    private void ApplyOp(scoped ref ImGuiInputTextCallbackData d, string cur)
    {
        var op = pendingOp; pendingOp = Op.None;
        if (ImGui.GetFrameCount() - opFrame > 60) return;
        var bytes = Encoding.UTF8.GetBytes(cur);
        var same = cur == opBase;
        int a = Math.Clamp(Math.Min(opA, opB), 0, bytes.Length), b = Math.Clamp(Math.Max(opA, opB), 0, bytes.Length);
        switch (op)
        {
            case Op.SelectAll:
                d.SelectionStart = 0; d.SelectionEnd = d.CursorPos = d.BufTextLen;
                break;
            case Op.Cut:
                if (same && b > a) Splice(ref d, bytes, a, b, "");
                break;
            case Op.Replace:
                if (same && b > a) Splice(ref d, bytes, a, b, opText);
                break;
            case Op.Paste:
                if (!same) a = b = bytes.Length;
                var clip = RichWords.CleanPaste(ImGui.GetClipboardText() ?? "", CharAllowed);
                clip = RichWords.FitBytes(clip, d.BufSize - 1 - (bytes.Length - (b - a)));
                if (clip.Length > 0 || b > a) Splice(ref d, bytes, a, b, clip);
                break;
        }
    }

    private static void Splice(scoped ref ImGuiInputTextCallbackData d, byte[] bytes, int a, int b, string insert)
    {
        var ins = Encoding.UTF8.GetBytes(insert);
        var nb = Concat(Concat(bytes[..a], ins), bytes[b..]);
        var c = a + ins.Length;
        SetText(ref d, Encoding.UTF8.GetString(nb), c, c, c);
    }

    // ======================================================================================================= Spell check

    private (Vector2 Origin, ImDrawListPtr List, bool Ok) Layout()
    {
        var st = ImGui.GetStyle();
        var scrollX = 0f;
        if (IsActive)
        {
            var ctx = ImGui.GetCurrentContext();
            ref var ts = ref ctx.InputTextState;
            if (ts.ID == itemId) scrollX = ts.ScrollX;
        }
        if (!Wrap) return (rectMin + st.FramePadding - new Vector2(scrollX, 0f), ImGui.GetWindowDrawList(), true);
        if (childName == null) return (default, default, false);
        var w = ImGuiP.FindWindowByName(childName);
        if (w.IsNull) return (default, default, false);
        return (w.Pos + st.FramePadding - w.Scroll - new Vector2(scrollX, 0f), w.DrawList, true);
    }

    private bool IsWordOk(string w)
    {
        if (spellCache.TryGetValue(w, out var ok)) return ok;
        ok = Spell!.Check(w);
        if (spellCache.Count > 5000) spellCache.Clear();
        spellCache[w] = ok;
        return ok;
    }

    private void DrawSpell(string text)
    {
        if (Spell is not { Ready: true }) return;
        var norm = RichWords.Normalize(text);
        var caret = IsActive ? CursorChar : -1;
        if (norm != spellFor || caret != spellCaret)
        {
            spellFor = norm; spellCaret = caret;
            spellSpans = RichWords.SpellWords(norm, caret);
            spellSpans.RemoveAll(w => IsWordOk(norm.Substring(w.Start, w.Length)));
        }
        if (spellSpans.Count == 0) return;
        var (o, dl, ok) = Layout();
        if (!ok) return;
        var disp = Wrap ? display : norm;
        if (disp.Length != norm.Length) return;
        var lineH = ImGui.GetTextLineHeight();
        var col = ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, 1f));
        var step = 2f * ImGuiHelpers.GlobalScale;
        dl.PushClipRect(rectMin, rectMax, true);
        foreach (var (s0, len) in spellSpans)
        {
            var rowStart = s0 == 0 ? 0 : disp.LastIndexOf('\n', s0 - 1) + 1;
            var row = 0;
            for (var i = 0; i < rowStart; i++) if (disp[i] == '\n') row++;
            var x0 = o.X + ImGui.CalcTextSize(disp[rowStart..s0]).X;
            var x1 = x0 + ImGui.CalcTextSize(disp.Substring(s0, len)).X;
            var y = o.Y + (row + 1) * lineH - step * 0.5f;
            var up = false;
            for (var x = x0; x < x1; x += step)
            {
                var nx = MathF.Min(x + step, x1);
                dl.AddLine(new Vector2(x, up ? y - step * 0.5f : y + step * 0.5f), new Vector2(nx, up ? y + step * 0.5f : y - step * 0.5f), col, 1f);
                up = !up;
            }
        }
        dl.PopClipRect();
    }

    /// <summary>Char index under a screen point, or -1.</summary>
    private int HitTest(Vector2 p)
    {
        var (o, _, ok) = Layout();
        if (!ok) return -1;
        var disp = Wrap ? display : menuText;
        var lineH = ImGui.GetTextLineHeight();
        var row = (int)MathF.Floor((p.Y - o.Y) / lineH);
        if (row < 0) return -1;
        var start = 0;
        for (var r = 0; r < row; r++)
        {
            var nl = disp.IndexOf('\n', start);
            if (nl < 0) return -1;
            start = nl + 1;
        }
        var end = disp.IndexOf('\n', start); if (end < 0) end = disp.Length;
        var rel = p.X - o.X;
        for (var i = start; i < end; i++)
            if (ImGui.CalcTextSize(disp[start..(i + 1)]).X > rel) return i;
        return -1;
    }

    // ========================================================================================================= Length bar

    /// <summary>Length bar (and, from 80 %, a used/max counter at its right end). Draws nothing without a maximum.</summary>
    internal void DrawLengthBar(float width, int used, int max, string unit = "bytes")
    {
        if (max <= 0) { CounterShown = false; return; }
        var f = used / (float)max;
        var col = RichBarColour.For(f);
        var show = RichBarColour.ShowCounter(used, max);
        var barH = BarHeight;
        var start = ImGui.GetCursorScreenPos();
        var counter = $"{used}/{max}";
        var tw = show ? ImGui.CalcTextSize(counter).X + ImGui.GetStyle().ItemSpacing.X : 0f;
        var rowH = show ? MathF.Max(barH, ImGui.GetTextLineHeight()) : barH;
        ImGui.SetCursorScreenPos(start + new Vector2(0f, (rowH - barH) * 0.5f));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
        ImGui.ProgressBar(Math.Clamp(f, 0f, 1f), new Vector2(MathF.Max(1f, width - tw), barH), "");
        ImGui.PopStyleColor();
        var tip = used > max ? $"{used} / {max} {unit}: too long" : $"{used} / {max} {unit}";
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
        if (show)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X + width - tw + ImGui.GetStyle().ItemSpacing.X, start.Y));
            ImGui.TextColored(col, counter);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
        }
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH));
        ImGui.Dummy(new Vector2(0f, 0f));
        CounterShown = show;
    }

    // ============================================================================================================ Drafts

    /// <summary>Loads the draft for DraftKey into an empty input (once per key).</summary>
    internal void DraftBefore(ref string text)
    {
        if (Drafts == null || DraftKey == null || draftLoaded == DraftKey) return;
        draftLoaded = DraftKey;
        if (text.Length == 0 && Drafts.Get(DraftKey) is { } d) text = d;
        draftSaved = RichWords.Normalize(text);
    }

    /// <summary>Saves the text as the draft for DraftKey (an empty input removes it).</summary>
    internal void DraftAfter(string text)
    {
        if (Drafts == null || draftLoaded == null) return;
        var n = RichWords.Normalize(text);
        var now = Environment.TickCount64;
        if (n != draftSaved) { draftSaved = n; Drafts.Set(draftLoaded, n, now); }
        Drafts.Tick(now);
    }

    // =========================================================================================================== Helpers

    private static byte[] Bytes(scoped ref ImGuiInputTextCallbackData d) =>
        new ReadOnlySpan<byte>(d.Buf, Math.Max(0, d.BufTextLen)).ToArray();

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        a.CopyTo(r, 0); b.CopyTo(r, a.Length);
        return r;
    }

    /// <summary>Replaces the whole buffer (cut to the buffer size on a character boundary) and sets caret and selection.</summary>
    private static void SetText(scoped ref ImGuiInputTextCallbackData d, string s, int caret, int a, int b)
    {
        s = RichWords.FitBytes(s, d.BufSize - 1);
        if (d.BufTextLen > 0) d.DeleteChars(0, d.BufTextLen);
        if (s.Length > 0) d.InsertChars(0, s);
        var n = d.BufTextLen;
        d.CursorPos = Math.Clamp(caret, 0, n);
        d.SelectionStart = Math.Clamp(a, 0, n);
        d.SelectionEnd = Math.Clamp(b, 0, n);
    }
}