using System.Numerics;
using ChatTwo.Ui.Handler;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using VierAIfiedShared;

namespace ChatTwo.Ui.ChatLog;

// VierAI-fied (AI generated code): click-and-drag selection with copy in the message log.
// 1.40.9.20: character-level. A left drag that starts on or near text selects from that character, within a line or
// across lines and messages; a plain click still activates links (on release); a drag that starts on empty space still
// moves the window. Ctrl+C copies (and is kept from the game), and the existing right-click menu gets "Copy link" for
// the web address under the mouse and "Copy selection" while something is selected.
public partial class ChatLog
{
    private readonly TextSelection Selection = new();
    /// <summary>Last frame: mouse was over/near text in the log. Used by PreOpenCheck to stop the window moving.</summary>
    private bool SelMouseNearText;

    private float SelPadX => 12f * ImGuiHelpers.GlobalScale;
    private float SelPadY => 3f * ImGuiHelpers.GlobalScale;

    private static float SelMeasure(string s) => ImGui.CalcTextSize(s).X;

    private void SelectionBeginFrame()
    {
        Selection.BeginFrame();
        ImGuiUtil.SelRecorder = null;
        var on = Plugin.Config.VierAIDragSelect;
        ImGuiUtil.ClickOnRelease = on;
        ImGuiUtil.BlockRelease = Selection.BlockRelease;
        if (!on && (Selection.Anchor != null || Selection.End != null))
            Selection.Clear();
    }

    private void SelectionRecordBegin(Message message)
    {
        if (!Plugin.Config.VierAIDragSelect)
            return;
        ImGuiUtil.SelRecorder = Selection;
        ImGuiUtil.SelMsg = message.Id;
        ImGuiUtil.SelChunkBase = 0;
    }

    private static void SelectionRecordEnd() => ImGuiUtil.SelRecorder = null;

    /// <summary>Plain text of a message as the selection counts it: sender text chunks then content text chunks.</summary>
    private static string SelectionPlainText(Message m)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in m.Sender)
            if (c is TextChunk t) sb.Append(t.Content);
        foreach (var c in m.Content)
            if (c is TextChunk t) sb.Append(t.Content);
        return sb.ToString();
    }

    /// <summary>Draws the highlight over the selected characters of a message's drawn pieces.</summary>
    private void SelectionRow(Message message, bool selected, int from, int to)
    {
        if (!selected || !Plugin.Config.VierAIDragSelect)
            return;
        var dl = ImGui.GetWindowDrawList();
        var col = ImGui.GetColorU32(ImGuiCol.TextSelectedBg);
        var segs = Selection.Segs;
        for (var i = segs.Count - 1; i >= 0; i--)
        {
            var s = segs[i];
            if (s.Id != message.Id)
                break; // this message's pieces are the last ones recorded
            var a = Math.Max(from, s.Start);
            var b = Math.Min(to, s.Start + s.Len);
            if (b <= a)
                continue;
            var x0 = TextSelection.XAt(s, a - s.Start, SelMeasure);
            var x1 = TextSelection.XAt(s, b - s.Start, SelMeasure);
            dl.AddRectFilled(new Vector2(x0, s.Y), new Vector2(Math.Max(x1, x0 + 1), s.Y + s.H), col);
        }
    }

    private void SelectionEndFrame(IReadOnlyList<Message> messages)
    {
        ImGuiUtil.SelRecorder = null;
        ImGuiUtil.ClickOnRelease = false;
        if (!Plugin.Config.VierAIDragSelect)
        {
            SelMouseNearText = false;
            PayloadHandler.VierAILinkText = null;
            return;
        }

        var mouse = ImGui.GetMousePos();
        var hovered = ImGui.IsWindowHovered();
        var segs = Selection.Segs;
        SelMouseNearText = hovered && TextSelection.NearText(segs, mouse.X, mouse.Y, SelPadX, SelPadY);

        var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var dragging = ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4f * ImGuiHelpers.GlobalScale);
        // Char under the mouse (clamped to the nearest line) only when it is needed: at the press and while dragging.
        TextSelection.Pos? hit = null;
        if ((clicked && hovered && SelMouseNearText) || (Selection.PressedOnText && dragging))
            hit = TextSelection.Hit(segs, mouse.X, mouse.Y, SelMeasure, true);
        Selection.UpdateMouse(hovered, clicked, dragging, ImGui.IsMouseReleased(ImGuiMouseButton.Left), SelMouseNearText, hit);

        if (Selection.Dragging)
        {
            // Auto-scroll when dragging past the top or bottom edge.
            var top = ImGui.GetWindowPos().Y;
            var bottom = top + ImGui.GetWindowHeight();
            if (mouse.Y < top)
                ImGui.SetScrollY(Math.Max(0, ImGui.GetScrollY() - (top - mouse.Y) * 0.5f));
            else if (mouse.Y > bottom)
                ImGui.SetScrollY(Math.Min(ImGui.GetScrollMaxY(), ImGui.GetScrollY() + (mouse.Y - bottom) * 0.5f));
        }

        // The existing right-click menu opens this frame; snapshot what it offers now while the messages are locked.
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            PayloadHandler.VierAILinkText = null;
            if (TextSelection.Hit(segs, mouse.X, mouse.Y, SelMeasure, false) is { } at)
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (messages[i].Id != at.Id)
                        continue;
                    PayloadHandler.VierAILinkText = TextSelection.UrlAt(SelectionPlainText(messages[i]), at.Char);
                    break;
                }
            }

            PayloadHandler.VierAISelectionText = null;
            if (Selection.HasSelection)
            {
                var text = Selection.BuildText(messages, m => m.Id, SelectionPlainText, out var n);
                PayloadHandler.VierAISelectionText = text.Length > 0 ? text : null;
                PayloadHandler.VierAISelectionCount = n;
            }
        }

        if (!Selection.HasSelection)
        {
            PayloadHandler.VierAISelectionText = null;
            return;
        }

        var io = ImGui.GetIO();
        var winHov = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var winFoc = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        // io.WantTextInput is recomputed by NewFrame from last frame's active text box, so here it means "typing".
        var typing = io.WantTextInput;
        if ((winHov || winFoc) && !typing)
        {
            CopyKeyGuard.MarkCopyable();
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.C, false))
            {
                var text = Selection.BuildText(messages, m => m.Id, SelectionPlainText, out _);
                if (text.Length > 0)
                    ImGui.SetClipboardText(text);
            }
            else if (winFoc && ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                Selection.Clear();
            }
        }
        // Keep Ctrl+C from reaching the game (it opens the currency window) while Ctrl is held over a selection.
        if (CopyKeyGuard.ShouldArm(Selection.HasSelection, winHov, winFoc, typing, io.KeyCtrl))
            CopyKeyGuard.Arm();
    }
}