using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using ChatTwo.Util;

namespace ChatTwo.Ui.ChatLog;

// VierAI-fied (AI generated code): fade the main chat window while it is not in use.
//
// "In use" means the mouse is over the window (or one of its child windows), the chat input is
// active, or a popup such as the right-click menu is open. While in use the window looks exactly as
// it does with the feature off (background at the existing Window opacity, everything else at 100%).
// Otherwise the window eases down by a fixed step per frame, and eases back up the same way. Since
// 1.41.2.10 there are two unfocused levels: the window (background, buttons, input) and the lines
// (message text), so the lines can stay more readable while the window fades further.
//
// New lines: with a new message fade time of D seconds, a line is drawn at full opacity for the first D/2
// seconds after it arrived, then fades on a smoothstep curve, by its own age (not per frame), down to
// the resting line opacity over the next D/2. Lines therefore fade one at a time in the order they
// arrived, and a line is never dimmer than the resting line level (also while it fades back up).
// A line that scrolls out of view is simply not drawn, so it no longer shows. No per-line state is kept.
//
// Line background (1.41.2.7, outline 1.41.2.8, soft edge 1.41.2.9, blended 1.41.2.10): a line drawn brighter
// than the resting line level also gets its own background (window background colour), so it stays readable
// over a light scene. Together with the faded window background it adds up to Window opacity at a fresh line
// and to nothing extra once the line is back at its resting level. It follows the text: every drawn text
// piece, icon and emote of the row reports its box (ImGuiUtil.FadeBgRecord). Boxes are grouped per visual
// line (wrapped lines and the timestamp cell included), and each line gets one padded rectangle centred on
// the font's actual glyph extents (cap height to descender), so the gap above and below the letters is equal.
// The rectangles of the whole frame are collected and drawn together, behind the log, on the draw list of
// the window that holds it (drawn before the log's child window). Neighbouring lines and rows that are close
// meet halfway with no soft edge between them, so a run of bright lines becomes one shape and nothing is
// drawn twice; only the outer edges of a run get the soft edge (three wider layers whose opacity builds up
// from the outside in). That keeps the background even when the window background is below 100%. Its peak
// opacity is scaled by the "Bright line background max opacity" setting.
public partial class ChatLog
{
    private float FadeCurrent = 1f; // window (background, buttons, input)
    private float FadeLineCurrent = 1f; // resting level of the message lines
    private bool FadeEngaged;
    private bool FadePushed;
    private float FadeBaseAlpha = 1f;

    // Multiplier for the window background alpha that Chat 2 hands to Dalamud as BgAlpha (1 when the feature is off).
    private float FadeBackgroundFactor => Plugin.Config.VierAIFade ? FadeCurrent : 1f;

    private static float FadeStep => Math.Clamp(Plugin.Config.VierAIFadeStep, 0.1f, 100f) / 100f;

    private static float FadeEase(float current, float target, float step)
    {
        if (!float.IsFinite(current))
            current = 1f;
        return current < target
            ? Math.Min(target, current + step)
            : Math.Max(target, current - step);
    }

    // Once per frame, before the window begins.
    private void FadeUpdate()
    {
        if (!Plugin.Config.VierAIFade)
        {
            FadeCurrent = 1f;
            FadeLineCurrent = 1f;
            FadeEngaged = false;
            return;
        }

        Plugin.Config.SanitizeFade();

        var engaged = FadeEngaged || InputHandler.Activate || InputHandler.InputFocused;
        var target = engaged ? 1f : Math.Clamp(Plugin.Config.VierAIFadeUnfocusedOpacity, 0f, 100f) / 100f;
        var lineTarget = engaged ? 1f : Math.Clamp(Plugin.Config.VierAIFadeLineUnfocusedOpacity, 0f, 100f) / 100f;
        var step = FadeStep;
        FadeCurrent = FadeEase(FadeCurrent, target, step);
        FadeLineCurrent = FadeEase(FadeLineCurrent, lineTarget, step);
    }

    // Inside the main window, once per drawn frame.
    private void FadeTrackEngaged()
    {
        if (!Plugin.Config.VierAIFade)
            return;

        FadeEngaged = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                      || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
                      || InputHandler.InputFocused;
    }

    // Called from FinalizeFrame when the window was not drawn this frame.
    private void FadeNotDrawn()
    {
        FadeEngaged = false;
    }

