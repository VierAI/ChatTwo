# Chat 2 (VierAI-fied)

> **Disclaimer:** this is an unofficial fork of Chat 2. Its changes are AI-generated ("vibe coded"). It is not made, endorsed or supported by the Chat 2 team, so please don't ask them for help with it. Use it at your own risk.

## Official Chat 2

Links from the original README:

For more infos read [IPC Guide](ipc.md)

Original source: [Infiziert90/ChatTwo](https://github.com/Infiziert90/ChatTwo)

## What this fork changes

Full list: [CHANGELOG.md](CHANGELOG.md)

- **Spell checker** in the chat input with hover suggestions, shared dictionary and settings with the VierAI-fied XIM.
- **Input:** Up/Down message history, single-match Tab completion, Shout keybind, /chat2keys.
- **Fixes:** emote Tab completion, more responsive typing (no delayed or dropped keys), no lost queued messages, no database clash with the official Chat 2.
- **Performance:** keybind lookups spread across frames, message thread no longer polls every millisecond.
- Installs alongside as its own plugin and imports the official settings; the installer's main window button shows the chat.

## Installing this fork

Add this custom plugin repository in Dalamud (`/xlsettings` > Experimental > Custom Plugin Repositories), then install it from `/xlplugins`:

`https://raw.githubusercontent.com/VierAI/DalamudPlugins/main/vierai.json`

## For the Chat 2 team

If the original Chat 2 authors would like any of these changes sent back upstream as pull requests, please open an issue on this repository or contact the fork maintainer, and we'll gladly prepare them.

## Licence and credits

This fork keeps the original licence, EUPL-1.2 (see [LICENCE](LICENCE)), and all original credits. See [MODIFICATIONS.md](MODIFICATIONS.md) for the list of changed files.

The original README follows unchanged.

---

## Original README

# Chat 2
#### Electric Boogaloo - ♪ A whole new chat, a new fantastic chat window ♪

Chat 2 is a complete rewrite of the in-game chat window as a plugin.
It supports:

- Unlimited tabs
- Tabs that always send to a certain channel
- More flexible filtering
- RGB channel colouring
- Completely variable font size
- Sidebar tabs
- Unread counts
- Emotes
- Screenshot mode (obfuscate names)

---

### Chat Window
![chatWindow.png](ChatTwo/images/chatWindow.png)

### With SimpleTweaks "Chat Name Colors"
![withSimpleTweaks.png](ChatTwo/images/withSimpleTweaks.png)

---

### IPC Integration
Other plugins can easily integrate their functionality into the context menu of chat2
For more infos read [IPC Guide](ipc.md)

---

### Thanks to
The original dev Anna~
