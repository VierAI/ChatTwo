// VierAI-fied fork support (AI generated code).
// Typed characters bypass ImGui's one-edge-per-frame key queue so fast typing is not delayed;
// editing keys and shortcuts stay in the queue. Called once per frame from the plugin's tick.
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.VierAIfied;

internal static class InputQueue
{
    private static bool loggedInputEventSize;

    internal static void PassTextAheadOfKeyEdges(Action<string> warn)
    {
        var imguiContext = ImGui.GetCurrentContext();
        if (imguiContext.IsNull)
            return;

        // Leave this on. One change per key per frame is what makes Backspace,
        // Enter, arrows, and shortcuts survive a hitch. Typed glyphs do not use it.
        ImGui.GetIO().ConfigInputTrickleEventQueue = true;
        if (imguiContext.WantTextInputNextFrame != 1)
            return;

        if (Unsafe.SizeOf<ImGuiInputEvent>() != 24 || Unsafe.SizeOf<ImGuiInputEventKey>() != 12)
        {
            if (!loggedInputEventSize)
            {
                loggedInputEventSize = true;
                warn("ImGui input event layout changed; typed characters stay on the slow path.");
            }

            return;
        }

        ref var queue = ref imguiContext.InputEventsQueue;
        if (queue.Size == 0)
            return;

        // A plain letter's down and up would sit in front of its character and hold
        // every following glyph. The text box inserts from the character, so those
        // edges can go. Alt chords stay: Chat 2 reads them as channel hotkeys.
        // Ctrl+RightAlt is AltGr and still drops, so that glyph is not delayed.
        var ctrl = ImGui.IsKeyDown(ImGuiKey.LeftCtrl) || ImGui.IsKeyDown(ImGuiKey.RightCtrl);
        var leftAlt = ImGui.IsKeyDown(ImGuiKey.LeftAlt);
        var rightAlt = ImGui.IsKeyDown(ImGuiKey.RightAlt);
        var super = ImGui.IsKeyDown(ImGuiKey.LeftSuper) || ImGui.IsKeyDown(ImGuiKey.RightSuper);
        var drop = queue.Size <= 256 ? stackalloc int[queue.Size] : new int[queue.Size];
        var dropCount = 0;

        for (var i = 0; i < queue.Size; i++)
        {
            var evt = queue[i];
            if (evt.Type != ImGuiInputEventType.Key)
                continue;

            var key = evt.Union.Key.Key;
            var down = evt.Union.Key.Down != 0;
            var altGr = ctrl && rightAlt && !leftAlt;
            var keepForChord = !altGr && (leftAlt || rightAlt || ctrl || super);
            if (IsCharacterKey(key) && !keepForChord && !ImGui.IsKeyDown(key))
                drop[dropCount++] = i;

            switch (key)
            {
                case ImGuiKey.LeftCtrl:
                case ImGuiKey.RightCtrl:
                    ctrl = down;
                    break;
                case ImGuiKey.LeftAlt:
                    leftAlt = down;
                    break;
                case ImGuiKey.RightAlt:
                    rightAlt = down;
                    break;
                case ImGuiKey.LeftSuper:
                case ImGuiKey.RightSuper:
                    super = down;
                    break;
            }
        }

        for (var n = dropCount - 1; n >= 0; n--)
            RemoveInputEvent(ref queue, drop[n]);
    }

    private static bool IsCharacterKey(ImGuiKey key)
    {
        var value = (int)key;
        if (value == (int)ImGuiKey.Space)
            return true;
        if (value >= (int)ImGuiKey.Key0 && value <= (int)ImGuiKey.Z)
            return true;
        if (value >= (int)ImGuiKey.Apostrophe && value <= (int)ImGuiKey.GraveAccent)
            return true;
        if (value >= (int)ImGuiKey.Keypad0 && value <= (int)ImGuiKey.KeypadAdd)
            return true;
        return value == (int)ImGuiKey.KeypadEqual;
    }

    private static void RemoveInputEvent(ref ImVector<ImGuiInputEvent> queue, int index)
    {
        var last = queue.Size - 1;
        for (var i = index; i < last; i++)
            queue[i] = queue[i + 1];
        queue.Resize(last);
    }
}
