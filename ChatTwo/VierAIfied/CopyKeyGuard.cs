// AI generated code.
using System;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace VierAIfiedShared;

/// <summary>Keeps Ctrl+C from reaching the game (where it opens the currency window) while a chat selection can be copied.
/// How: while a selection exists, the chat window is hovered or focused, no text box is active and Ctrl is held, the draw
/// code calls Arm() each frame. Arm() sets io.WantTextInput (plus WantCaptureKeyboard); Dalamud's Win32 input handler then
/// feeds key messages to ImGui only and returns before the game's window procedure sees them, so the C key-down never
/// reaches the game but ImGui still reports Ctrl+C to us. Backup for a C pressed in the same frame as Ctrl: OnUpdate clears
/// the game's C key state. Because the Ctrl key-up may also be swallowed, OnUpdate releases Ctrl/C in the game's key state
/// once they are physically up, so Ctrl never sticks.</summary>
internal static class CopyKeyGuard
{
    private const int VK_CONTROL = 0x11, VK_C = 0x43;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

    private static IKeyState? keys;
    private static long armedFrame = -10, lastArmMs, copyableMs;

    public static void Init(IKeyState k) => keys = k;

    /// <summary>Pure gate, unit tested: arm only for a live selection, window hovered or focused, no text box active, Ctrl down.</summary>
    public static bool ShouldArm(bool hasSel, bool hovered, bool focused, bool textInputActive, bool ctrl)
        => hasSel && (hovered || focused) && !textInputActive && ctrl;

    /// <summary>Call from the draw code every frame a selection is copyable (Ctrl not required) so OnUpdate can catch a fast Ctrl+C.</summary>
    public static void MarkCopyable() => copyableMs = Environment.TickCount64;

    public static void Arm()
    {
        var io = ImGui.GetIO();
        io.WantTextInput = true;
        io.WantCaptureKeyboard = true;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        armedFrame = ImGui.GetFrameCount();
        lastArmMs = Environment.TickCount64;
    }

    public static bool Armed => ImGui.GetFrameCount() - armedFrame <= 1;

    /// <summary>Framework.Update handler.</summary>
    public static void OnUpdate(IFramework _)
    {
        if (keys == null) return;
        var now = Environment.TickCount64;
        try
        {
            // fast press: C arrived before the first armed frame; drop it from the game's key state
            if (now - copyableMs < 200 && keys[VirtualKey.CONTROL] && keys[VirtualKey.C]) keys[VirtualKey.C] = false;
            // after arming, release keys whose key-up the game may not have seen
            if (now - lastArmMs < 3000)
            {
                if (keys[VirtualKey.CONTROL] && (GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0) keys[VirtualKey.CONTROL] = false;
                if (keys[VirtualKey.C] && (GetAsyncKeyState(VK_C) & 0x8000) == 0) keys[VirtualKey.C] = false;
            }
        }
        catch { /* key state is best effort */ }
    }
}
