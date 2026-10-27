// VierAI-fied fork support (AI generated code).
// Tab completion: when exactly one entry matches and it starts with the typed word, fill it in
// straight away instead of opening the selection popup. The edit is made on ImGui's own input
// buffer inside the input callback; changing ChatInput while the box is active gets overwritten
// by ImGui on the next frame (that was the "flicker then revert" bug). Anything else (no match,
// several matches, or a fuzzy-only match) opens the normal search popup.
using System.Text;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.VierAIfied;

internal static class SingleCompletion
{
    internal static unsafe bool TryApplyInCallback(ref ImGuiInputTextCallbackData data, int startByte, string toComplete)
    {
        if (string.IsNullOrEmpty(toComplete))
            return false;

        var list = AutoTranslate.Matching(toComplete, Plugin.Config.SortAutoTranslate);
        if (list.Count != 1)
            return false;

        var entry = list[0];
        if (!entry.Text.StartsWith(toComplete, StringComparison.OrdinalIgnoreCase))
            return false;

        var replacement = Encoding.UTF8.GetBytes(AutoTranslateDisplay.Insertion(entry));
        var endByte = data.CursorPos;
        var tailLen = data.BufTextLen - endByte;
        var newLen = startByte + replacement.Length + tailLen;
        if (startByte < 0 || endByte < startByte || newLen >= data.BufSize)
            return false;

        var buf = new Span<byte>(data.Buf, data.BufSize);
        var tail = buf.Slice(endByte, tailLen).ToArray();
        replacement.CopyTo(buf[startByte..]);
        tail.CopyTo(buf[(startByte + replacement.Length)..]);
        buf[newLen] = 0;
        data.BufTextLen = newLen;
        data.BufDirty = 1;
        data.CursorPos = startByte + replacement.Length;
        data.SelectionStart = data.SelectionEnd = data.CursorPos;
        return true;
    }
}
