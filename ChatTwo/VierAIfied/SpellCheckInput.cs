// VierAI-fied fork support (AI generated code).
// Marks misspelled words in an ImGui input box, with a hover popup and a right-click menu offering
// suggestions, "Ignore" and "Add to dictionary". Shared word for word by the VierAI-fied XIM and Chat 2.
// Usage per input box, every frame:
//   before the input:  if(box.TakeReplacement(ref text, out var cursorBytes)) { refocus, set cursor }
//   right after it:    box.AfterInput(text, enabled, multiline, "##label", cursorChar)
//   later, same scope: box.DrawPopup()
// AfterInput sets RightClickConsumed when it opened its menu, so the host can skip its own context menu.
// TakeReplacement also returns true (text unchanged) to hand focus back after Ignore / Add in the hover popup.
//
// Drawing: everything goes on the input's own window draw list (for InputTextMultiline that is its child
// window, found by name and checked by ChildId), inside a clip rect of the input's inner frame, so it sits
// in the same layer as the text: windows, popups and tooltips above the input cover it, and it is clipped
// while scrolled. Scroll comes from the same window (child Scroll, or the single-line ScrollX), and
// positions are measured with the input's current font. ImGui's InputText does not word wrap; XIM's
// multi-line box wraps by inserting real line breaks, so laying out by '\n' matches what is on screen.
// Nothing is drawn while the IME is composing, or when the input or its window is hidden or collapsed.
//
// Highlight style: a faint rounded box drawn over the word after the input (InputText draws its opaque frame
// and text in one call, so nothing can go between them). The fill is light enough (HighlightFill, 10% by
// default) that the word shows through, slightly tinted.
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace VierAIfiedShared;

internal sealed class SpellCheckBox
{
    private const string PopupId = "##vierai-spellcheck";
    private const long HoverDelayMs = 350, TypingQuietMs = 300, LeaveGraceMs = 250;
    private static int nextId;

    private readonly string hoverWindowName = $"##vierai-spell-hover-{++nextId}";
    private string lastText = string.Empty;
    private int lastGeneration = -1, lastCursor = -2;
    private float lastFontSize;
    private nint lastFont;
    private List<SpellSpan> spans = [];
    private readonly List<(float X, float Width, int Line, int LineStart)> layout = [];

    private string targetWord = string.Empty;
    private int targetStart;
    private Task<List<string>>? suggestions;
    private (int Start, string Word, string Replacement)? pending;

    // Hover popup state.
    private long lastTypeMs, hoverSinceMs, lastInsideMs;
    private int hoverIndex = -1;
    private bool hoverOpen;
    private int hoverFrame = -1;
    private string hoverWord = string.Empty;
    private int hoverStart;
    private Task<List<string>>? hoverSuggestions;
    private Vector2 anchorMin, anchorMax, popupMin, popupMax;
    private int refocusBytes = -1;
    private bool refocusPending;

    internal bool RightClickConsumed { get; private set; }

    private static long Now => Environment.TickCount64;

    // Applies a picked suggestion to the host's text. Returns the new cursor as a UTF-8 byte offset.
    internal bool TakeReplacement(ref string text, out int cursorBytes)
    {
        cursorBytes = -1;
        if(pending is not { } p)
        {
            if(!refocusPending) return false;
            refocusPending = false;
            if(refocusBytes < 0 || refocusBytes > Encoding.UTF8.GetByteCount(text)) return false;
            cursorBytes = refocusBytes;
            return true;
        }
        pending = null;
        refocusPending = false;
        if(p.Start + p.Word.Length > text.Length || string.CompareOrdinal(text, p.Start, p.Word, 0, p.Word.Length) != 0) return false;
        text = string.Concat(text.AsSpan(0, p.Start), p.Replacement, text.AsSpan(p.Start + p.Word.Length));
        cursorBytes = Encoding.UTF8.GetByteCount(text.AsSpan(0, p.Start + p.Replacement.Length));
        return true;
    }

