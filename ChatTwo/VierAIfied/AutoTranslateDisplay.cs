// VierAI-fied fork support (AI generated code).
// Auto-translate entries are shown in the input box as the phrase wrapped in the game's
// auto-translate arrow glyphs (like the vanilla chat box) instead of a raw <at:group,row> code.
// Before sending or previewing, the arrow-wrapped phrases are turned back into <at:..> tags so
// the normal payload code still builds the real auto-translate payload.
using System.Collections.Concurrent;
using System.Text;
using ChatTwo.Util;

namespace ChatTwo.VierAIfied;

internal static class AutoTranslateDisplay
{
    internal const char Open = '\uE040';
    internal const char Close = '\uE041';
    private static readonly ConcurrentDictionary<string, (uint Group, uint Row)> Known = new();

    internal static string Insertion(AutoTranslateEntry entry)
    {
        if (entry.PlainText)
            return entry.Text;
        Known[entry.Text] = (entry.Group, entry.Row);
        return $"{Open}{entry.Text}{Close}";
    }

    private static bool Resolve(string text, out (uint Group, uint Row) id)
    {
        if (Known.TryGetValue(text, out id))
            return true;
        foreach (var e in AutoTranslate.Matching(text, false))
        {
            if (!e.PlainText && e.Text == text)
            {
                id = (e.Group, e.Row);
                Known[text] = id;
                return true;
            }
        }
        return false;
    }

    internal static string ToTags(string input)
    {
        if (input.IndexOf(Open) < 0)
            return input;
        var sb = new StringBuilder(input.Length);
        var i = 0;
        while (i < input.Length)
        {
            var o = input.IndexOf(Open, i);
            var c = o < 0 ? -1 : input.IndexOf(Close, o + 1);
            if (o < 0 || c < 0)
            {
                sb.Append(input, i, input.Length - i);
                break;
            }
            sb.Append(input, i, o - i);
            var phrase = input[(o + 1)..c];
            if (Resolve(phrase, out var id))
                sb.Append($"<at:{id.Group},{id.Row}>");
            else
                sb.Append(input, o, c - o + 1);
            i = c + 1;
        }
        return sb.ToString();
    }

    internal static byte[] ToTags(byte[] bytes)
    {
        var s = Encoding.UTF8.GetString(bytes);
        if (s.IndexOf(Open) < 0)
            return bytes;
        return Encoding.UTF8.GetBytes(ToTags(s));
    }
}
