// VierAI-fied fork support (AI generated code).
// 1.40.9.27: glue for the shared RichChatInput editor (MIT licence, source copy in ChatTwo/RichChatInput) used by the
// chat input: Up/Down walks the per-tab history kept by SendHandler, and drafts are kept in a small file in the plugin
// configuration folder so a half-typed line survives closing the window and reloading the plugin.
using ChatTwo.Ui.Handler;

namespace ChatTwo.VierAIfied;

internal sealed class BacklogHistory : RichChatInput.IRichHistory
{
    private readonly SendHandler Send;

    internal BacklogHistory(SendHandler send)
    {
        Send = send;
    }

    public IReadOnlyList<string> Items => Send.InputBacklog;

    public int Index
    {
        get => Send.InputBacklogIdx;
        set => Send.InputBacklogIdx = value;
    }
}

internal static class InputEditor
{
    private static RichChatInput.RichDrafts? DraftStore;

    internal static RichChatInput.RichDrafts Drafts =>
        DraftStore ??= new RichChatInput.RichDrafts(Path.Combine(Plugin.Interface.GetPluginConfigDirectory(), "vierai-input-drafts.json"));

    internal static void Flush() => DraftStore?.Flush();
}
