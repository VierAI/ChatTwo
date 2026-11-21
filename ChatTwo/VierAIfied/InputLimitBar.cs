// VierAI-fied fork support (AI generated code).
// Thin bar under the chat input showing how many UTF-8 bytes the line will use against the game's
// 500-byte per-line cap. The count follows what SendHandler.SendChatBox actually sends: a typed
// command as-is, a direct tell as the bare message, and anything else with the channel prefix
// ("/s ", "/p ", "/tell Name@World " ...) in front. Auto-translate entries are counted as typed.
// Toggle: Settings > Chat Log, "Show the message length bar" (VierAIInputLimitBar, on by default).
// 1.40.9.27: drawn by the shared RichChatInput editor (white, to yellow by 75 %, to red by 90 %; count from 80 %).
using System.Numerics;
using System.Text;
using ChatTwo.Code;
using ChatTwo.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace ChatTwo.VierAIfied;

internal static class InputLimitBar
{
    internal const int MaxBytes = 500;

    // Space the bar takes below the input row (bar row plus one row of item spacing). 1.40.9.27: the bar, its colours
    // and the count shown from 80 % are drawn by the RichChatInput editor.
    internal static float ReservedHeight(RichChatInput.RichInput editor) => editor.BarRowHeight + ImGui.GetStyle().ItemSpacing.Y;

    internal static int LineBytes(Tab tab, string input)
    {
        var text = input.AsSpan().Trim();
        if (text.IsEmpty)
            return 0;

        var bytes = Encoding.UTF8.GetByteCount(text);
        if (text[0] == '/')
            return bytes;

        var channel = tab.CurrentChannel;
        var target = tab.TellTarget.IsSet() ? tab.TellTarget : channel.TempTellTarget ?? channel.TellTarget;
        if (target != null)
            return target.ContentId == 0 ? bytes + Encoding.UTF8.GetByteCount($"/tell {target.ToTargetString()} ") : bytes;

        var prefix = channel.UseTempChannel ? channel.TempChannel.Prefix() : channel.Channel.Prefix();
        return bytes + Encoding.UTF8.GetByteCount(prefix) + 1;
    }

    internal static void Draw(Tab tab, string input, RichChatInput.RichInput editor)
    {
        if (!Plugin.Config.VierAIInputLimitBar)
            return;

        editor.DrawLengthBar(ImGui.GetContentRegionAvail().X, LineBytes(tab, input), MaxBytes);
    }
}