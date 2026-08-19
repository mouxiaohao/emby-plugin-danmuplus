using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scraper
{
    public enum PartTitleParseStatus { Absent, Valid, Malformed }
    /// <summary>Strict grammar for upstream Part labels.  This is deliberately
    /// separate from Season parsing: a bare ordinal is never a Part.</summary>
    public static class PartTitleParser
    {
        private static readonly Regex PartRegex = new Regex(
            @"(?:(?<![\p{L}\p{N}])(?:part|部分)\s*[:：._]*\s*(?<after>[0-9]+|[ivxlcdm]+|[零〇一二两三四五六七八九十百千]+)(?=$|[^\p{L}\p{N}])|第?\s*(?<before>[0-9]+|[ivxlcdm]+|[零〇一二两三四五六七八九十百千]+)\s*部分(?=$|[^\p{L}\p{N}]))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PartMarkerRegex = new Regex(@"(?:(?<!\p{L})part(?!ition)|部分)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(string value, out int partNumber)
        {
            partNumber = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Normalize(NormalizationForm.FormKC);
            var match = PartRegex.Match(normalized);
            var ordinal = match.Groups["after"].Success ? match.Groups["after"].Value : match.Groups["before"].Value;
            return match.Success && TryParseOrdinal(ordinal, out partNumber);
        }

        public static PartTitleParseStatus Analyze(string value, out int partNumber)
        {
            partNumber = 0;
            if (string.IsNullOrWhiteSpace(value)) return PartTitleParseStatus.Absent;
            var normalized = value.Normalize(NormalizationForm.FormKC);
            var markers = PartMarkerRegex.Matches(normalized);
            if (markers.Count == 0) return PartTitleParseStatus.Absent;
            var matches = PartRegex.Matches(normalized);
            if (matches.Count != markers.Count) return PartTitleParseStatus.Malformed;
            int? parsed = null;
            foreach (Match match in matches)
            {
                var ordinal = match.Groups["after"].Success ? match.Groups["after"].Value : match.Groups["before"].Value;
                if (!TryParseOrdinal(ordinal, out var current) || (parsed.HasValue && parsed.Value != current))
                    return PartTitleParseStatus.Malformed;
                parsed = current;
            }
            if (!parsed.HasValue) return PartTitleParseStatus.Malformed;
            partNumber = parsed.Value;
            return PartTitleParseStatus.Valid;
        }

        /// <summary>Parses a Part token attached directly to the known parent
        /// title without letting generic parsing mistake a word suffix for it.</summary>
        public static PartTitleParseStatus AnalyzeForFamily(string value, string parentTitle, out int partNumber)
        {
            partNumber = 0;
            if (string.IsNullOrWhiteSpace(value)) return PartTitleParseStatus.Absent;
            var normalized = value.Normalize(NormalizationForm.FormKC);
            var parent = (parentTitle ?? string.Empty).Normalize(NormalizationForm.FormKC);
            var index = string.IsNullOrWhiteSpace(parent) ? -1 : normalized.IndexOf(parent, StringComparison.OrdinalIgnoreCase);
            return Analyze(index < 0 ? normalized : normalized.Remove(index, parent.Length), out partNumber);
        }

        public static PartTitleParseStatus AnalyzeForFamily(string value, string parentTitle, string lastTitle, out int partNumber)
        {
            partNumber = 0;
            var candidate = StripParentAndSeason(value, parentTitle);
            var last = StripParentAndSeason(lastTitle, parentTitle);
            if (!string.IsNullOrWhiteSpace(last) && candidate.StartsWith(last, StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(last.Length);
            return Analyze(candidate, out partNumber);
        }

        private static string StripParentAndSeason(string value, string parentTitle)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var result = value.Normalize(NormalizationForm.FormKC);
            var parent = (parentTitle ?? string.Empty).Normalize(NormalizationForm.FormKC);
            var index = string.IsNullOrWhiteSpace(parent) ? -1 : result.IndexOf(parent, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) result = result.Remove(index, parent.Length);
            return Regex.Replace(result, @"(?:第?[0-9一二三四五六七八九十]+季|season\s*\d+|s\s*\d+)", string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        /// <summary>Removes only expressions accepted by this parser.  Family
        /// analysis uses this rather than a second, drifting Part regex.</summary>
        public static string RemoveValidExpressions(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var normalized = value.Normalize(NormalizationForm.FormKC);
            return PartRegex.Replace(normalized, string.Empty).Trim();
        }

        internal static bool TryParseOrdinal(string value, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number))
            {
                return number > 0;
            }

            if (Regex.IsMatch(value, "^[ivxlcdm]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return TryParseStrictRoman(value, out number);
            }

            return TryParseChinese(value, out number);
        }

        private static bool TryParseStrictRoman(string value, out int number)
        {
            number = 0;
            var roman = value.ToUpperInvariant();
            if (!Regex.IsMatch(roman, "^M{0,3}(CM|CD|D?C{0,3})(XC|XL|L?X{0,3})(IX|IV|V?I{0,3})$")) return false;
            var map = new System.Collections.Generic.Dictionary<char, int>
            {
                ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000,
            };
            for (var index = 0; index < roman.Length; index++)
            {
                var current = map[roman[index]];
                number += index + 1 < roman.Length && current < map[roman[index + 1]] ? -current : current;
            }
            return number > 0;
        }

        private static bool TryParseChinese(string value, out int number)
        {
            number = 0;
            var digits = new System.Collections.Generic.Dictionary<char, int>
            {
                ['零'] = 0, ['〇'] = 0, ['一'] = 1, ['二'] = 2, ['两'] = 2, ['三'] = 3, ['四'] = 4,
                ['五'] = 5, ['六'] = 6, ['七'] = 7, ['八'] = 8, ['九'] = 9,
            };
            var units = new System.Collections.Generic.Dictionary<char, int> { ['十'] = 10, ['百'] = 100, ['千'] = 1000 };
            var current = 0;
            foreach (var character in value)
            {
                if (digits.TryGetValue(character, out var digit)) { current = digit; continue; }
                if (!units.TryGetValue(character, out var unit)) return false;
                if (current == 0) current = 1;
                number += current * unit;
                current = 0;
            }
            number += current;
            return number > 0;
        }
    }
}
