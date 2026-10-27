# Chat 2 (VierAI-fied) changelog

Cumulative list of changes from upstream Chat 2. Updated as changes land.

## Features

- Right-click only menu (1.40.9.21): left-click on a message no longer opens the context menu; it only selects text. Links, map links, quests, achievements, party finder and plugin links still act on a plain left-click. Items, player names and plain text open their menu on right-click only, with the same entries as before, Copy link and Copy selection included (AI generated code)
- Character-level selection and Copy link (1.40.9.20): a drag now selects from the exact character under the mouse, within one line or across lines and messages, so part of a line (one web address out of several) can be copied. Right-click on a web address shows Copy link for that address only, even with several on one line. Ctrl+C on a selection no longer reaches the game (it opened the currency window). Links, item links and party finder links still open on a plain click (AI generated code)
- Drag to select and copy (1.40.9.18): press on or near message text and drag to select one or more lines (wrapped lines included), then Ctrl+C or right-click > Copy selection to copy them as plain text; Escape or a click clears it. A plain click still opens links, item links and party finder links; dragging empty space still moves the window (when moving is allowed). Toggle in Settings > Display (on by default)

- Send Tell opens XIM: when XIM is installed and loaded, Send Tell on a player name opens that player's XIM conversation with its input focused instead of typing /tell in Chat 2; falls back to /tell otherwise; toggle in Settings > Chat Log (on by default)
- Scroll-to-bottom button: when you scroll up in the message log, a small down-arrow button appears in its bottom-right corner; click it to jump back to the newest message
- Spell checker in the chat input: misspelled words are highlighted, hover or right-click for suggestions, Add to dictionary or Ignore; skips commands, links, auto-translate, numbers, ALL CAPS and nearby player names; includes an FFXIV word list; toggle in Settings > Chat Log
- Spell check settings: Highlight, Wavy or Straight style, colour, highlight fill slider, hover suggestions on/off, Clear ignored words; ignored words and the user dictionary are shared between the VierAI-fied XIM and Chat 2 (vierai-config.json)
- Up/Down recalls earlier messages and commands; one history for all input boxes, saved between restarts and shared with the VierAI-fied XIM; Down past the newest entry restores your draft
- Tab completion fills in the command straight away when there is only one match that starts with what you typed (fixed in 1.40.9.12: it used to flicker and revert); otherwise the auto-translate search window opens as before
- Auto-translate phrases show in the input as the words between the game's auto-translate arrows, like the vanilla chat box, instead of a raw <at:..> code; they are still sent as real auto-translate
- Auto-translate phrases in the input act as one block: the cursor cannot land inside one, Backspace or Delete on any part (including either arrow) removes the whole phrase, and a selection that touches one covers all of it
- Handles the Shout (always) game keybind; /chat2keys lists the chat keybinds read from the game
- The plugin installer's main window button now shows the chat window if it is hidden
- Installs as its own plugin (ChatTwoVierAIfied), imports the official Chat 2 settings on first start and shares the official message database

## Fixes

- Spell check suggestion popup stays on screen: when the input is near the bottom of the screen it opens above the word instead of below, and it is always kept inside the game window, left and right too (AI generated code)

- Tab completion of emotes and text commands inserted a code like <at:62,504> instead of the command
- More responsive typing: typed characters no longer wait in the one-key-per-frame queue, so fast typing and frame hitches no longer delay or drop letters; editing keys and shortcuts (Backspace, Enter, arrows, chords) stay queued so a tap still registers
- A message arriving at the same moment the message queue emptied could be lost; it is now always picked up
- Uses its own message database for the session when the official Chat 2 is loaded too (no duplicate messages or database locks)
- The input history shared with XIM is saved safely when both plugins write at once

## Performance

- Keybind lookups are spread across frames (a few per tick, refreshed every 5 s) instead of one framework spike
- The message processing thread sleeps until a message arrives instead of waking every millisecond
- The key list used to release keys while typing is built once instead of on every keypress
- Hide vanilla chat (1.40.9.19): the five chat-window lookups no longer run every frame; once all are hidden the result is reused for 250 ms, and a zone change or login re-checks on the next frame

## Other

- Own plugin icon and AI-generated code disclosure
