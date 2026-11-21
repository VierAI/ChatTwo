# Modification notice

This repository is a modified version of Chat 2 (https://github.com/Infiziert90/ChatTwo), distributed under the EUPL-1.2.

It was modified by VierAI between 2026-10-18 and 2026-10-28. The changes are AI generated code and are not supported by the original authors. This notice is provided for Article 5 of the EUPL.

Files changed relative to upstream commit a63403c (the `upstream-pin` branch); the full history of every change is in this repository's git log:

- ChatTwo/ChatTwo.csproj (changed)
- ChatTwo/ChatTwo.yaml (removed)
- ChatTwo/ChatTwoVierAIfied.yaml (added)
- ChatTwo/GameFunctions/KeybindManager.cs (changed)
- ChatTwo/Configuration.cs (changed; 1.41.2.4: window fade settings, NaN or Infinity in them falls back to the defaults; 1.41.2.6: new message fade time defaults to 20 s, Window opacity is also checked; 1.41.2.9: bright line background max opacity setting; 1.41.2.10: separate unfocused line opacity, older configs copy the window value)
- ChatTwo/MessageManager.cs (changed)
- ChatTwo/packages.lock.json (changed)
- ChatTwo/Plugin.cs (changed; 1.40.9.19: the hide-vanilla-chat addon check is cached for 250 ms once all chat addons are hidden, re-checked at once on zone change or login; 1.40.9.20: registers the Ctrl+C key guard, AI generated code)
- ChatTwo/Ui/SettingsTabs/Display.cs (changed: 1.40.9.17 spam digest options)
- ChatTwo/Ui/ChatLog/ChatLog.Window.cs (changed; 1.40.9.17: off-screen rows with a known height are replaced by one spacer so long history no longer drops FPS, and runs of system/notice/glamour lines fold into one clickable line, AI generated code; 1.41.2.4: hooks for the window fade; 1.41.2.5: background alpha follows the fade, scroll anchor hooks; 1.41.2.6: scroll anchor applied in finally; 1.41.2.7: passes the table mode to the line fade; 1.41.2.8: the timestamp, fold line and [-] marker report their boxes for the line background; 1.41.2.10: the bright line backgrounds are drawn in one pass behind the log child window)
- ChatTwo/Ui/ChatLog/ChatLog.Fade.cs (added; 1.41.2.4: optional window fade while not in use, new lines stay bright then fade one at a time, AI generated code; 1.41.2.5: the window background fades too; 1.41.2.6: new lines fade by age, solid for half the fade time then a smoothstep over the other half; 1.41.2.7: bright new lines get their own row background; 1.41.2.8: that background follows the text outline per line; 1.41.2.9: soft feathered edge, a pixel lower, peak opacity setting; 1.41.2.10: all bright line backgrounds of a frame drawn in one pass behind the log so neighbours meet halfway instead of overlapping, centred on the glyph extents, separate window and line unfocused levels)
- ChatTwo/Ui/ChatLog/ChatLog.ScrollAnchor.cs (added; 1.41.2.5: a scrolled-up log stays on its lines when old lines are dropped from the top, AI generated code)
- ChatTwo/Ui/ChatLog/ChatLog.Selection.cs (added; 1.40.9.18: click-and-drag line selection in the message log, Ctrl+C and right-click Copy selection; 1.40.9.20: character-level selection, Copy link for the web address under the mouse, Ctrl+C kept from the game, AI generated code)
- ChatTwo/VierAIfied/TextSelection.cs (added; 1.40.9.20: character-level selection bookkeeping, hit test and web address lookup; replaces DragSelection.cs from 1.40.9.18, AI generated code)
- ChatTwo/VierAIfied/CopyKeyGuard.cs (added; 1.40.9.20: keeps Ctrl+C on a selection from reaching the game, AI generated code)
- ChatTwo/Util/ImGuiUtil.cs (changed; 1.40.9.18: with drag selection on, a left click on a link activates on release unless the press became a drag; 1.40.9.20: records each drawn piece of message text for character-level selection; 1.41.2.8: reports drawn item boxes for the fade line background, not from hover tooltips)
- ChatTwo/Ui/Handler/ChunkHandler.cs (changed; 1.40.9.20: tracks the character offset of each drawn chunk for selection; 1.41.2.8: icons, emotes and unwrapped text report their boxes for the fade line background)
- ChatTwo/Ui/Handler/PayloadHandler.cs (changed; 1.40.9.18: Copy selection entry at the top of the existing right-click menu while lines are selected; 1.40.9.20: Copy link entry for the web address under the mouse; 1.40.9.21: the context menu opens on right-click only, left-click no longer falls back to it)
- ChatTwo/Ui/Handler/AutoCompleteHandler.cs (changed)
- ChatTwo/Ui/Handler/InputHandler.cs (changed)
- ChatTwo/VierAIfied/AtomicPhrases.cs (added: auto-translate phrases in the input act as one block)
- ChatTwo/Ui/Handler/SendHandler.cs (changed)
- ChatTwo/Ui/SettingsTabs/ChatLogConfig.cs (changed; 1.41.2.4: window fade settings under Window opacity; 1.41.2.5: Window opacity and the fade settings are sliders; 1.41.2.6: new message fade time label and help, 20 s default, no clamp while a slider is being edited; 1.41.2.7: window opacity and fade settings apply and save when an edit ends; 1.41.2.9: bright line background max opacity slider; 1.41.2.10: Line opacity (unfocused) slider)
- ChatTwo/Ui/SettingsTabs/Database.cs (changed)
- ChatTwo/Util/AutoTranslate.cs (changed)
- ChatTwo/VierAIfied/Dictionaries/ (added: en_US.dic, en_US.aff, en_US-LICENSE.txt, ffxiv-words.txt, THIRD-PARTY-NOTICES.txt)
- ChatTwo/VierAIfied/InputHistory.cs (added; 1.40.9.16: Up/Down input history is kept per tab, in memory only, and cleared when a popped-out tab closes, AI generated code)
- ChatTwo/Ui/Popout.cs (changed: 1.40.9.16 clears the tab's input history on close)
- ChatTwo/VierAIfied/InputQueue.cs (added)
- ChatTwo/VierAIfied/KeybindAudit.cs (added)
- ChatTwo/VierAIfied/ScrollToBottomButton.cs (added)
- ChatTwo/RichChatInput/ (added; 1.40.9.27: RichInput.cs, RichState.cs, RichWords.cs, LICENSE and VERSION, an unmodified pinned copy of the RichChatInput repository, a separate MIT-licensed work written from scratch for VierAI and not derived from this program; see "Third-party components added by VierAI"), ChatTwo/VierAIfied/InputEditor.cs (added; history and draft glue), ChatTwo/Ui/Handler/InputHandler.cs (changed; the chat input is drawn by RichChatInput: wrap, triple-click, Shift+click, Ctrl+Delete, undo/redo, Cut/Copy/Paste/Select All menu with Hide chat, drag and drop, word drag, drafts), ChatTwo/VierAIfied/InputLimitBar.cs, ChatTwo/Ui/ChatLog/ChatLog.Window.cs and ChatTwo/Ui/Popout.cs (changed; the bar is drawn by RichChatInput with its 80% byte count and the log reserves the wrapped input height), ChatTwo/Configuration.cs and ChatTwo/Ui/SettingsTabs/ChatLogConfig.cs (changed; setting VierAIInputWrap), ChatTwo/Plugin.cs (changed; drafts saved on unload) and ChatTwo/ChatTwo.csproj (changed; ships RichChatInput-LICENSE.txt), AI generated code
- ChatTwo/VierAIfied/WordSpans.cs, ChatTwo/VierAIfied/InputWordSelect.cs (added in 1.40.9.26) and ChatTwo/VierAIfied/InputHistory.cs (added in 1.40.9.16) removed in 1.40.9.27: word drag and Up/Down history now run in RichChatInput over the same per-tab history list
- ChatTwo/VierAIfied/InputJumpToLatest.cs (added, written for this fork), ChatTwo/Ui/Handler/InputHandler.cs and ChatTwo/Ui/ChatLog/ChatLog.Window.cs (changed; 1.40.9.25: focusing, typing or editing in the chat input, or sending with Enter, scrolls the message log drawn with that input's payload handler to the bottom for 3 frames; a scrolled-up log is not moved otherwise, AI generated code)
- ChatTwo/VierAIfied/SingleCompletion.cs (added)
- ChatTwo/VierAIfied/AutoTranslateDisplay.cs (added)
- ChatTwo/Ui/InputPreview.cs (changed)
- ChatTwo/VierAIfied/SpellCheck.cs (added)
- ChatTwo/VierAIfied/SpellCheckInput.cs (added; 1.40.9.15: suggestion popup flips above and is clamped to the screen, AI generated code)
- ChatTwo/VierAIfiedMigration.cs (added)
- README.md (changed)

## Shared settings files

- pluginConfigs/ChatTwoVierAIfied/vierai-config.json: shared VierAI settings (section "SpellCheck": ignored words, marking style Highlight/Wavy/Straight, colour, highlight fill, hover suggestions on/off), read and written by both VierAI-fied XIM and Chat 2.
- pluginConfigs/ChatTwoVierAIfied/vierai-user-dictionary.txt and vierai-input-history.json: shared spell-check dictionary. vierai-input-history.json is no longer read or written (1.40.9.16).

## Message length bar

ChatTwo/VierAIfied/InputLimitBar.cs (new, written for this fork): draws a 3 px ImGui progress bar under the input row of the main window (ChatLog.Window.cs) and of popped-out tabs (Popout.cs). The count mirrors SendHandler.SendChatBox: a line starting with / is counted as typed, a direct tell as the bare message, a tell without a content id with "/tell Name@World " in front, and anything else with the channel prefix and a space in front. The fill is always its own colour, never the theme or channel colour: plain white up to 250 bytes (50%), blending to yellow at 375 (75%), then to red at 450 (90%), red from there on (1.40.9.24; 1.40.9.23 started the blend at 0 bytes). GetRemainingHeightForMessageLog reserves the bar's height so the message log is not pushed down. Setting VierAIInputLimitBar (Settings > Chat Log, on by default). Sending, the input buffer and text selection are unchanged.

## Third-party components

- WeCantSpell.Hunspell 7.0.1 (https://github.com/aarondandy/WeCantSpell.Hunspell): unmodified library, tri-licensed MPL-1.1 / GPL-2.0-or-later / LGPL-2.1-or-later; used under LGPL-2.1-or-later.
- en_US Hunspell dictionary from SCOWL 2020.12.07 via https://github.com/wooorm/dictionaries: SCOWL/Ispell permissive (BSD-style) licence, full text in ChatTwo/VierAIfied/Dictionaries/en_US-LICENSE.txt.
- Notices are shipped in the plugin zip under VierAIfied/Dictionaries/.

## Send Tell opens XIM

ChatTwo/VierAIfied/XimTell.cs: when the setting VierAITellOpensXim (Settings > Chat Log, on by default) is on and XIM (InternalName MessengerVierAIfied or Messenger) is loaded, the Send Tell context-menu action calls XIM's IPC Messenger.OpenMessenger("Name@World", setFocus: true). If that throws it runs /xim Name@World; if that fails or XIM is not loaded it uses the original /tell behaviour. Not used in Eureka, Bozja or Occult Crescent, where tells keep their special handling.

## Third-party components added by VierAI
WeCantSpell.Hunspell 7.0.1 (MPL-1.1/GPL-2.0/LGPL-2.1, used under LGPL-2.1-or-later as an unmodified library) and the SCOWL en_US Hunspell dictionary (BSD-style, Kevin Atkinson and others). SixLabors.ImageSharp is an upstream dependency under the Six Labors Split License (Apache-2.0 for open-source use). Full notices: ChatTwo/VierAIfied/Dictionaries/THIRD-PARTY-NOTICES.txt and en_US-LICENSE.txt (both shipped in the release zip).

RichChatInput 1.0.0 (MIT License, Copyright (c) 2026 VierAI) is compiled in as source from ChatTwo/RichChatInput, an unmodified pinned copy of the RichChatInput repository; the copy in this repository is the complete source used for the build, and its licence ships as RichChatInput-LICENSE.txt. AI generated code.
