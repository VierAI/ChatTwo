// VierAI-fied fork support (AI generated code).
// Spell checking for the chat input box, shared word for word by the VierAI-fied XIM and Chat 2.
// Engine: WeCantSpell.Hunspell with the SCOWL en_US Hunspell dictionary (embedded), an FFXIV word list
// (embedded) and a user dictionary shared by both plugins at
// pluginConfigs\ChatTwoVierAIfied\vierai-user-dictionary.txt (one word per line).
// Ignored words and the marking style live in the shared VierAI config,
// pluginConfigs\ChatTwoVierAIfied\vierai-config.json, section "SpellCheck":
//   { "SpellCheck": { "Ignored": ["word", ...], "Style": "Highlight" | "Wavy" | "Straight", "Colour": "#RRGGBBAA",
//                     "HoverSuggestions": true, "HighlightFill": 0.10, "StyleRev": 3 } }
// StyleRev marks which defaults a config has seen: below 2 is switched to Highlight once, below 3 gets the 10% fill.
// Other top-level sections are kept as they are. Both plugins write it under one named mutex via a GUID temp file.
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using WeCantSpell.Hunspell;

namespace VierAIfiedShared;

// A misspelled word, as char offsets into the input text.
internal readonly record struct SpellSpan(int Start, int Length);

internal static partial class SpellChecker
{
    internal const string SharedFolder = "ChatTwoVierAIfied";
    private const string UserFileName = "vierai-user-dictionary.txt";
    private const string MutexName = @"Local\VierAI-fied-user-dictionary";
    private const string ConfigFileName = "vierai-config.json";
    private const string ConfigMutexName = @"Local\VierAI-fied-config";
    private const string Section = "SpellCheck";
    private const uint DefaultColour = 0xFF3A3AFF; // ABGR: red

    private static WordList? words;
    private static Task? loading;
    private static readonly HashSet<string> Extra = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> user = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> ignored = new(StringComparer.OrdinalIgnoreCase);
    private static string? configPath;
    private static DateTime configStamp;
    private static HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> Cache = new(StringComparer.Ordinal);
    private static string? userPath;
    private static DateTime userStamp;
    private static long nextUserCheck, nextNames;
    private static Action<string> log = _ => { };
    private static Func<IEnumerable<string>>? nameSource;

    // Bumped whenever the dictionaries change, so input boxes re-check their text.
    internal static int Generation;
    internal static bool Ready => words != null;

