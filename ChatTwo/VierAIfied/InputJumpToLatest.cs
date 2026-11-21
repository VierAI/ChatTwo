// VierAI-fied fork support (AI generated code).
// Typing in the chat input shows the newest message: when the input is focused, edited (typing, paste,
// delete), gets a text or editing key (held keys repeat), or sends with Enter, the message log that
// belongs to that input is scrolled to the bottom for the next few frames. A log scrolled up for reading
// is not moved while nobody types. The log keeps its own stick-to-bottom rule otherwise.
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.VierAIfied;

internal static class InputJumpToLatest
{
    private const int Frames = 3;

    private sealed class Until
    {
        public int Frame = -1;
    }

    // One entry per input (keyed by its payload handler, which is what the log is drawn with).
    private static readonly ConditionalWeakTable<object, Until> Pending = new();

    // Not counted: Page Up/Down, Esc, Ctrl/Shift/Alt/Super/Menu alone, function keys, lock keys,
    // Print Screen, Pause, mouse and gamepad keys.
    private static readonly ImGuiKey[] TextKeys = BuildTextKeys();

    private static ImGuiKey[] BuildTextKeys()
    {
        var keys = new System.Collections.Generic.List<ImGuiKey>();
        void Range(ImGuiKey from, ImGuiKey to)
        {
            for (var k = from; k <= to; k++)
                keys.Add(k);
        }

        Range(ImGuiKey.Tab, ImGuiKey.DownArrow);   // Tab, arrows
        Range(ImGuiKey.Home, ImGuiKey.Enter);      // Home, End, Insert, Delete, Backspace, Space, Enter
        Range(ImGuiKey.Key0, ImGuiKey.Z);          // digits and letters
        Range(ImGuiKey.Apostrophe, ImGuiKey.GraveAccent);
        Range(ImGuiKey.Keypad0, ImGuiKey.KeypadEqual);
        return keys.ToArray();
    }

    /// <summary>Call right after the input widget, with the payload handler its log is drawn with.</summary>
    public static void AfterInput(object owner, bool enabled, bool active)
    {
        if (!enabled)
            return;

        if (ImGui.IsItemActivated() || ImGui.IsItemEdited() || (active && TextKeyPressed()))
            Request(owner);
    }

    /// <summary>Show the newest message in this input's log for the next few frames (also used on send).</summary>
    public static void Request(object owner)
        => Pending.GetOrCreateValue(owner).Frame = ImGui.GetFrameCount() + Frames;

    /// <summary>True while a jump requested by this input is still running.</summary>
    public static bool IsActive(object owner)
        => Pending.TryGetValue(owner, out var until) && ImGui.GetFrameCount() <= until.Frame;

    private static bool TextKeyPressed()
    {
        foreach (var key in TextKeys)
            if (ImGui.IsKeyPressed(key, true))
                return true;

        return false;
    }
}
