# Modification notice

This repository is a modified version of Chat 2 (https://github.com/Infiziert90/ChatTwo), distributed under the EUPL-1.2.

It was modified by VierAI between 2026-10-18 and 2026-10-24. The changes are AI generated code and are not supported by the original authors. This notice is provided for Article 5 of the EUPL.

Files changed relative to upstream commit a63403c (the `upstream-pin` branch); the full history of every change is in this repository's git log:

- ChatTwo/ChatTwo.csproj (changed)
- ChatTwo/ChatTwo.yaml (removed)
- ChatTwo/ChatTwoVierAIfied.yaml (added)
- ChatTwo/GameFunctions/KeybindManager.cs (changed)
- ChatTwo/Configuration.cs (changed)
- ChatTwo/MessageManager.cs (changed)
- ChatTwo/packages.lock.json (changed)
- ChatTwo/Plugin.cs (changed)
- ChatTwo/Ui/Handler/AutoCompleteHandler.cs (changed)
- ChatTwo/Ui/Handler/InputHandler.cs (changed)
- ChatTwo/Ui/Handler/SendHandler.cs (changed)
- ChatTwo/Ui/SettingsTabs/ChatLogConfig.cs (changed)
- ChatTwo/Ui/SettingsTabs/Database.cs (changed)
- ChatTwo/Util/AutoTranslate.cs (changed)
- ChatTwo/VierAIfied/Dictionaries/ (added: en_US.dic, en_US.aff, en_US-LICENSE.txt, ffxiv-words.txt, THIRD-PARTY-NOTICES.txt)
- ChatTwo/VierAIfied/InputHistory.cs (added)
- ChatTwo/VierAIfied/InputQueue.cs (added)
- ChatTwo/VierAIfied/KeybindAudit.cs (added)
- ChatTwo/VierAIfied/SingleCompletion.cs (added)
- ChatTwo/VierAIfied/SpellCheck.cs (added)
- ChatTwo/VierAIfied/SpellCheckInput.cs (added)
- ChatTwo/VierAIfiedMigration.cs (added)
- README.md (changed)

## Shared settings files

- pluginConfigs/ChatTwoVierAIfied/vierai-config.json: shared VierAI settings (section "SpellCheck": ignored words, marking style Highlight/Wavy/Straight, colour, highlight fill, hover suggestions on/off), read and written by both VierAI-fied XIM and Chat 2.
- pluginConfigs/ChatTwoVierAIfied/vierai-user-dictionary.txt and vierai-input-history.json: shared spell-check dictionary and input history.

## Third-party components

- WeCantSpell.Hunspell 7.0.1 (https://github.com/aarondandy/WeCantSpell.Hunspell): unmodified library, tri-licensed MPL-1.1 / GPL-2.0-or-later / LGPL-2.1-or-later; used under LGPL-2.1-or-later.
- en_US Hunspell dictionary from SCOWL 2020.12.07 via https://github.com/wooorm/dictionaries: SCOWL/Ispell permissive (BSD-style) licence, full text in ChatTwo/VierAIfied/Dictionaries/en_US-LICENSE.txt.
- Notices are shipped in the plugin zip under VierAIfied/Dictionaries/.
- vierai/icon.png (added)