    // configDirectory: the plugin's own config folder. Loading runs in the background.
    internal static void Init(string configDirectory, Action<string> logger, Func<IEnumerable<string>>? playerNames)
    {
        log = logger;
        nameSource = playerNames;
        var parent = Directory.GetParent(configDirectory.TrimEnd('\\', '/'))?.FullName ?? configDirectory;
        userPath = Path.Combine(parent, SharedFolder, UserFileName);
        configPath = Path.Combine(parent, SharedFolder, ConfigFileName);
        ReloadConfig(true);
        if(loading != null) return;
        var asm = typeof(SpellChecker).Assembly;
        loading = Task.Run(() =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var dic = Open(asm, "en_US.dic");
                using var aff = Open(asm, "en_US.aff");
                var list = WordList.CreateFromStreams(dic, aff);
                using(var reader = new StreamReader(Open(asm, "ffxiv-words.txt")))
                {
                    string? line;
                    while((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if(line.Length > 0 && line[0] != '#') Extra.Add(Normalize(line));
                    }
                }
                ReloadUser(true);
                words = list;
                Cache.Clear();
                Analyze("Warm up teh checker, Zorbly Qwip don't it's Frobnicated aren't Xylophones", -1); // JIT and regex warm-up off the game thread
                Interlocked.Increment(ref Generation);
                log($"Spell checker ready in {sw.ElapsedMilliseconds} ms");
            }
            catch(Exception ex)
            {
                log($"Spell checker could not load: {ex.Message}");
            }
        });
    }

    private static Stream Open(Assembly asm, string suffix)
    {
        var name = asm.GetManifestResourceNames().First(x => x.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return asm.GetManifestResourceStream(name)!;
    }

    private static string Normalize(string word) => word.Replace('\u2019', '\'');

    // Cheap housekeeping, called from the draw code: picks up dictionary edits made by the other plugin
    // (file timestamp every 2 s) and refreshes nearby player names (every 5 s).
    internal static void Poll()
    {
        var now = Environment.TickCount64;
        if(now >= nextUserCheck)
        {
            nextUserCheck = now + 2000;
            ReloadUser(false);
            ReloadConfig(false);
        }
        if(nameSource != null && now >= nextNames)
        {
            nextNames = now + 5000;
            try
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var full in nameSource())
                {
                    if(string.IsNullOrWhiteSpace(full)) continue;
                    foreach(var part in full.Split(' ', '@')) if(part.Length > 0) set.Add(Normalize(part));
                }
                if(!set.SetEquals(names))
                {
                    names = set;
                    Interlocked.Increment(ref Generation);
                }
            }
            catch(Exception ex)
            {
                log($"Spell checker could not read player names: {ex.Message}");
            }
        }
    }

    private static void ReloadUser(bool force)
    {
        try
        {
            if(userPath == null || !File.Exists(userPath)) return;
            var stamp = File.GetLastWriteTimeUtc(userPath);
            if(!force && stamp == userStamp) return;
            userStamp = stamp;
            user = new HashSet<string>(File.ReadAllLines(userPath).Select(x => Normalize(x.Trim())).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
            Interlocked.Increment(ref Generation);
        }
        catch(Exception ex)
        {
            log($"Spell checker could not read the user dictionary: {ex.Message}");
        }
    }

    internal static bool IsCorrect(string word)
    {
        var w = Normalize(word);
        var stem = w.EndsWith("'s", StringComparison.OrdinalIgnoreCase) ? w[..^2] : w;
        if(Extra.Contains(w) || Extra.Contains(stem) || user.Contains(w) || user.Contains(stem) || ignored.Contains(w)
           || names.Contains(w) || names.Contains(stem)) return true;
        var list = words;
        if(list == null) return true;
        if(Cache.TryGetValue(w, out var ok)) return ok;
        ok = list.Check(w);
        if(Cache.Count > 20000) Cache.Clear();
        Cache[w] = ok;
        return ok;
    }

    internal static Task<List<string>> SuggestAsync(string word)
    {
        var list = words;
        if(list == null) return Task.FromResult(new List<string>());
        return Task.Run(() =>
        {
            try { return list.Suggest(Normalize(word)).Take(8).ToList(); }
            catch(Exception ex) { log($"Spell checker suggestion failed: {ex.Message}"); return new List<string>(); }
        });
    }

    // Squiggle look, from the shared config.
    internal static uint SquiggleColour { get; private set; } = DefaultColour;
    internal static SpellStyle Style { get; private set; } = SpellStyle.Highlight;
    internal static bool HoverSuggestions { get; private set; } = true;
    internal static float HighlightFill { get; private set; } = DefaultFill;
    private const float DefaultFill = 0.10f;
    private const int CurrentStyleRev = 3;
    internal static int IgnoredCount => ignored.Count;

    // "Ignore": remembered for good in the shared VierAI config, so it holds in both plugins and after reloads.
    internal static void Ignore(string word)
    {
        word = Normalize(word.Trim());
        if(word.Length == 0) return;
        ignored = new HashSet<string>(ignored, StringComparer.OrdinalIgnoreCase) { word };
        Interlocked.Increment(ref Generation);
        UpdateConfig(sc =>
        {
            var arr = sc["Ignored"] as JsonArray ?? new JsonArray();
            if(!arr.Any(x => string.Equals(x?.GetValue<string>(), word, StringComparison.OrdinalIgnoreCase))) arr.Add(word);
            sc["Ignored"] = Sorted(arr);
        });
    }

    internal static void ClearIgnored()
    {
        ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Interlocked.Increment(ref Generation);
        UpdateConfig(sc => sc["Ignored"] = new JsonArray());
    }

    private static JsonArray Sorted(JsonArray arr)
    {
        var words = arr.Select(x => x?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!)
                       .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        return new JsonArray(words.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
    }

    // Settings rows for the host's settings tab: marking style, colour, hover popup, and clearing ignored words.
    internal static void DrawSettings()
    {
        ImGui.TextUnformatted("Mark misspelled words with:");
        ImGui.SameLine();
        if(ImGui.RadioButton("Highlight##vierai-sc-highlight", Style == SpellStyle.Highlight) && Style != SpellStyle.Highlight) SetStyle(SpellStyle.Highlight, SquiggleColour, HoverSuggestions);
        ImGui.SameLine();
        if(ImGui.RadioButton("Wavy##vierai-sc-wavy", Style == SpellStyle.Wavy) && Style != SpellStyle.Wavy) SetStyle(SpellStyle.Wavy, SquiggleColour, HoverSuggestions);
        ImGui.SameLine();
        if(ImGui.RadioButton("Straight##vierai-sc-straight", Style == SpellStyle.Straight) && Style != SpellStyle.Straight) SetStyle(SpellStyle.Straight, SquiggleColour, HoverSuggestions);
        ImGui.SameLine();
        var col = ImGui.ColorConvertU32ToFloat4(SquiggleColour);
        if(ImGui.ColorEdit4("##vierai-sc-colour", ref col, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            pendingColour = ImGui.ColorConvertFloat4ToU32(col);
        if(pendingColour is { } pc && !ImGui.IsItemActive()) { pendingColour = null; SetStyle(Style, pc, HoverSuggestions); }
        else if(pendingColour is { } live) SquiggleColour = live;
        ImGui.SameLine();
        if(ImGui.SmallButton("Reset##vierai-sc-reset")) SetStyle(SpellStyle.Highlight, DefaultColour, HoverSuggestions);
        if(Style == SpellStyle.Highlight)
        {
            var pct = HighlightFill * 100f;
            ImGui.SetNextItemWidth(160 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
            if(ImGui.SliderFloat("Highlight fill##vierai-sc-fill", ref pct, 0f, 50f, "%.0f%%")) HighlightFill = Math.Clamp(pct / 100f, 0f, 0.5f);
            if(ImGui.IsItemDeactivatedAfterEdit()) SetStyle(Style, SquiggleColour, HoverSuggestions);
        }
        var hover = HoverSuggestions;
        if(ImGui.Checkbox("Show suggestions on hover##vierai-sc-hover", ref hover)) SetStyle(Style, SquiggleColour, hover);
        ImGui.SameLine();
        using(var _ = new Disabled(ignored.Count == 0))
            if(ImGui.SmallButton($"Clear ignored words ({ignored.Count})##vierai-sc-clear")) ClearIgnored();
    }

    private static uint? pendingColour;

    private readonly struct Disabled : IDisposable
    {
        private readonly bool on;
        public Disabled(bool on) { this.on = on; if(on) ImGui.BeginDisabled(); }
        public void Dispose() { if(on) ImGui.EndDisabled(); }
    }

    private static void SetStyle(SpellStyle style, uint colour, bool hover)
    {
        Style = style;
        SquiggleColour = colour;
        HoverSuggestions = hover;
        UpdateConfig(sc =>
        {
            sc["Style"] = style.ToString();
            sc["Colour"] = ToHex(colour);
            sc["HoverSuggestions"] = hover;
            sc["HighlightFill"] = Math.Round(HighlightFill, 3);
            sc["StyleRev"] = CurrentStyleRev;
        });
    }

    private static bool styleMigrated;

    private static int StyleRevOf(JsonObject? sc) => sc?["StyleRev"] is JsonValue v && v.TryGetValue<int>(out var r) ? r : 0;

    // ImGui colours are ABGR in a uint; the file stores #RRGGBBAA.
    private static string ToHex(uint abgr) => $"#{abgr & 0xFF:X2}{(abgr >> 8) & 0xFF:X2}{(abgr >> 16) & 0xFF:X2}{abgr >> 24:X2}";

    private static uint? FromHex(string? hex)
    {
        if(hex is not { Length: 9 } || hex[0] != '#' || !uint.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out var rgba)) return null;
        return ((rgba >> 24) & 0xFF) | (((rgba >> 16) & 0xFF) << 8) | (((rgba >> 8) & 0xFF) << 16) | ((rgba & 0xFF) << 24);
    }

    private static void ReloadConfig(bool force)
    {
        try
        {
            if(configPath == null || !File.Exists(configPath)) return;
            var stamp = File.GetLastWriteTimeUtc(configPath);
            if(!force && stamp == configStamp) return;
            configStamp = stamp;
            var root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
            ApplyConfig(root);
            // One-time switch to the Highlight default for configs written before it existed.
            if(!styleMigrated && StyleRevOf(root?[Section] as JsonObject) < CurrentStyleRev)
            {
                styleMigrated = true;
                SetStyle(SpellStyle.Highlight, SquiggleColour, HoverSuggestions);
            }
        }
        catch(Exception ex)
        {
            log($"Spell checker could not read {ConfigFileName}: {ex.Message}");
        }
    }

    private static void ApplyConfig(JsonObject? root)
    {
        var sc = root?[Section] as JsonObject;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(sc?["Ignored"] is JsonArray arr)
            foreach(var x in arr) if(x is JsonValue v && v.TryGetValue<string>(out var w) && w.Trim().Length > 0) set.Add(Normalize(w.Trim()));
        var rev = StyleRevOf(sc);
        HighlightFill = rev < 3 || sc?["HighlightFill"] is not JsonValue fv || !fv.TryGetValue<double>(out var fd) ? DefaultFill : Math.Clamp((float)fd, 0f, 0.5f);
        Style = rev < 2 ? SpellStyle.Highlight
              : Enum.TryParse<SpellStyle>(sc?["Style"]?.ToString(), true, out var st) && Enum.IsDefined(st) ? st : SpellStyle.Highlight;
        HoverSuggestions = sc?["HoverSuggestions"] is not JsonValue hv || !hv.TryGetValue<bool>(out var h) || h;
        if(pendingColour == null) SquiggleColour = FromHex(sc?["Colour"]?.ToString()) ?? DefaultColour;
        if(!set.SetEquals(ignored))
        {
            ignored = set;
            Interlocked.Increment(ref Generation);
        }
    }

    // Read-modify-write of the shared config under a session-wide named mutex, through a GUID temp file.
    // The change is applied to what is on disk at that moment, so edits from the other plugin are kept.
    private static void UpdateConfig(Action<JsonObject> change)
    {
        if(configPath == null) return;
        Mutex? mutex = null;
        var owned = false;
        try
        {
            mutex = new Mutex(false, ConfigMutexName);
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch(AbandonedMutexException) { owned = true; }
            if(!owned)
            {
                log($"{ConfigFileName} is busy; kept the change for this session only");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            JsonObject root;
            try { root = (File.Exists(configPath) ? JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject : null) ?? new JsonObject(); }
            catch(JsonException) { root = new JsonObject(); log($"{ConfigFileName} was not valid JSON; starting it again"); }
            if(root[Section] is not JsonObject sc) root[Section] = sc = new JsonObject();
            change(sc);
            var temp = $"{configPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
                File.Move(temp, configPath, true);
            }
            finally
            {
                try { if(File.Exists(temp)) File.Delete(temp); } catch { }
            }
            configStamp = File.GetLastWriteTimeUtc(configPath);
            ApplyConfig(root);
        }
        catch(Exception ex)
        {
            log($"Could not save {ConfigFileName}: {ex.Message}");
        }
        finally
        {
            if(owned) mutex!.ReleaseMutex();
            mutex?.Dispose();
        }
    }

    // Adds a word to the shared user dictionary. Both plugins write the file, so the read-merge-write
    // runs under a session-wide named mutex and goes through a uniquely named temp file.
    internal static void AddToDictionary(string word)
    {
        word = Normalize(word.Trim());
        if(word.Length == 0) return;
        var copy = new HashSet<string>(user, StringComparer.OrdinalIgnoreCase) { word };
        user = copy;
        Interlocked.Increment(ref Generation);
        if(userPath == null) return;
        Mutex? mutex = null;
        var owned = false;
        try
        {
            mutex = new Mutex(false, MutexName);
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch(AbandonedMutexException) { owned = true; }
            if(!owned)
            {
                log("User dictionary is busy; kept the word for this session only");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
            var merged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if(File.Exists(userPath)) foreach(var l in File.ReadAllLines(userPath)) if(l.Trim().Length > 0) merged.Add(Normalize(l.Trim()));
            merged.UnionWith(copy);
            var temp = $"{userPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllLines(temp, merged, new UTF8Encoding(false));
                File.Move(temp, userPath, true);
            }
            finally
            {
                try { if(File.Exists(temp)) File.Delete(temp); } catch { }
            }
            user = new HashSet<string>(merged, StringComparer.OrdinalIgnoreCase);
            userStamp = File.GetLastWriteTimeUtc(userPath);
        }
        catch(Exception ex)
        {
            log($"Could not save the user dictionary: {ex.Message}");
        }
        finally
        {
            if(owned) mutex!.ReleaseMutex();
            mutex?.Dispose();
        }
    }

    // Finds the misspelled words. cursor (char index, -1 for none) skips the word being typed right now.
    internal static List<SpellSpan> Analyze(string text, int cursor)
    {
        var result = new List<SpellSpan>();
        if(!Ready || string.IsNullOrEmpty(text)) return result;
        var i = 0;
        var tokenIndex = 0;
        var skipTellTarget = 0;
        while(i < text.Length)
        {
            while(i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if(i >= text.Length) break;
            var start = i;
            // <at:group,key> auto-translate placeholders (Chat 2) are skipped whole, even with spaces inside.
            if(text[i] == '<')
            {
                var close = text.IndexOf('>', i);
                if(close != -1) { i = close + 1; continue; }
            }
            while(i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            var token = text.AsSpan(start, i - start);
            var first = tokenIndex++ == 0;
            if(first && token.Length > 0 && token[0] == '/')
            {
                // /tell First Last@World message: the target is a name.
                if(token.Equals("/t", StringComparison.OrdinalIgnoreCase) || token.Equals("/tell", StringComparison.OrdinalIgnoreCase)) skipTellTarget = 2;
                continue;
            }
            if(skipTellTarget > 0)
            {
                skipTellTarget = token.Contains('@') ? 0 : skipTellTarget - 1;
                continue;
            }
            if(SkipToken(token)) continue;
            foreach(Match m in WordRegex().Matches(text, start).Cast<Match>().TakeWhile(m => m.Index < i))
            {
                var w = m.Value;
                if(w.Length < 2) continue;
                if(cursor >= 0 && m.Index + m.Length == cursor) continue;
                if(IsAllCaps(w)) continue;
                if(!IsCorrect(w)) result.Add(new SpellSpan(m.Index, m.Length));
            }
        }
        return result;
    }

    private static bool SkipToken(ReadOnlySpan<char> token)
    {
        if(token[0] == '/') return true;
        if(token.Contains("://", StringComparison.Ordinal) || token.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return true;
        if(token.Length > 2 && token[0] == ':' && token[^1] == ':') return true; // :emoji:
        for(var k = 0; k < token.Length; k++)
        {
            var c = token[k];
            if(char.IsDigit(c) || c == '@' || c == '<' || c == '>' || c == '{' || c == '}' || c == '#' || c == '_') return true;
            if(c >= '\uE000' && c <= '\uF8FF') return true; // game glyphs, auto-translate markers
            // something.com / file.txt
            if(c == '.' && k > 0 && k < token.Length - 1 && char.IsLetter(token[k - 1]) && char.IsLetter(token[k + 1])) return true;
        }
        return false;
    }

    private static bool IsAllCaps(string w)
    {
        var letters = 0;
        foreach(var c in w)
        {
            if(char.IsLetter(c))
            {
                if(!char.IsUpper(c)) return false;
                letters++;
            }
        }
        return letters >= 2;
    }

    [GeneratedRegex(@"\p{L}+(?:['\u2019]\p{L}+)*")]
    private static partial Regex WordRegex();
}

internal enum SpellStyle { Highlight, Wavy, Straight }