    // The child window InputTextMultiline draws into: "<parent>/<label>_<item id>", whose ChildId is the item id.
    private static unsafe ImGuiWindowPtr FindInputChild(ImGuiWindowPtr parent, string label, uint id)
    {
        var name = $"{Marshal.PtrToStringUTF8((nint)parent.Name)}/{label}_{id:X8}";
        var child = ImGuiP.FindWindowByName(name);
        if(!child.IsNull && child.ChildId == id) return child;
        var list = parent.DC.ChildWindows;
        for(var k = list.Size - 1; k >= 0; k--)
        {
            var w = list[k];
            if(!w.IsNull && w.ChildId == id) return w;
        }
        return default;
    }

    private static uint WithAlpha(uint abgr, float factor)
    {
        var a = (uint)Math.Clamp((abgr >> 24) * factor, 0f, 255f);
        return (abgr & 0x00FFFFFF) | (a << 24);
    }

    private static bool InRect(Vector2 p, Vector2 min, Vector2 max) => p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;

    internal unsafe void AfterInput(string text, bool enabled, bool multiline, string label, int cursorChar = -1)
    {
        RightClickConsumed = false;
        if(!enabled || !SpellChecker.Ready) return;
        SpellChecker.Poll();

        var window = ImGuiP.GetCurrentWindow();
        if(window.IsNull || window.SkipItems || window.Collapsed || window.Hidden || !ImGui.IsItemVisible()) return;
        var id = ImGuiP.GetItemID();
        var active = ImGui.IsItemActive();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var pad = ImGui.GetStyle().FramePadding;
        var scroll = Vector2.Zero;
        var drawList = window.DrawList;
        var clipMin = min + pad;
        var clipMax = max - pad;
        if(multiline)
        {
            var child = FindInputChild(window, label, id);
            if(child.IsNull || child.Hidden || child.SkipItems) return;
            drawList = child.DrawList;
            scroll = child.Scroll;
            clipMin = Vector2.Max(clipMin, child.InnerClipRect.Min);
            clipMax = Vector2.Min(clipMax, child.InnerClipRect.Max);
        }
        else if(active)
        {
            var state = ImGuiP.GetInputTextState(id);
            if(!state.IsNull && state.ID == id) scroll.X = state.ScrollX;
        }
        if(clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) return;
        if(active && Ime.Composing()) return;

        var now = Now;
        var fontSize = ImGui.GetFontSize();
        var fontPtr = ImGui.GetFont();
        var font = (nint)fontPtr.Handle;
        if(text != lastText || cursorChar != lastCursor)
        {
            lastTypeMs = now; // typing or moving the cursor: hold the hover popup back, and close an open one
            if(text != lastText) hoverOpen = false;
        }
        if(text != lastText || lastGeneration != SpellChecker.Generation || cursorChar != lastCursor || fontSize != lastFontSize || font != lastFont)
        {
            lastText = text;
            lastGeneration = SpellChecker.Generation;
            lastCursor = cursorChar;
            lastFontSize = fontSize;
            lastFont = font;
            spans = SpellChecker.Analyze(text, cursorChar);
            layout.Clear();
            hoverIndex = -1;
            foreach(var s in spans)
            {
                var lineStart = s.Start == 0 ? 0 : text.LastIndexOf('\n', s.Start - 1) + 1;
                var line = 0;
                for(var k = 0; k < lineStart; k++) if(text[k] == '\n') line++;
                var x = s.Start == lineStart ? 0f : ImGui.CalcTextSize(text.Substring(lineStart, s.Start - lineStart)).X;
                layout.Add((x, ImGui.CalcTextSize(text.Substring(s.Start, s.Length)).X, line, lineStart));
            }
        }
        if(spans.Count == 0) { hoverOpen = false; return; }

        var origin = min + pad - scroll;
        var lineHeight = fontSize;
        var gs = ImGuiHelpers.GlobalScale;
        var mouse = ImGui.GetMousePos();
        var windowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
        var mouseInBox = windowHovered && InRect(mouse, clipMin, clipMax);

        // Which flagged word the mouse is on (only looked up while the mouse is over the input).
        var under = -1;
        if(mouseInBox)
        {
            for(var k = 0; k < layout.Count; k++)
            {
                var (x, width, line, _) = layout[k];
                var x0 = origin.X + x;
                var top = origin.Y + line * lineHeight;
                if(mouse.X < x0 - 1 || mouse.X > x0 + width + 1 || mouse.Y < top || mouse.Y > top + lineHeight + 2) continue;
                under = k;
                break;
            }
        }

        // Hover popup: open after the mouse rests on a word, with no button held and no recent typing.
        hoverFrame = ImGui.GetFrameCount();
        if(!SpellChecker.HoverSuggestions || ImGui.IsPopupOpen(PopupId)) hoverOpen = false;
        else
        {
            if(under != hoverIndex) { hoverIndex = under; hoverSinceMs = now; }
            if(hoverOpen)
            {
                var bridgeMin = new Vector2(Math.Min(anchorMin.X, popupMin.X), Math.Min(anchorMax.Y, popupMin.Y));
                var bridgeMax = new Vector2(Math.Max(anchorMax.X, popupMax.X), Math.Max(anchorMax.Y, popupMin.Y));
                var grow = new Vector2(3 * gs);
                if(InRect(mouse, anchorMin - grow, anchorMax + grow) || InRect(mouse, popupMin - grow, popupMax + grow) || InRect(mouse, bridgeMin, bridgeMax))
                    lastInsideMs = now;
                if(now - lastInsideMs > LeaveGraceMs || ImGui.IsKeyPressed(ImGuiKey.Escape)) hoverOpen = false;
            }
            if(!hoverOpen && under >= 0 && now - hoverSinceMs >= HoverDelayMs && now - lastTypeMs >= TypingQuietMs && !ImGui.IsAnyMouseDown())
            {
                var (x, width, line, _) = layout[under];
                var hp = new Vector2(1.5f * gs, 1f * gs);
                anchorMin = new Vector2(origin.X + x, origin.Y + line * lineHeight) - hp;
                anchorMax = new Vector2(origin.X + x + width, origin.Y + (line + 1) * lineHeight) + hp;
                popupMin = popupMax = new Vector2(anchorMin.X, anchorMax.Y + 2 * gs);
                hoverStart = spans[under].Start;
                hoverWord = text.Substring(hoverStart, spans[under].Length);
                hoverSuggestions = SpellChecker.SuggestAsync(hoverWord);
                refocusBytes = active && cursorChar >= 0 && cursorChar <= text.Length ? Encoding.UTF8.GetByteCount(text.AsSpan(0, cursorChar)) : -1;
                lastInsideMs = now;
                hoverOpen = true;
            }
        }

        var colour = SpellChecker.SquiggleColour;
        var style = SpellChecker.Style;
        var scale = Math.Max(1f, fontSize / 17f);
        var fill = SpellChecker.HighlightFill;
        drawList.PushClipRect(clipMin, clipMax, true);
        var points = stackalloc Vector2[256];
        for(var k = 0; k < layout.Count; k++)
        {
            var (x, width, line, lineStart) = layout[k];
            var x0 = origin.X + x;
            var x1 = x0 + width;
            var top = origin.Y + line * lineHeight;
            if(x1 < clipMin.X || x0 > clipMax.X || top + lineHeight < clipMin.Y - 2 || top > clipMax.Y + 2) continue;
            if(style == SpellStyle.Highlight)
            {
                var hot = k == under || (hoverOpen && spans[k].Start == hoverStart);
                var bMin = new Vector2(x0 - 1.5f * gs, top - 0.5f * gs);
                var bMax = new Vector2(x1 + 1.5f * gs, top + lineHeight + 0.5f * gs);
                var radius = 3f * gs;
                drawList.AddRectFilled(bMin, bMax, WithAlpha(colour, hot ? Math.Max(0.18f, fill + 0.08f) : fill), radius);
                drawList.AddRect(bMin, bMax, WithAlpha(colour, hot ? 0.80f : 0.55f), radius, ImDrawFlags.None, Math.Max(1f, gs));
                continue;
            }
            var y = top + lineHeight - 0.5f * scale;
            if(style == SpellStyle.Straight)
            {
                drawList.AddLine(new Vector2(x0, y), new Vector2(x1, y), colour, scale);
                continue;
            }
            var step = 2f * scale;
            var n = 0;
            for(var px = x0; px < x1 && n < 255; px += step, n++)
                points[n] = new Vector2(px, y + ((n & 1) == 0 ? 0 : -1.5f * scale));
            if(n < 2) points[n++] = new Vector2(x1, y);
            drawList.AddPolyline(points, n, colour, ImDrawFlags.None, scale);
        }
        drawList.PopClipRect();

        // Right click on a misspelled word opens the suggestion menu (on release, like ImGui's context menus).
        if(under >= 0 && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            targetStart = spans[under].Start;
            targetWord = text.Substring(spans[under].Start, spans[under].Length);
            suggestions = SpellChecker.SuggestAsync(targetWord);
            hoverOpen = false;
            ImGui.OpenPopup(PopupId);
            RightClickConsumed = true;
        }
    }

