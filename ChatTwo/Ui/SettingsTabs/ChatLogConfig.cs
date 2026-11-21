using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface.Style;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

namespace ChatTwo.Ui.SettingsTabs;

public sealed class ChatLogConfig : ISettingsTab
{
    private readonly Plugin Plugin;
    private Configuration Mutable { get; }

    public string Name => Language.Options_ChatLog_Tab + "###tabs-chatlog";

    public ChatLogConfig(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;
    }

    public void Draw(bool changed)
    {
        using (ImRaii.TextWrapPos(0.0f))
        {
            ImGuiUtil.OptionCheckbox(ref Mutable.KeepInputFocus, Language.Options_KeepInputFocus_Name, Language.Options_KeepInputFocus_Description);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.PlaySounds, Language.Options_PlaySounds_Name, Language.Options_PlaySounds_Description);
            ImGui.Spacing();

            // VierAIfied
            ImGuiUtil.OptionCheckbox(ref Mutable.VierAISpellCheck, "Check spelling in the chat input", "Misspelled words are marked. Hover over or right-click one for suggestions, Ignore or Add to dictionary.");
            ImGuiUtil.OptionCheckbox(ref Mutable.VierAITellOpensXim, "Send Tell opens XIM", "When XIM is installed and loaded, Send Tell opens that player's XIM conversation instead of typing /tell here.");
            ImGuiUtil.OptionCheckbox(ref Mutable.VierAIInputLimitBar, "Show the message length bar", "A thin bar under the chat input fills up as you type, counting UTF-8 bytes (channel prefix included) against the game's 500-byte limit for one chat line. It starts white, fades to yellow by 375 bytes (75%) and to red by 450 bytes (90%), and stays red past 500. From 400 bytes (80%) the count is shown at its right end. Hover over it for the exact count.");
            ImGuiUtil.OptionCheckbox(ref Mutable.VierAIInputWrap, "Wrap long messages in the input", "The chat input grows to up to 4 rows for a long line instead of scrolling sideways. Enter still sends (it never adds a line break); Up/Down recall earlier messages from the first/last row.");
            if (Mutable.VierAISpellCheck) VierAIfiedShared.SpellChecker.DrawSettings(); // VierAIfied: squiggle style, colour, ignored words (shared, saved straight away)
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.SidebarTabView, Language.Options_SidebarTabView_Name, string.Format(Language.Options_SidebarTabView_Description, Plugin.PluginName));
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.ShowNoviceNetwork, Language.Options_ShowNoviceNetwork_Name, Language.Options_ShowNoviceNetwork_Description);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.ShowHideButton, Language.Options_ShowHideButton_Name, Language.Options_ShowHideButton_Description);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.NativeItemTooltips, Language.Options_NativeItemTooltips_Name, string.Format(Language.Options_NativeItemTooltips_Description, Plugin.PluginName));
            ImGui.Spacing();

            if (Mutable.NativeItemTooltips)
            {
                ImGuiUtil.DragFloatVertical(Language.Options_TooltipOffset_Name, Language.Options_TooltipOffset_Desc, ref Mutable.TooltipOffset, 1, 0f, 400f, $"{Mutable.TooltipOffset:N0}px", ImGuiSliderFlags.AlwaysClamp);
                ImGui.Spacing();
            }

            SliderFloatVertical(Mutable.VierAIFade ? "Window opacity (focused)" : Language.Options_WindowOpacity_Name, null, ref Mutable.WindowAlpha, 0f, 100f, 100f, "%.0f%%", commit: v => CommitLive(c => c.WindowAlpha = v)); // VierAIfied: real slider
            ImGui.Spacing();

            // VierAIfied: fade the main window while it is not in use (AI generated code)
            if (ImGuiUtil.OptionCheckbox(ref Mutable.VierAIFade, "Fade the chat window when not in use", "While the mouse is away from the main chat window and you are not typing, the window fades to the unfocused opacities (one for the window itself, one for the message lines). Moving the mouse over it, typing or opening a menu brings it back to the focused look. Pop-out windows are not affected."))
                CommitLive(c => c.VierAIFade = Mutable.VierAIFade);
            if (Mutable.VierAIFade)
            {
                if (ActiveSlider == null) // not while a value is being dragged or typed
                    Mutable.SanitizeFade();
                using (ImRaii.PushIndent())
                {
                    SliderFloatVertical("Window opacity (unfocused)", "Opacity of the window itself (background, buttons, tabs and input) while it is not in use, as a percentage of the focused look. The background ends up at the focused opacity times this value. The message lines have their own setting below.", ref Mutable.VierAIFadeUnfocusedOpacity, 0f, 100f, 30f, "%.0f%%", commit: v => CommitLive(c => c.VierAIFadeUnfocusedOpacity = v));
                    ImGui.Spacing();

                    SliderFloatVertical("Line opacity (unfocused)", "Opacity of the message lines while the window is not in use. Set it higher than the window opacity to keep the lines more readable while the window itself fades further, or lower to fade the text more. New lines fade down to this level.", ref Mutable.VierAIFadeLineUnfocusedOpacity, 0f, 100f, 30f, "%.0f%%", commit: v => CommitLive(c => c.VierAIFadeLineUnfocusedOpacity = v));
                    ImGui.Spacing();

                    SliderFloatVertical("Fade speed per frame", "How much the opacity changes on each frame while the whole window fades in or out (new lines use the new message fade time instead). The rate is per frame, so it fades faster at higher frame rates: at 2%, going from 100% to 30% takes about 0.6 s at 60 FPS and about 0.25 s at 144 FPS. Lower is smoother and slower.", ref Mutable.VierAIFadeStep, 0.1f, 100f, 2f, "%.1f%% per frame", ImGuiSliderFlags.Logarithmic, v => CommitLive(c => c.VierAIFadeStep = v));
                    ImGui.Spacing();

                    SliderFloatVertical("New message fade time (total)", "While the window is faded, a new line stays at full opacity for the first half of this time, then fades smoothly down to the unfocused line opacity over the second half (the default 20 s is 10 s solid, then 10 s fading). Lines fade one at a time in the order they arrived; lines that scroll out of view first are not shown. 0 turns this off, so new lines follow the other lines.", ref Mutable.VierAIFadeNewLineSeconds, 0f, 120f, 20f, "%.1f s", commit: v => CommitLive(c => c.VierAIFadeNewLineSeconds = v));
                    ImGui.Spacing();

                    SliderFloatVertical("Bright line background max opacity", "How solid the soft background behind a new line is when it is at its brightest, as a percentage of the strongest it can be. Lower is more see-through; 0 turns the background off. It fades out with the line.", ref Mutable.VierAIFadeLineBgMax, 0f, 100f, 60f, "%.0f%%", commit: v => CommitLive(c => c.VierAIFadeLineBgMax = v));
                    ImGui.Spacing();
                }
            }

            if (ImGuiUtil.InputIntVertical(Language.Options_MaxLinesToShow_Name, Language.Options_MaxLinesToShow_Description, ref Mutable.MaxLinesToRender))
                Mutable.MaxLinesToRender = Math.Clamp(Mutable.MaxLinesToRender, 1, 10_000);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.CanMove, Language.Options_CanMove_Name);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.CanResize, Language.Options_CanResize_Name);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.ShowTitleBar, Language.Options_ShowTitleBar_Name);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.ShowPopOutTitleBar, Language.Options_ShowPopOutTitleBar_Name);
            ImGui.Spacing();

            ImGuiUtil.OptionCheckbox(ref Mutable.OverrideStyle, Language.Options_OverrideStyle_Name, Language.Options_OverrideStyle_Name_Desc);
            ImGui.Spacing();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted(Language.Options_ChatTabForwardKeybind_Name);
            ImGui.SetNextItemWidth(-1);
            ImGuiUtil.KeybindInput("ChatTabForwardKeybind", ref Mutable.ChatTabForward);

            ImGui.TextUnformatted(Language.Options_ChatTabBackwardKeybind_Name);
            ImGui.SetNextItemWidth(-1);
            ImGuiUtil.KeybindInput("ChatTabBackwardKeybind", ref Mutable.ChatTabBackward);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted(Language.Options_AdjustPosition_Name);
            ImGui.SetNextItemWidth(-1);
            var pos = Plugin.ChatLog.LastWindowPos;
            if (ImGui.DragFloat2($"##{Language.Options_AdjustPosition_Name}", ref pos, 1, 0, float.MaxValue, "%.0fpx"))
                Plugin.ChatLog.Position = pos;
            ImGuiUtil.WarningText(Language.Options_AdjustPosition_Warning);
            ImGui.Spacing();
        }

        if (!Mutable.OverrideStyle)
            return;

        var styles = StyleModel.GetConfiguredStyles();
        if (styles == null)
        {
            ImGui.TextUnformatted(Language.Options_OverrideStyle_NotAvailable);
            ImGui.Spacing();
            return;
        }

        var currentStyle = Mutable.ChosenStyle ?? Language.Options_OverrideStyle_NotSelected;
        using var combo = ImRaii.Combo(Language.Options_OverrideStyleDropdown_Name, currentStyle);
        if (combo)
        {
            foreach (var style in styles)
                if (ImGui.Selectable(style.Name, Mutable.ChosenStyle == style.Name))
                    Mutable.ChosenStyle = style.Name;
        }

        ImGui.Spacing();
    }

    // VierAIfied (AI generated code): a real slider (track and grab) under its label, full width, with optional help text.
    // Ctrl+click types a value; the result is kept finite and inside the range.
    // Label of the slider that was active (dragged or Ctrl+click typed) on the previous frame, if any.
    private static string? ActiveSlider;

    // VierAIfied 1.41.2.7: the window opacity and fade settings take effect and are saved as soon as an edit
    // ends (slider released, typed value entered, checkbox clicked), like the window itself, instead of
    // only when Save is pressed. Before, a change made here was lost if the settings window was closed
    // with its X (or Discard) or the game was reloaded without pressing Save.
    private void CommitLive(Action<Configuration> set)
    {
        set(Plugin.Config);
        Plugin.Config.SanitizeFade();
        Plugin.DeferredSaveFrames = 60;
    }

    private static bool SliderFloatVertical(string label, string? description, ref float value, float min, float max, float fallback, string format, ImGuiSliderFlags extraFlags = ImGuiSliderFlags.None, Action<float>? commit = null)
    {
        if (!float.IsFinite(value))
            value = fallback;
        // Not while this slider is being edited, so Ctrl+click typing (e.g. 0.15) is not overwritten mid-entry.
        if (ActiveSlider != label)
            value = Math.Clamp(value, min, max);

        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        var changed = ImGui.SliderFloat($"##{label}", ref value, min, max, format, ImGuiSliderFlags.AlwaysClamp | extraFlags);
        var committed = ImGui.IsItemDeactivatedAfterEdit();
        // Leave the value alone while the slider is being dragged or typed into (Ctrl+click), so typing is
        // not fought; AlwaysClamp clamps on commit, and the value is checked again on the next frame and by SanitizeFade.
        var active = ImGui.IsItemActive();
        if (active)
            ActiveSlider = label;
        else if (ActiveSlider == label)
            ActiveSlider = null;
        if (description != null)
            ImGuiUtil.HelpText(description);

        if (!active)
        {
            if (!float.IsFinite(value))
                value = fallback;
            value = Math.Clamp(value, min, max);
        }

        if (committed && commit != null)
        {
            if (!float.IsFinite(value))
                value = fallback;
            value = Math.Clamp(value, min, max);
            commit(value);
        }

        return changed;
    }
}
