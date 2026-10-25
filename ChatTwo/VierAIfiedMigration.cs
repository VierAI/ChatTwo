// VierAI-fied fork support (AI generated code).
//
// Dalamud hides custom-repo plugins whose InternalName is also in the official repo, so this
// fork ships under InternalName "ChatTwoVierAIfied". Dalamud keys config files by InternalName, so:
//  - ImportOfficialSettings copies the official plugin's settings once, when the fork has none yet.
//    It only ever reads the official files; it never modifies or deletes them.
//  - OfficialConfigDirectory points shared data (the chat-sqlite.db message database) at the official plugin's folder,
//    so history stays in one place whichever build is installed.
// If the official plugin is loaded too, the fork uses its own database for that session (see DatabaseDirectory).
using System;
using System.IO;
using Dalamud.Plugin;

namespace ChatTwo;

internal static class VierAIfiedMigration
{
    internal const string OfficialInternalName = "ChatTwo";

    // True when this build runs under a different InternalName than the official plugin.
    internal static bool IsRenamed(IDalamudPluginInterface pi) =>
        !string.Equals(pi.InternalName, OfficialInternalName, StringComparison.OrdinalIgnoreCase);

    // pluginConfigs\<official InternalName>, the official plugin's config folder (created if missing).
    internal static string OfficialConfigDirectory(IDalamudPluginInterface pi)
    {
        if (!IsRenamed(pi))
            return pi.GetPluginConfigDirectory();
        var dir = Path.Combine(pi.ConfigFile.Directory!.FullName, OfficialInternalName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string? databaseDirectory;

    // Folder for chat-sqlite.db. Normally the official plugin's folder, so history is shared with it.
    // If the official ChatTwo is loaded right now, both would write the same database (duplicate rows,
    // "database is locked"), so this session uses the fork's own folder instead. Decided once per load.
    internal static string DatabaseDirectory(IDalamudPluginInterface pi, Action<string> warn)
    {
        if (databaseDirectory != null)
            return databaseDirectory;
        if (IsRenamed(pi) && OfficialPluginLoaded(pi))
        {
            databaseDirectory = pi.GetPluginConfigDirectory();
            warn($"[VierAI-fied] The official {OfficialInternalName} plugin is loaded, so this fork is using its own message database ({databaseDirectory}) to avoid sharing one. Disable one of the two and reload to use the shared history.");
        }
        else
        {
            databaseDirectory = OfficialConfigDirectory(pi);
        }
        return databaseDirectory;
    }

    internal static bool OfficialPluginLoaded(IDalamudPluginInterface pi)
    {
        try
        {
            foreach (var p in pi.InstalledPlugins)
                if (p.IsLoaded && string.Equals(p.InternalName, OfficialInternalName, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        catch
        {
            // If the list can't be read, assume it's loaded and keep the databases apart.
            return true;
        }
        return false;
    }

    // Call once at startup, before the plugin loads its configuration.
    internal static void ImportOfficialSettings(IDalamudPluginInterface pi, Action<string> log)
    {
        try
        {
            if (!IsRenamed(pi))
                return;

            var root = pi.ConfigFile.Directory!.FullName;
            var newJson = pi.ConfigFile.FullName;
            var newDir = Path.Combine(root, pi.InternalName);
            var oldJson = Path.Combine(root, OfficialInternalName + ".json");
            var oldDir = Path.Combine(root, OfficialInternalName);

            if (HasOwnConfig(newJson, newDir))
                return;

            var copied = 0;
            if (File.Exists(oldJson))
            {
                File.Copy(oldJson, newJson, false);
                copied++;
            }

            if (Directory.Exists(oldDir))
                copied += CopyFolder(oldDir, newDir, true);

            log($"[VierAI-fied] Imported {copied} settings file(s) from the official {OfficialInternalName} plugin.");
        }
        catch (Exception e)
        {
            log($"[VierAI-fied] Settings import from {OfficialInternalName} failed: {e.Message}");
        }
    }

    private static int CopyFolder(string from, string to, bool topLevel)
    {
        var count = 0;
        foreach (var file in Directory.GetFiles(from))
        {
            var name = Path.GetFileName(file);
            if (IsShared(name, topLevel))
                continue;
            var target = Path.Combine(to, name);
            if (File.Exists(target))
                continue;
            Directory.CreateDirectory(to);
            File.Copy(file, target, false);
            count++;
        }

        foreach (var dir in Directory.GetDirectories(from))
        {
            var name = Path.GetFileName(dir);
            if (IsSharedFolder(name, topLevel))
                continue;
            count += CopyFolder(dir, Path.Combine(to, name), false);
        }

        return count;
    }

    // The fork has its own settings once ChatTwoVierAIfied.json exists.
    private static bool HasOwnConfig(string newJson, string newDir) => File.Exists(newJson);

    // The message database stays in the official folder (see MessageManager.DatabasePath).
    private static bool IsShared(string fileName, bool topLevel) =>
        topLevel && (fileName.StartsWith("chat-sqlite.db", StringComparison.OrdinalIgnoreCase)
                     || fileName.StartsWith("chat.db", StringComparison.OrdinalIgnoreCase)
                     || fileName.StartsWith("chat-litedb.db", StringComparison.OrdinalIgnoreCase));

    private static bool IsSharedFolder(string folderName, bool topLevel) => false;
}