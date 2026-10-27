# Modification notice

This repository is a modified version of Chat 2 (https://github.com/Infiziert90/ChatTwo), distributed under the EUPL-1.2.

It was modified by VierAI between 2026-10-18 and 2026-10-26. The changes are AI generated code and are not supported by the original authors. This notice is provided for Article 5 of the EUPL.

Files changed relative to upstream commit a63403c (the `upstream-pin` branch); the full history of every change is in this repository's git log:

- ChatTwo/ChatTwo.csproj (changed)
- ChatTwo/ChatTwo.yaml (removed)
- ChatTwo/ChatTwoVierAIfied.yaml (added)
- ChatTwo/GameFunctions/KeybindManager.cs (changed)
- ChatTwo/Configuration.cs (changed)
- ChatTwo/MessageManager.cs (changed)
- ChatTwo/packages.lock.json (changed)
- ChatTwo/Plugin.cs (changed; 1.40.9.19: the hide-vanilla-chat addon check is cached for 250 ms once all chat addons are hidden, re-checked at once on zone change or login; 1.40.9.20: registers the Ctrl+C key guard, AI generated code)
- ChatTwo/Ui/SettingsTabs/Display.cs (changed: 1.40.9.17 spam digest options)
- ChatTwo/Ui/ChatLog/ChatLog.Window.cs (changed; 1.40.9.17: off-screen rows with a known height are replaced by one spacer so long history no longer drops FPS, and runs of system/notice/glamour lines fold into one clickable line, AI generated code)
- ChatTwo/Ui/ChatLog/ChatLog.Selection.cs (added; 1.40.9.18: click-and-drag line selection in the message log, Ctrl+C and right-click Copy selection; 1.40.9.20: character-level selection, Copy link for the web address under the mouse, Ctrl+C kept from the game, AI generated code)
- ChatTwo/VierAIfied/TextSelection.cs (added; 1.40.9.20: character-level selection bookkeeping, hit test and web address lookup; replaces DragSelection.cs from 1.40.9.18, AI generated code)
- ChatTwo/VierAIfied/CopyKeyGuard.cs (added; 1.40.9.20: keeps Ctrl+C on a selection from reaching the game, AI generated code)
- ChatTwo/Util/ImGuiUtil.cs (changed; 1.40.9.18: with drag selection on, a left click on a link activates on release unless the press became a drag; 1.40.9.20: records each drawn piece of message text for character-level selection)
- ChatTwo/Ui/Handler/ChunkHandler.cs (changed; 1.40.9.20: tracks the character offset of each drawn chunk for selection)
- ChatTwo/Ui/Handler/PayloadHandler.cs (changed; 1.40.9.18: Copy selection entry at the top of the existing right-click menu while lines are selected; 1.40.9.20: Copy link entry for the web address under the mouse; 1.40.9.21: the context menu opens on right-click only, left-click no longer falls back to it)
- ChatTwo/Ui/Handler/AutoCompleteHandler.cs (changed)
- ChatTwo/Ui/Handler/InputHandler.cs (changed)
- ChatTwo/VierAIfied/AtomicPhrases.cs (added: auto-translate phrases in the input act as one block)
- ChatTwo/Ui/Handler/SendHandler.cs (changed)
- ChatTwo/Ui/SettingsTabs/ChatLogConfig.cs (changed)
- ChatTwo/Ui/SettingsTabs/Database.cs (changed)
- ChatTwo/Util/AutoTranslate.cs (changed)
- ChatTwo/VierAIfied/Dictionaries/ (added: en_US.dic, en_US.aff, en_US-LICENSE.txt, ffxiv-words.txt, THIRD-PARTY-NOTICES.txt)
- ChatTwo/VierAIfied/InputHistory.cs (added; 1.40.9.16: Up/Down input history is kept per tab, in memory only, and cleared when a popped-out tab closes, AI generated code)
- ChatTwo/Ui/Popout.cs (changed: 1.40.9.16 clears the tab's input history on close)
- ChatTwo/VierAIfied/InputQueue.cs (added)
- ChatTwo/VierAIfied/KeybindAudit.cs (added)
- ChatTwo/VierAIfied/ScrollToBottomButton.cs (added)
- ChatTwo/VierAIfied/SingleCompletion.cs (added)
- ChatTwo/VierAIfied/AutoTranslateDisplay.cs (added)
- ChatTwo/Ui/InputPreview.cs (changed)
- ChatTwo/VierAIfied/SpellCheck.cs (added)
- ChatTwo/VierAIfied/SpellCheckInput.cs (added; 1.40.9.15: suggestion popup flips above and is clamped to the screen, AI generated code)
- ChatTwo/VierAIfiedMigration.cs (added)
- README.md (changed)
- TODO.md (added)

## Shared settings files

- pluginConfigs/ChatTwoVierAIfied/vierai-config.json: shared VierAI settings (section "SpellCheck": ignored words, marking style Highlight/Wavy/Straight, colour, highlight fill, hover suggestions on/off), read and written by both VierAI-fied XIM and Chat 2.
- pluginConfigs/ChatTwoVierAIfied/vierai-user-dictionary.txt and vierai-input-history.json: shared spell-check dictionary. vierai-input-history.json is no longer read or written (1.40.9.16).

## Third-party components

- WeCantSpell.Hunspell 7.0.1 (https://github.com/aarondandy/WeCantSpell.Hunspell): unmodified library, tri-licensed MPL-1.1 / GPL-2.0-or-later / LGPL-2.1-or-later; used under LGPL-2.1-or-later.
- en_US Hunspell dictionary from SCOWL 2020.12.07 via https://github.com/wooorm/dictionaries: SCOWL/Ispell permissive (BSD-style) licence, full text in ChatTwo/VierAIfied/Dictionaries/en_US-LICENSE.txt.
- Notices are shipped in the plugin zip under VierAIfied/Dictionaries/.

## Send Tell opens XIM

ChatTwo/VierAIfied/XimTell.cs: when the setting VierAITellOpensXim (Settings > Chat Log, on by default) is on and XIM (InternalName MessengerVierAIfied or Messenger) is loaded, the Send Tell context-menu action calls XIM's IPC Messenger.OpenMessenger("Name@World", setFocus: true). If that throws it runs /xim Name@World; if that fails or XIM is not loaded it uses the original /tell behaviour. Not used in Eureka, Bozja or Occult Crescent, where tells keep their special handling.

## Third-party components added by VierAI
WeCantSpell.Hunspell 7.0.1 (MPL-1.1/GPL-2.0/LGPL-2.1, used under LGPL-2.1-or-later as an unmodified library) and the SCOWL en_US Hunspell dictionary (BSD-style, Kevin Atkinson and others). SixLabors.ImageSharp is an upstream dependency under the Six Labors Split License (Apache-2.0 for open-source use). Full notices: ChatTwo/VierAIfied/Dictionaries/THIRD-PARTY-NOTICES.txt and en_US-LICENSE.txt (both shipped in the release zip).
