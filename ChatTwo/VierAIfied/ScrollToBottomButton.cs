// VierAI-fied fork support (AI generated code).
// Floating "scroll to bottom" button: when the message log is scrolled up, a small down-arrow button is drawn
// in the bottom-right corner of the log; clicking it jumps to the newest message, which resumes auto-scroll.
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace ChatTwo.VierAIfied;

internal static class ScrollToBottomButton
{
    /// <summary>Call inside the scrolling child, after its content. Returns true when clicked.</summary>
    public static bool Draw(string id)
    {
        var maxY = ImGui.GetScrollMaxY();
        // Hide when at (or within about one line of) the bottom, so new messages arriving don't make it flicker.
        if (maxY <= 0 || ImGui.GetScrollY() >= maxY - ImGui.GetFrameHeight())
            return false;

        var style = ImGui.GetStyle();
        var saved = ImGui.GetCursorPos();
        var icon = FontAwesomeIcon.ArrowDown.ToIconString();
        ImGui.PushFont(UiBuilder.IconFont);
        var size = ImGui.CalcTextSize(icon) + style.FramePadding * 2;
        var pad = 6f * ImGuiHelpers.GlobalScale;
        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        ImGui.SetCursorScreenPos(new Vector2(
            winPos.X + winSize.X - style.ScrollbarSize - pad - size.X,
            winPos.Y + winSize.Y - pad - size.Y));
        var clicked = ImGui.Button($"{icon}##{id}", size);
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Scroll to bottom");
        ImGui.SetCursorPos(saved);
        if (clicked)
            ImGui.SetScrollY(maxY);
        return clicked;
    }
}