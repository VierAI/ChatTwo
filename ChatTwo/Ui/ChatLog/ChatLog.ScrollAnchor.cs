using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui.ChatLog;

// VierAI-fied (AI generated code): keep a scrolled-up log on the lines being read when old lines are dropped.
//
// When the tab is full (10,000 messages) or longer than "Max lines to show", every new message removes
// the oldest row from the top of what is drawn. The scroll position is an offset from the top of the
// content, so without help the remaining rows move up under a fixed offset and the log scrolls by.
//
// Each frame the content Y of the first rows is remembered per tab. On the next frame, if the first
// drawn message changed and that message is among the remembered rows, the height that was removed
// above it is known exactly (difference of the two remembered Y values). That height is put back as
// a spacer at the top for this one frame, so nothing moves on screen, and the scroll offset is lowered
// by the same amount for the next frame, when the spacer is gone. Once the offset reaches 0 the rows
// scroll off the top as before. At the bottom (auto-scroll) nothing changes.
public partial class ChatLog
{
    private const int AnchorTrackedRows = 128;

    private sealed class ScrollAnchorState
    {
        public readonly Guid[] Ids = new Guid[AnchorTrackedRows];
        public readonly float[] Ys = new float[AnchorTrackedRows];
        public int Count;
        public int Frame = -10;
    }

    private readonly Dictionary<Guid, ScrollAnchorState> ScrollAnchors = [];

    // Before the rows are drawn. Returns the height removed above the first row since the previous frame
    // (0 when nothing needs to be held), and starts a fresh record for this frame.
    private float ScrollAnchorBegin(Guid tab, Message? first, bool reset, float scrollY, float tolerance, out ScrollAnchorState state)
    {
        if (!ScrollAnchors.TryGetValue(tab, out state!))
        {
            state = new ScrollAnchorState();
            ScrollAnchors[tab] = state;
        }

        var frame = ImGui.GetFrameCount();
        var removed = 0f;
        var atBottom = scrollY >= ImGui.GetScrollMaxY() - tolerance;
        if (!reset && !atBottom && first != null && state.Frame == frame - 1 && state.Count > 1 && state.Ids[0] != first.Id)
        {
            for (var k = 1; k < state.Count; k++)
            {
                if (state.Ids[k] != first.Id)
                    continue;

                removed = state.Ys[k] - state.Ys[0];
                break;
            }
        }

        if (!float.IsFinite(removed) || removed < 0f)
            removed = 0f;

        state.Count = 0;
        state.Frame = frame;
        return removed;
    }

    // At the top of each row, before anything is skipped or drawn for it.
    private static void ScrollAnchorRecord(ScrollAnchorState state, Message message, float contentY)
    {
        if (state.Count >= AnchorTrackedRows)
            return;

        state.Ids[state.Count] = message.Id;
        state.Ys[state.Count] = contentY;
        state.Count++;
    }

    // After the rows are drawn: lower the scroll offset by what was removed, for the next frame.
    private static void ScrollAnchorEnd(float removed, float scrollY)
    {
        if (removed > 0f)
            ImGui.SetScrollY(Math.Max(0f, scrollY - removed));
    }
}
