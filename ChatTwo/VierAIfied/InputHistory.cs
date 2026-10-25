// VierAI-fied fork support (AI generated code).
// Input history for the chat box (Up/Down recall). Upstream keeps it per window and only in memory,
// so it is empty after every restart. Here one list is shared by every input box and saved to the
// plugin's config folder, and Down past the newest entry restores what was being typed.
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using ChatTwo.Ui.Handler;

namespace ChatTwo.VierAIfied;

internal static class InputHistory
{
    private const int MaxEntries = 200;
    private const string FileName = "vierai-input-history.json";

    private static readonly List<string> Entries = [];
    private static bool loaded;

    // The list every SendHandler uses as its InputBacklog.
    internal static List<string> Shared()
    {
        EnsureLoaded();
        return Entries;
    }

    private static string FilePath => Path.Combine(Plugin.Interface.ConfigDirectory.FullName, FileName);

    // The file is shared with XIV Instant Messenger, so it is re-read whenever it changed on disk
    // and merged before every save instead of being overwritten.
    private static DateTime lastWrite = DateTime.MinValue;

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        Reload();
    }

    private static List<string>? ReadFile()
    {
        var path = FilePath;
        if (!File.Exists(path))
            return null;

        lastWrite = File.GetLastWriteTimeUtc(path);
        var items = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
        return items?.Where(item => !string.IsNullOrWhiteSpace(item)).TakeLast(MaxEntries).ToList();
    }

    // Picks up entries another plugin wrote. Keeps the same list instance the SendHandlers hold.
    internal static void Reload()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) == lastWrite)
                return;

            var items = ReadFile();
            if (items == null)
                return;

            Entries.Clear();
            Entries.AddRange(items);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not read the saved chat input history");
        }
    }

    // Call after the shared list changed (a message was added at the end).
    // Chat 2 and XIM both write the file, so the read-merge-write runs under a session-wide mutex
    // (same name in both plugins) and goes through a uniquely named temp file.
    private const string MutexName = @"Local\VierAI-fied-input-history";

    internal static void Save()
    {
        Mutex? mutex = null;
        var owned = false;
        try
        {
            mutex = new Mutex(false, MutexName);
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned)
            {
                Plugin.Log.Warning("Chat input history is busy (XIM is saving); skipped this save");
                return;
            }

            if (Entries.Count > 0)
            {
                var added = Entries[^1];
                List<string>? disk = null;
                try { disk = ReadFile(); }
                catch (Exception ex) { Plugin.Log.Warning(ex, "Could not re-read the chat input history before saving"); }

                if (disk != null)
                {
                    disk.RemoveAll(item => item == added);
                    disk.Add(added);
                    Entries.Clear();
                    Entries.AddRange(disk);
                }
            }

            if (Entries.Count > MaxEntries)
                Entries.RemoveRange(0, Entries.Count - MaxEntries);

            var temp = $"{FilePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(Entries));
                File.Move(temp, FilePath, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best effort */ }
            }
            lastWrite = File.GetLastWriteTimeUtc(FilePath);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not save the chat input history");
        }
        finally
        {
            if (owned) mutex!.ReleaseMutex();
            mutex?.Dispose();
        }
    }
}

// Up/Down handling for one input box. Up on the line recalls older entries, Down walks forward again
// and finally gives back the text that was being typed.
internal sealed class HistoryNavigator
{
    private readonly SendHandler Send;
    private string? Draft;

    internal HistoryNavigator(SendHandler send)
    {
        Send = send;
    }

    internal int OnHistoryKey(ref ImGuiInputTextCallbackData data, ImGuiKey key)
    {
        var list = Send.InputBacklog;
        var previous = Send.InputBacklogIdx;
        var index = Math.Min(previous, list.Count - 1);

        switch (key)
        {
            case ImGuiKey.UpArrow:
                if (list.Count == 0)
                    return 0;

                if (index == -1)
                {
                    InputHistory.Reload();
                    if (list.Count == 0)
                        return 0;

                    Draft = ReadBuffer(ref data);
                    index = list.Count - 1;
                }
                else if (index > 0)
                {
                    index--;
                }
                break;
            case ImGuiKey.DownArrow:
                if (index == -1)
                    return 0;

                if (++index >= list.Count)
                    index = -1;
                break;
        }

        if (index == previous)
            return 0;

        Send.InputBacklogIdx = index;
        var text = index >= 0 ? list[index] : Draft ?? string.Empty;
        if (index == -1)
            Draft = null;

        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, text);
        return 0;
    }

    private static unsafe string ReadBuffer(ref ImGuiInputTextCallbackData data)
    {
        return System.Text.Encoding.UTF8.GetString(data.Buf, data.BufTextLen);
    }
}