    internal void DrawPopup()
    {
        DrawHover();
        if(!ImGui.BeginPopup(PopupId)) return;
        ImGui.TextDisabled(targetWord);
        ImGui.Separator();
        if(DrawSuggestions(suggestions, "sugg") is { } pick) pending = (targetStart, targetWord, pick);
        ImGui.Separator();
        if(ImGui.Selectable("Add to dictionary##vierai-add")) SpellChecker.AddToDictionary(targetWord);
        if(ImGui.Selectable("Ignore##vierai-ignore")) SpellChecker.Ignore(targetWord);
        ImGui.EndPopup();
    }

    private static string? DrawSuggestions(Task<List<string>>? task, string id)
    {
        if(task is not { IsCompleted: true }) { ImGui.TextDisabled("Looking for suggestions..."); return null; }
        var list = task.IsCompletedSuccessfully ? task.Result : [];
        if(list.Count == 0) { ImGui.TextDisabled("No suggestions"); return null; }
        string? pick = null;
        for(var k = 0; k < list.Count && k < 8; k++)
            if(ImGui.Selectable($"{list[k]}##vierai-{id}{k}")) pick = list[k];
        return pick;
    }

    // The hover popup: a plain window that never takes focus when it appears, so typing stays in the input.
    // Clicking in it does move focus, so every action hands focus back through TakeReplacement.
    private void DrawHover()
    {
        if(!hoverOpen || hoverFrame != ImGui.GetFrameCount()) return;
        var gs = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowPos(new Vector2(anchorMin.X, anchorMax.Y + 2 * gs));
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings
                                     | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDocking;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6 * gs, 4 * gs));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4 * gs);
        var open = ImGui.Begin(hoverWindowName, flags);
        ImGui.PopStyleVar(2);
        if(open)
        {
            ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());
            ImGui.TextDisabled(hoverWord);
            ImGui.Separator();
            if(DrawSuggestions(hoverSuggestions, "hsugg") is { } pick)
            {
                pending = (hoverStart, hoverWord, pick);
                hoverOpen = false;
            }
            ImGui.Separator();
            if(ImGui.Selectable("Ignore##vierai-hignore")) { SpellChecker.Ignore(hoverWord); refocusPending = true; hoverOpen = false; }
            if(ImGui.Selectable("Add to dictionary##vierai-hadd")) { SpellChecker.AddToDictionary(hoverWord); refocusPending = true; hoverOpen = false; }
            popupMin = ImGui.GetWindowPos();
            popupMax = popupMin + ImGui.GetWindowSize();
        }
        ImGui.End();
    }

    // IME composition check on the game window (Dalamud draws the composition itself; marks would
    // otherwise sit under half-typed text).
    private static class Ime
    {
        private const uint GcsCompStr = 0x0008;
        private static nint hwnd;

        [DllImport("imm32.dll")] private static extern nint ImmGetContext(nint hWnd);
        [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(nint hWnd, nint hImc);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)] private static extern int ImmGetCompositionStringW(nint hImc, uint index, nint buf, uint len);

        internal static bool Composing()
        {
            try
            {
                if(hwnd == 0) hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if(hwnd == 0) return false;
                var ctx = ImmGetContext(hwnd);
                if(ctx == 0) return false;
                try { return ImmGetCompositionStringW(ctx, GcsCompStr, 0, 0) > 0; }
                finally { ImmReleaseContext(hwnd, ctx); }
            }
            catch { return false; }
        }
    }
}
