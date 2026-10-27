// VierAI-fied fork support (AI generated code).
// "Send Tell" opens a conversation in XIM (Messenger) when it is installed and loaded, instead of
// putting /tell Name@World into the Chat 2 input. Falls back to the normal /tell behaviour when XIM
// is not loaded or the call fails.
namespace ChatTwo.VierAIfied;

internal static class XimTell
{
    private static readonly string[] XimInternalNames = ["MessengerVierAIfied", "Messenger"];

    public static bool XimLoaded()
    {
        try
        {
            foreach (var p in Plugin.Interface.InstalledPlugins)
                if (p.IsLoaded && XimInternalNames.Any(n => string.Equals(p.InternalName, n, StringComparison.OrdinalIgnoreCase)))
                    return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"XimTell: could not read installed plugins: {ex.Message}");
        }
        return false;
    }

    /// <summary>Try to open (and focus) the XIM conversation. Returns false if the caller should use /tell.</summary>
    public static bool TryOpen(string name, string world)
    {
        if (!Plugin.Config.VierAITellOpensXim || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world) || !XimLoaded())
            return false;

        var target = $"{name}@{world}";
        try
        {
            // XIM's IPC label (kept as "Messenger." by the fork): OpenMessenger(string nameWithWorld, bool setFocus)
            Plugin.Interface.GetIpcSubscriber<string, bool, object>("Messenger.OpenMessenger").InvokeAction(target, true);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"XimTell: IPC failed, trying /xim: {ex.Message}");
        }

        try
        {
            if (Plugin.CommandManager.Commands.ContainsKey("/xim") && Plugin.CommandManager.ProcessCommand($"/xim {target}"))
                return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"XimTell: /xim failed, using /tell: {ex.Message}");
        }
        return false;
    }
}