# Chat 2 (VierAI-fied) changelog

Cumulative list of changes from upstream Chat 2. Updated as changes land.

## Features

- Spell checker in the chat input: misspelled words are highlighted, hover or right-click for suggestions, Add to dictionary or Ignore; skips commands, links, auto-translate, numbers, ALL CAPS and nearby player names; includes an FFXIV word list; toggle in Settings > Chat Log
- Spell check settings: Highlight, Wavy or Straight style, colour, highlight fill slider, hover suggestions on/off, Clear ignored words; ignored words and the user dictionary are shared between the VierAI-fied XIM and Chat 2 (vierai-config.json)
- Up/Down recalls earlier messages and commands; one history for all input boxes, saved between restarts and shared with the VierAI-fied XIM; Down past the newest entry restores your draft
- Tab completion fills in the command straight away when there is only one match
- Handles the Shout (always) game keybind; /chat2keys lists the chat keybinds read from the game
- The plugin installer's main window button now shows the chat window if it is hidden
- Installs as its own plugin (ChatTwoVierAIfied), imports the official Chat 2 settings on first start and shares the official message database

## Fixes

- Tab completion of emotes and text commands inserted a code like <at:62,504> instead of the command
- More responsive typing: typed characters no longer wait in the one-key-per-frame queue, so fast typing and frame hitches no longer delay or drop letters; editing keys and shortcuts (Backspace, Enter, arrows, chords) stay queued so a tap still registers
- A message arriving at the same moment the message queue emptied could be lost; it is now always picked up
- Uses its own message database for the session when the official Chat 2 is loaded too (no duplicate messages or database locks)
- The input history shared with XIM is saved safely when both plugins write at once

## Performance

- Keybind lookups are spread across frames (a few per tick, refreshed every 5 s) instead of one framework spike
- The message processing thread sleeps until a message arrives instead of waking every millisecond
- The key list used to release keys while typing is built once instead of on every keypress

## Other

- Own plugin icon and AI-generated code disclosure
