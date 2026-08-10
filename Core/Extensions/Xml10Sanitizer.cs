using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    /// <summary>
    /// Removes characters and numeric character references that XML 1.0 cannot represent.
    /// Valid supplementary Unicode scalars are preserved as UTF-16 surrogate pairs.
    /// </summary>
    public static class Xml10Sanitizer
    {
        private static readonly Regex NumericCharacterReference = new Regex(
            @"&#(?<hex>[xX])?(?<value>[0-9A-Fa-f]+);",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var result = new StringBuilder(text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    {
                        result.Append(current);
                        result.Append(text[++index]);
                    }

                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    continue;
                }

                if (IsLegalScalar(current))
                {
                    result.Append(current);
                }
            }

            return result.ToString();
        }

        public static string SanitizeDocument(string xml)
        {
            var sanitized = SanitizeText(xml);
            if (string.IsNullOrEmpty(sanitized))
            {
                return sanitized;
            }

            var result = new StringBuilder(sanitized.Length);
            var position = 0;
            while (position < sanitized.Length)
            {
                var cdataStart = sanitized.IndexOf("<![CDATA[", position, StringComparison.Ordinal);
                if (cdataStart < 0)
                {
                    result.Append(SanitizeNumericCharacterReferences(sanitized.Substring(position)));
                    break;
                }

                result.Append(SanitizeNumericCharacterReferences(
                    sanitized.Substring(position, cdataStart - position)));
                var cdataEnd = sanitized.IndexOf("]]>", cdataStart + 9, StringComparison.Ordinal);
                if (cdataEnd < 0)
                {
                    result.Append(sanitized.Substring(cdataStart));
                    break;
                }

                var cdataLength = cdataEnd + 3 - cdataStart;
                result.Append(sanitized.Substring(cdataStart, cdataLength));
                position = cdataEnd + 3;
            }

            return result.ToString();
        }

        private static string SanitizeNumericCharacterReferences(string value)
        {
            return NumericCharacterReference.Replace(value, match =>
            {
                var style = match.Groups["hex"].Success
                    ? NumberStyles.AllowHexSpecifier
                    : NumberStyles.None;
                return uint.TryParse(
                           match.Groups["value"].Value,
                           style,
                           CultureInfo.InvariantCulture,
                           out var scalar) && IsLegalScalar(scalar)
                    ? match.Value
                    : string.Empty;
            });
        }

        private static bool IsLegalScalar(uint scalar)
        {
            return scalar == 0x9 || scalar == 0xA || scalar == 0xD ||
                   scalar >= 0x20 && scalar <= 0xD7FF ||
                   scalar >= 0xE000 && scalar <= 0xFFFD ||
                   scalar >= 0x10000 && scalar <= 0x10FFFF;
        }
    }
}
