// VierAI-fied fork support (AI generated code).
// Input history for the chat box (Up/Down recall). Each tab keeps its own list (Tab.InputHistory),
// in memory only; it is cleared when a popped-out tab is closed. Down past the newest entry restores
// what was being typed.
using Dalamud.Bindings.ImGui;
using ChatTwo.Ui.Handler;

namespace ChatTwo.VierAIfied;

// Up/Down handling for one input box. Up on the line recalls older entries, Down walks forward again
// and finally gives back the text that was being typed.
internal sealed class HistoryNavigator
{
    private readonly SendHandler Send;
    private string? Draft;

    internal HistoryNavigator(SendHandler send)
    {
        Send = send;
    }

    internal int OnHistoryKey(ref ImGuiInputTextCallbackData data, ImGuiKey key)
    {
        var list = Send.InputBacklog;
        var previous = Send.InputBacklogIdx;
        var index = Math.Min(previous, list.Count - 1);

        switch (key)
        {
            case ImGuiKey.UpArrow:
                if (list.Count == 0)
                    return 0;

                if (index == -1)
                {
                    Draft = ReadBuffer(ref data);
                    index = list.Count - 1;
                }
                else if (index > 0)
                {
                    index--;
                }
                break;
            case ImGuiKey.DownArrow:
                if (index == -1)
                    return 0;

                if (++index >= list.Count)
                    index = -1;
                break;
        }

        if (index == previous)
            return 0;

        Send.InputBacklogIdx = index;
        var text = index >= 0 ? list[index] : Draft ?? string.Empty;
        if (index == -1)
            Draft = null;

        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, text);
        return 0;
    }

    private static unsafe string ReadBuffer(ref ImGuiInputTextCallbackData data)
    {
        return System.Text.Encoding.UTF8.GetString(data.Buf, data.BufTextLen);
    }
}