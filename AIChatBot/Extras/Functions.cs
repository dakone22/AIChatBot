using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AIChatBot.Extras;

internal static partial class Functions
{
    public static ICollection<string> GetSimilarWords(ICollection<string> wordsToCheck, ICollection<string> referenceWords)
    {
        var similarWords = new List<string>();
        foreach (var word in wordsToCheck) {
            var threshold = word.Length switch {
                <= 4 => 0,
                > 6 => 2,
                > 4 => 1
            };

            similarWords.AddRange(referenceWords.Where(referenceWord =>
                LevenshteinDistance(word, referenceWord.ToLower()) <= threshold));
        }

        return similarWords;
    }
    
    public static string RemoveSimilarWords(string text, ICollection<string> referenceWords)
    {
        var words = text.Split(' ').Select(word => NotWordRegex().Replace(word.ToLower(), ""));
        
        var detectedWords = Functions.GetSimilarWords(words.ToArray(), referenceWords);
        if (detectedWords.Count <= 2) return text; // TODO: ?

        foreach (var detectedWord in detectedWords.Where(word => word.Length > 2)) {
            text = text.Replace(detectedWord, "");

            while (text.Contains("  "))
                text = text.Replace("  ", " ");
        }

        return text;
    }

    public static DateTime GetCurrentTimeInJapan()
    {
        var utcNow = DateTime.UtcNow;
        var japanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        var currentTimeInJapan = TimeZoneInfo.ConvertTimeFromUtc(utcNow, japanTimeZone);
        return currentTimeInJapan;
    }

    public static string GetTimeOfDayInNaturalLanguage(DateTime dateTime)
    {
        var hour = dateTime.Hour;

        return hour switch {
            >= 5 and < 12 => "Morning",
            >= 12 and < 17 => "Afternoon",
            >= 17 and < 21 => "Evening",
            _ => "Night"
        };
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return 0;

        var d = new int[s.Length + 1, t.Length + 1];

        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;

        for (var j = 0; j <= t.Length; j++) d[0, j] = j;

        for (var i = 1; i <= s.Length; i++)
        for (var j = 1; j <= t.Length; j++) {
            var cost = GetSubstitutionCost(s[i - 1], t[j - 1]);

            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }

        return d[s.Length, t.Length];
    }

    private static int GetSubstitutionCost(char a, char b)
    {
        if (a == b) return 0;

        var isSymbolOrNumberA = !char.IsLetter(a);
        var isSymbolOrNumberB = !char.IsLetter(b);

        if (isSymbolOrNumberA && isSymbolOrNumberB) return 1;
        if (isSymbolOrNumberA || isSymbolOrNumberB) return 2;

        return 1;
    }

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex NotWordRegex();
}