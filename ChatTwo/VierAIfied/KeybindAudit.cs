// VierAI-fied fork support (AI generated code).
// Diagnostics for the game chat keybinds Chat 2 listens for. The bindings are read from the game
// (UIInputData) and refreshed every few seconds, so this always shows what is bound right now.
using ChatTwo.GameFunctions;
using ChatTwo.GameFunctions.Types;
using Dalamud.Game.ClientState.Keys;

namespace ChatTwo.VierAIfied;

internal static class KeybindAudit
{
    internal static void Register(Plugin plugin)
    {
        plugin.Commands.Register("/chat2keys", "Shows the game chat keybinds Chat 2 is listening for right now.", true).Execute += (_, _) => Print(plugin);
    }

    // Debug log line for every chat keybind that fired (Dalamud log level Debug).
    internal static void LogMatch(KeyboardSource source, string name, VirtualKey key, ModifierFlag modifiers)
    {
        Plugin.Log.Debug($"Chat keybind {name} fired from {source}: {Describe(key, modifiers)}");
    }

    private static string Describe(VirtualKey key, ModifierFlag modifiers)
    {
        var text = string.Empty;
        if (modifiers.HasFlag(ModifierFlag.Ctrl))
            text += "Ctrl+";
        if (modifiers.HasFlag(ModifierFlag.Alt))
            text += "Alt+";
        if (modifiers.HasFlag(ModifierFlag.Shift))
            text += "Shift+";
        return text + key;
    }

    private static void Print(Plugin plugin)
    {
        var manager = plugin.Functions.KeybindManager;
        var bound = 0;
        foreach (var (name, bind) in manager.CurrentKeybinds.OrderBy(pair => pair.Key))
        {
            var parts = new List<string>();
            if (bind.Key1 != VirtualKey.NO_KEY)
                parts.Add(Describe(bind.Key1, bind.Modifier1));
            if (bind.Key2 != VirtualKey.NO_KEY)
                parts.Add(Describe(bind.Key2, bind.Modifier2));
            if (parts.Count == 0)
                continue;

            bound++;
            Plugin.ChatGui.Print($"{name}: {string.Join(" / ", parts)}");
        }

        Plugin.ChatGui.Print($"Chat 2 is listening for {bound} bound chat keybinds (of {manager.CurrentKeybinds.Count} tracked). Mode: {Plugin.Config.KeybindMode}, direct chat: {manager.DirectChat}.");
    }
}