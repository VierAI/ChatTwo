// VierAI-fied fork support (AI generated code).
// Tab completion: when exactly one entry matches, fill it in straight away instead of opening
// the selection popup. Plain-text entries (emotes, text commands) insert their text, never <at:..>.
using ChatTwo.Ui;
using ChatTwo.Ui.Handler;
using ChatTwo.Util;

namespace ChatTwo.VierAIfied;

internal static class SingleCompletion
{
    internal static bool TryApply(InputHandler input, AutoCompleteInfo info, List<AutoTranslateEntry> list)
    {
        if (list.Count != 1 || string.IsNullOrEmpty(info.ToComplete))
            return false;

        var entry = list[0];
        var before = input.ChatInput[..info.StartPos];
        var after = input.ChatInput[info.EndPos..];
        var replacement = entry.PlainText ? entry.Text : $"<at:{entry.Group},{entry.Row}>";
        input.ChatInput = $"{before}{replacement}{after}";
        input.Activate = true;
        input.ActivatePos = info.StartPos + replacement.Length;
        return true;
    }
}
