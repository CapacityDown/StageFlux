using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace REPOJP.StagePhysicsEvents;

internal static class EventText
{
    private static readonly Dictionary<EventLanguage, IReadOnlyDictionary<string, string>> Catalogs = new();
    private static readonly Dictionary<EventLanguage, IReadOnlyList<Phrase>> Phrases = new();

    internal static IReadOnlyDictionary<string, string> Catalog(EventLanguage language)
    {
        if (Catalogs.TryGetValue(language, out var catalog)) return catalog;
        using Stream? stream = typeof(EventText).Assembly.GetManifestResourceStream(
            $"REPOJP.StagePhysicsEvents.Localization.{language}.json");
        if (stream == null) return Catalogs[language] = new Dictionary<string, string>();
        var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        return Catalogs[language] = (Dictionary<string, string>)serializer.ReadObject(stream)!;
    }

    internal static string Get(string english, EventLanguage language, string? japanese = null)
    {
        if (language == EventLanguage.English) return english;
        if (language == EventLanguage.Japanese && japanese != null) return japanese;
        return Catalog(language).TryGetValue(english, out var translated) ? translated : english;
    }

    internal static string Format(string english, EventLanguage language, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, Get(english, language), values);

    // Host snapshots keep their existing English contract. Localize only known
    // phrases and labels; values always come from that host, never local config.
    internal static string Description(string english, EventLanguage language)
    {
        if (language == EventLanguage.English || english == "???" || english.Length > 16000) return english;
        if (Catalog(language).TryGetValue(english, out var exact)) return exact;
        if (english.Contains("\n")) return string.Join("\n", english.Replace("\r", "").Split('\n').Select(line => Description(line, language)));
        int separator = english.IndexOf(": ", StringComparison.Ordinal);
        if (separator > 0 && Catalog(language).TryGetValue(english.Substring(0, separator), out var label))
            return label + ": " + Description(english.Substring(separator + 2), language);
        if (!Phrases.TryGetValue(language, out var phrases))
        {
            phrases = Catalog(language).Select(pair => new Phrase(pair.Key, pair.Value)).ToArray();
            Phrases[language] = phrases;
        }
        StringBuilder output = new();
        int position = 0;
        try
        {
            while (position < english.Length)
            {
                if (char.IsWhiteSpace(english[position])) { output.Append(english[position++]); continue; }
                Phrase? selected = null;
                Match? selectedMatch = null;
                int length = 0;
                foreach (Phrase phrase in phrases)
                {
                    if (string.CompareOrdinal(english, position, phrase.Prefix, 0, phrase.Prefix.Length) != 0) continue;
                    Match match = phrase.Pattern.Match(english, position);
                    if (match.Success && match.Length > length)
                    { selected = phrase; selectedMatch = match; length = match.Length; }
                }
                if (selected == null || selectedMatch == null || length == 0) return english;
                object[] arguments = new object[selected.ArgumentCount];
                for (int i = 0; i < arguments.Length; i++) arguments[i] = selectedMatch.Groups["p" + i].Value;
                output.Append(string.Format(CultureInfo.InvariantCulture, selected.Translation, arguments));
                position += length;
            }
            return output.ToString();
        }
        catch (RegexMatchTimeoutException) { return english; }
        catch (FormatException) { return english; }
    }

    private sealed class Phrase
    {
        internal string Prefix { get; }
        internal Regex Pattern { get; }
        internal string Translation { get; }
        internal int ArgumentCount { get; }
        internal Phrase(string source, string translated)
        {
            Translation = translated;
            int first = source.IndexOf('{');
            Prefix = first < 0 ? source : source.Substring(0, first);
            int count = 0;
            string pattern = Regex.Replace(Regex.Escape(source), @"\\\{(\d+)}", match =>
            {
                int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                count = Math.Max(count, index + 1);
                return "(?<p" + index + @">[^\r\n]{1,256}?)";
            });
            Pattern = new Regex(@"\G" + pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));
            ArgumentCount = count;
        }
    }
}