    // PreDraw, after any style override was pushed.
    private void FadePush()
    {
        FadePushed = false;
        if (!Plugin.Config.VierAIFade)
            return;

        FadeBaseAlpha = ImGui.GetStyle().Alpha;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, FadeBaseAlpha * FadeCurrent);
        FadePushed = true;
    }

    // PostDraw, before any style override is popped.
    private void FadePop()
    {
        if (!FadePushed)
            return;

        ImGui.PopStyleVar();
        FadePushed = false;
    }

    // Line opacity for a message of the given age (seconds) with fade time D: full for D/2, then a
    // smoothstep from 1 down to the resting level over the next D/2. Never below the resting level.
    internal static float FadeLineOpacity(double ageSeconds, float delay, float window)
    {
        if (!(delay > 0f) || !double.IsFinite(ageSeconds))
            return window;

        var half = delay / 2f;
        var t = (float)((ageSeconds - half) / half);
        if (t <= 0f)
            return 1f;
        if (t >= 1f)
            return window;

        var eased = t * t * (3f - 2f * t); // smoothstep
        return Math.Max(window, 1f + (window - 1f) * eased);
    }

    // Background level a line at opacity "line" should show: the window level at the resting line opacity,
    // rising linearly to 1 (the full Window opacity) at a fresh line. Equal to "line" when both rest levels match.
    internal static float FadeBgLevel(float line, float lineRest, float window)
    {
        if (!float.IsFinite(line) || !float.IsFinite(lineRest) || !float.IsFinite(window))
            return 0f;
        window = Math.Clamp(window, 0f, 1f);
        if (lineRest >= 1f || line <= lineRest)
            return window;
        var frac = Math.Clamp((line - lineRest) / (1f - lineRest), 0f, 1f);
        return window + frac * (1f - window);
    }

    // Background alpha a bright line's own rectangle needs so that, drawn over the faded window background,
    // the two add up to Window opacity x the wanted level. 0 when the window background already covers it.
    internal static float FadeLineBackgroundAlpha(float windowAlpha, float window, float line, float maxOpacity = 1f)
    {
        if (!float.IsFinite(windowAlpha) || !float.IsFinite(window) || !float.IsFinite(line))
            return 0f;
        maxOpacity = float.IsFinite(maxOpacity) ? Math.Clamp(maxOpacity, 0f, 1f) : 1f;
        var below = Math.Clamp(windowAlpha * window, 0f, 1f); // what the faded window background already shows
        var want = Math.Clamp(windowAlpha * line, 0f, 1f);
        if (want <= below || below >= 1f)
            return 0f;
        return Math.Clamp((want - below) / (1f - below), 0f, 1f) * maxOpacity;
    }

    // Opacity of each soft-edge layer, outermost first, so that drawing them in order over each other adds up
    // to peak * (i + 1) / layers at layer i. Layer alpha = 1 - (1 - cumulative) / (1 - previous cumulative).
    internal static void FadeBgLayerAlphas(float peak, Span<float> alphas)
    {
        peak = float.IsFinite(peak) ? Math.Clamp(peak, 0f, 1f) : 0f;
        var n = alphas.Length;
        var previous = 0f;
        for (var i = 0; i < n; i++)
        {
            var cumulative = peak * (i + 1) / n;
            var a = 1f - (1f - cumulative) / Math.Max(1f - previous, 0.0001f);
            alphas[i] = Math.Clamp(a, 0f, 1f);
            previous = cumulative;
        }
    }

    // One background rectangle of the frame (core, before the soft edge), with the peak opacity of its row.
    private readonly record struct FadeBgLine(float Left, float Top, float Right, float Bottom, float Peak);

    private bool FadeBgWanted; // this row gets a background
    private bool FadeBgRecording; // item boxes of the current row are being collected
    private float FadeBgRowPeak;
    private float FadeBgInkTop; // glyph extents of the current font, from the top of a text line
    private float FadeBgInkBottom;
    private float FadeBgTextHeight;
    private bool FadeBgInkValid;
    private const int FadeBgLayers = 4; // soft edge: three feather layers and the core
    private static readonly List<Vector4> FadeBgItems = new(64); // reused: x0, y0, x1, y1 of drawn items of one row
    private readonly List<FadeBgLine> FadeBgFrame = new(64); // reused: all background rectangles of this frame

    // Draw list of the window that holds the log child window (drawn behind it), and the log's clip rectangle.
    private ImDrawListPtr FadeBgTarget;
    private bool FadeBgHasTarget;
    private Vector2 FadeBgClipMin;
    private Vector2 FadeBgClipMax;

    private static float FadeBgPadX => 2f * ImGuiHelpers.GlobalScale;    // core padding left and right
    private static float FadeBgPadY => 2f * ImGuiHelpers.GlobalScale;    // core padding above the cap height and below the descenders
    private static float FadeBgFeather => 3f * ImGuiHelpers.GlobalScale; // how far the soft edge reaches outward
    private static float FadeBgRounding => 3f * ImGuiHelpers.GlobalScale;

    // Before the log child window begins: remember the outer window's draw list.
    private void FadeBgCaptureTarget()
    {
        FadeBgHasTarget = false;
        FadeBgFrame.Clear();
        if (!FadePushed)
            return;
        FadeBgTarget = ImGui.GetWindowDrawList();
        FadeBgHasTarget = true;
    }

    // Right after the log child window began: its clip rectangle.
    private void FadeBgCaptureClip()
    {
        if (!FadeBgHasTarget)
            return;
        var dl = ImGui.GetWindowDrawList();
        FadeBgClipMin = dl.GetClipRectMin();
        FadeBgClipMax = dl.GetClipRectMax();
    }

    private void FadeBgReleaseTarget()
    {
        FadeBgHasTarget = false;
        FadeBgFrame.Clear();
    }

    // Glyph extents of the current font, scaled to the current font size: top of the tallest of a few
    // ascender letters and bottom of the lowest of a few descender letters, measured from the top of a line.
    private unsafe void FadeBgMeasureInk()
    {
        FadeBgInkValid = false;
        FadeBgTextHeight = ImGui.GetTextLineHeight();
        var font = ImGui.GetFont();
        if (font.Handle == null)
            return;
        var baseSize = font.FontSize;
        var size = ImGui.GetFontSize();
        if (!(baseSize > 0f) || !(size > 0f) || !float.IsFinite(baseSize) || !float.IsFinite(size))
            return;
        var scale = size / baseSize;

        var top = float.MaxValue;
        foreach (var c in "Hdlbk")
        {
            var g = font.FindGlyphNoFallback(c);
            if (g != null && float.IsFinite(g->Y0))
                top = Math.Min(top, g->Y0 * scale);
        }

        var bottom = float.MinValue;
        foreach (var c in "gpjyq")
        {
            var g = font.FindGlyphNoFallback(c);
            if (g != null && float.IsFinite(g->Y1))
                bottom = Math.Max(bottom, g->Y1 * scale);
        }

        // Keep it only when it looks like a real text font; otherwise the plain line box is used.
        if (top < -0.25f * size || top > 0.6f * size || bottom < 0.4f * size || bottom > 1.4f * size || bottom - top < 0.3f * size)
            return;

        FadeBgInkTop = top;
        FadeBgInkBottom = bottom;
        FadeBgInkValid = true;
    }

    // Start collecting item boxes for the current row.
    private void FadeBgOpen()
    {
        if (!FadeBgWanted || FadeBgRecording || !FadeBgHasTarget)
            return;
        FadeBgMeasureInk();
        FadeBgItems.Clear();
        ImGuiUtil.FadeBgItems = FadeBgItems;
        FadeBgRecording = true;
    }

    // Turn the row's item boxes into one rectangle per visual line and add them to the frame.
    private void FadeBgClose()
    {
        if (!FadeBgRecording)
            return;
        FadeBgRecording = false;
        ImGuiUtil.FadeBgItems = null;

        var items = FadeBgItems;
        if (items.Count == 0)
            return;

        var padX = FadeBgPadX;
        var padY = FadeBgPadY;
        var textHeight = FadeBgTextHeight;
        var line = items[0];
        for (var i = 1; i <= items.Count; i++)
        {
            if (i < items.Count)
            {
                var it = items[i];
                // Same visual line while the item starts above the current line's bottom.
                if (it.Y < line.W - 0.5f)
                {
                    line = new Vector4(Math.Min(line.X, it.X), Math.Min(line.Y, it.Y), Math.Max(line.Z, it.Z), Math.Max(line.W, it.W));
                    continue;
                }
            }

            float top, bottom;
            if (FadeBgInkValid && textHeight > 0f)
            {
                top = line.Y + FadeBgInkTop - padY;
                bottom = line.W - textHeight + FadeBgInkBottom + padY;
            }
            else
            {
                top = line.Y; // 1.41.2.9 look
                bottom = line.W + 2f * ImGuiHelpers.GlobalScale;
            }

            if (float.IsFinite(top) && float.IsFinite(bottom) && float.IsFinite(line.X) && float.IsFinite(line.Z))
                FadeBgFrame.Add(new FadeBgLine(line.X - padX, top, line.Z + padX, Math.Max(bottom, top + 1f), FadeBgRowPeak));

            if (i < items.Count)
                line = items[i];
        }

        items.Clear();
    }

    // After the message loop: draw every collected rectangle on the outer window's draw list, behind the log.
    // Lines that are close (vertically within two soft edges, and overlapping sideways) meet halfway and get no
    // soft edge between them, so overlapping layers never add up.
    private void FadeBgFlush()
    {
        var lines = FadeBgFrame;
        if (!FadeBgHasTarget || lines.Count == 0)
        {
            lines.Clear();
            return;
        }

        var dl = FadeBgTarget;
        var feather = FadeBgFeather;
        var round = FadeBgRounding;
        var join = 2f * feather + 1f * ImGuiHelpers.GlobalScale;
        var rgb = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        Span<float> layers = stackalloc float[FadeBgLayers];

        dl.PushClipRect(FadeBgClipMin, FadeBgClipMax, false);
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (!(l.Peak > 0.004f))
                continue;

            var joinUp = i > 0 && FadeBgNear(lines[i - 1], l, join);
            var joinDown = i < lines.Count - 1 && FadeBgNear(l, lines[i + 1], join);
            var top = joinUp ? (lines[i - 1].Bottom + l.Top) / 2f : l.Top;
            var bottom = joinDown ? (l.Bottom + lines[i + 1].Top) / 2f : l.Bottom;
            if (!(bottom > top))
                continue;

            var flags = joinUp && joinDown ? ImDrawFlags.RoundCornersNone
                : joinUp ? ImDrawFlags.RoundCornersBottom
                : joinDown ? ImDrawFlags.RoundCornersTop
                : ImDrawFlags.RoundCornersAll;

            FadeBgLayerAlphas(l.Peak, layers);
            for (var layer = 0; layer < FadeBgLayers; layer++)
            {
                var e = feather * (FadeBgLayers - 1 - layer) / (FadeBgLayers - 1);
                var colour = rgb with { W = layers[layer] };
                var r = flags == ImDrawFlags.RoundCornersNone ? 0f : round + e;
                dl.AddRectFilled(
                    new Vector2(l.Left - e, joinUp ? top : top - e),
                    new Vector2(l.Right + e, joinDown ? bottom : bottom + e),
                    ImGui.ColorConvertFloat4ToU32(colour), r, flags);
            }
        }
        dl.PopClipRect();

        lines.Clear();
    }

    private static bool FadeBgNear(FadeBgLine upper, FadeBgLine lower, float join)
        => lower.Top - upper.Bottom <= join && Math.Min(upper.Right, lower.Right) > Math.Max(upper.Left, lower.Left);

    // Table mode: the timestamp and the message are in different columns; the row keeps collecting across both.
    private void FadeBgNextColumn()
    {
        ImGui.TableNextColumn();
    }

    // Around one message row. Returns true when an alpha was pushed; pair it with FadeLineEnd.
    private bool FadeLineBegin(Message message, bool isTable)
    {
        if (!FadePushed)
            return false;

        var window = FadeCurrent;
        var rest = FadeLineCurrent;
        if (window >= 1f && rest >= 1f)
            return false;

        var alpha = rest;
        var delay = Plugin.Config.VierAIFadeNewLineSeconds;
        if (delay > 0f)
        {
            var age = (DateTimeOffset.UtcNow - message.Date).TotalSeconds;
            if (age < delay)
                alpha = FadeLineOpacity(age, delay, rest);
        }

        alpha = float.IsFinite(alpha) ? Math.Clamp(alpha, 0f, 1f) : 1f;
        var bgAlpha = alpha > rest
            ? FadeLineBackgroundAlpha(Math.Clamp(Plugin.Config.WindowAlpha, 0f, 100f) / 100f, window, FadeBgLevel(alpha, rest, window), Plugin.Config.VierAIFadeLineBgMax / 100f)
            : 0f;

        if (Math.Abs(alpha - window) < 0.001f && bgAlpha <= 0.004f)
            return false; // drawn exactly like the rest of the window

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, FadeBaseAlpha * alpha);
        FadeLineOpen = true;

        if (bgAlpha > 0.004f)
        {
            FadeBgRowPeak = bgAlpha;
            FadeBgWanted = true;
            FadeBgOpen();
        }

        return true;
    }

    private bool FadeLineOpen;

    // Also called after the message loop (even after an exception) so the style stack stays balanced.
    private void FadeLineEnd(bool pushed = true)
    {
        if (!pushed || !FadeLineOpen)
            return;

        FadeBgClose();
        FadeBgWanted = false;

        ImGui.PopStyleVar();
        FadeLineOpen = false;
    }
}
