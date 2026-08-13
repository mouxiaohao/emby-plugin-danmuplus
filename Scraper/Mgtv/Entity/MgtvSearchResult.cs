using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scrapers.Mgtv.Entity
{
    /// <summary>
    /// The bounded public PC suggestion response.  This is deliberately kept
    /// separate from the normalized provider candidate below: suggestion
    /// fields are untrusted until <see cref="MgtvSuggestionNormalizer"/> has
    /// checked them.
    /// </summary>
    public class MgtvSearchResult
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("data")]
        public MgtvSearchData Data { get; set; }
    }

    public class MgtvSearchData
    {
        [JsonPropertyName("suggest")]
        public List<MgtvSearchSuggestion> Suggest { get; set; }
    }

    public class MgtvSearchSuggestion
    {
        [JsonPropertyName("cid")]
        public JsonElement? CollectionId { get; set; }

        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("showTitle")]
        public string ShowTitle { get; set; }

        [JsonPropertyName("hit")]
        public string Hit { get; set; }

        // The endpoint has returned both numeric and string values here.  It
        // is not the display category, so keep it opaque rather than letting a
        // numeric value make the whole otherwise-valid response malformed.
        [JsonPropertyName("type")]
        public JsonElement? Type { get; set; }

        [JsonPropertyName("typeName")]
        public string TypeName { get; set; }

        [JsonPropertyName("jumpKind")]
        public string JumpKind { get; set; }

        [JsonPropertyName("year")]
        public JsonElement? Year { get; set; }

        // The observed response may expose a nested preview list.  It is only
        // consulted as a bounded same-domain URL fallback, never as episode
        // metadata or as an invitation to resolve media detail.
        [JsonPropertyName("video")]
        public JsonElement? Video { get; set; }

        [JsonPropertyName("videos")]
        public JsonElement? Videos { get; set; }

        [JsonPropertyName("videoList")]
        public JsonElement? VideoList { get; set; }
    }

    /// <summary>
    /// Existing provider-facing candidate contract.  Its values are already
    /// normalized and safe to pass to the shared match scorer.
    /// </summary>
    public class MgtvSearchItem
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string TypeName { get; set; } = string.Empty;

        public int? Year { get; set; }

        // The suggestion endpoint does not provide a trustworthy episode
        // total.  Preserve the existing shape while leaving it unknown (zero).
        public int VideoCount { get; set; }
    }

    public static class MgtvSuggestionNormalizer
    {
        private const int MaximumCollectionIdLength = 20;
        private const int MaximumTitleLength = 256;
        private const int MaximumSuggestions = 100;
        private const int MaximumNestedUrls = 20;
        private static readonly Regex HtmlTag = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Whitespace = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly Regex CollectionPage = new Regex(
            "^/b/(\\d+)/\\d+(?:[/?.]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HubPage = new Regex(
            "^/h/(\\d+)(?:[/?.]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<MgtvSearchItem> Normalize(MgtvSearchResult response)
        {
            var normalized = new List<MgtvSearchItem>();
            var byId = new Dictionary<string, MgtvSearchItem>(StringComparer.Ordinal);
            foreach (var suggestion in (response?.Data?.Suggest ?? new List<MgtvSearchSuggestion>())
                .Take(MaximumSuggestions))
            {
                if (suggestion == null || IsExplicitNonMedia(suggestion))
                {
                    continue;
                }

                var id = ResolveCollectionId(suggestion);
                var title = FirstPlainTitle(suggestion.Title, suggestion.ShowTitle, suggestion.Hit);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(title))
                {
                    continue;
                }

                // `typeName` is the provider's human-readable category.  The
                // raw `type` field is intentionally not used for filtering:
                // it may be a numeric discriminator.
                var category = ToPlainText(suggestion.TypeName);
                var year = ParseYear(suggestion.Year);
                if (byId.TryGetValue(id, out var existing))
                {
                    // Keep the first endpoint position and title.  Later
                    // duplicates may fill safe metadata only.
                    if (!existing.Year.HasValue && year.HasValue)
                    {
                        existing.Year = year;
                    }
                    if (string.IsNullOrWhiteSpace(existing.TypeName) && !string.IsNullOrWhiteSpace(category))
                    {
                        existing.TypeName = category;
                    }
                    continue;
                }

                var item = new MgtvSearchItem
                {
                    Id = id,
                    Title = title,
                    TypeName = category,
                    Year = year,
                    VideoCount = 0,
                };
                byId.Add(id, item);
                normalized.Add(item);
            }

            return normalized;
        }

        private static string ResolveCollectionId(MgtvSearchSuggestion suggestion)
        {
            var direct = NormalizePositiveId(ScalarValue(suggestion.CollectionId)) ??
                NormalizePositiveId(ScalarValue(suggestion.Id));
            if (!string.IsNullOrEmpty(direct))
            {
                return direct;
            }

            var fromUrl = CollectionIdFromMgtvUrl(suggestion.Url);
            if (!string.IsNullOrEmpty(fromUrl))
            {
                return fromUrl;
            }

            foreach (var nestedUrl in EnumerateNestedUrls(suggestion).Take(MaximumNestedUrls))
            {
                var nestedId = CollectionIdFromMgtvUrl(nestedUrl);
                if (!string.IsNullOrEmpty(nestedId))
                {
                    return nestedId;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateNestedUrls(MgtvSearchSuggestion suggestion)
        {
            foreach (var value in new[] { suggestion.Video, suggestion.Videos, suggestion.VideoList })
            {
                if (!value.HasValue)
                {
                    continue;
                }

                foreach (var url in EnumerateUrls(value.Value, 0))
                {
                    yield return url;
                }
            }
        }

        private static IEnumerable<string> EnumerateUrls(JsonElement value, int depth)
        {
            if (depth > 2)
            {
                yield break;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in value.EnumerateArray())
                {
                    foreach (var url in EnumerateUrls(child, depth + 1))
                    {
                        yield return url;
                    }
                }
                yield break;
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            foreach (var property in value.EnumerateObject())
            {
                if (string.Equals(property.Name, "url", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    yield return property.Value.GetString();
                }
                else if (depth < 2 && (string.Equals(property.Name, "list", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "video", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "videos", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var url in EnumerateUrls(property.Value, depth + 1))
                    {
                        yield return url;
                    }
                }
            }
        }

        private static string CollectionIdFromMgtvUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalizedUrl = value.Trim();
            if (normalizedUrl.StartsWith("//", StringComparison.Ordinal))
            {
                normalizedUrl = "https:" + normalizedUrl;
            }

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
                !(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) || !IsMgtvHost(uri.Host))
            {
                return null;
            }

            var match = CollectionPage.Match(uri.AbsolutePath);
            if (!match.Success)
            {
                match = HubPage.Match(uri.AbsolutePath);
            }
            return match.Success ? NormalizePositiveId(match.Groups[1].Value) : null;
        }

        private static bool IsMgtvHost(string host)
        {
            return string.Equals(host, "mgtv.com", StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith(".mgtv.com", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePositiveId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.Length == 0 || trimmed.Length > MaximumCollectionIdLength ||
                trimmed.Any(character => character < '0' || character > '9'))
            {
                return null;
            }

            trimmed = trimmed.TrimStart('0');
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string ScalarValue(JsonElement? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() :
                value.Value.ValueKind == JsonValueKind.Number ? value.Value.GetRawText() : null;
        }

        private static int? ParseYear(JsonElement? value)
        {
            var text = ScalarValue(value);
            if (text == null || text.Length != 4 || !int.TryParse(text, out var year) ||
                year < 1900 || year > 2100)
            {
                return null;
            }

            return year;
        }

        private static string FirstPlainTitle(params string[] values)
        {
            foreach (var value in values ?? Array.Empty<string>())
            {
                var title = ToPlainText(value);
                if (!string.IsNullOrEmpty(title))
                {
                    return title;
                }
            }

            return string.Empty;
        }

        private static string ToPlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var plain = WebUtility.HtmlDecode(HtmlTag.Replace(value, " "));
            plain = new string(plain.Where(character => !char.IsControl(character)).ToArray());
            plain = Whitespace.Replace(plain, " ").Trim();
            return plain.Length > MaximumTitleLength ? plain.Substring(0, MaximumTitleLength) : plain;
        }

        private static bool IsExplicitNonMedia(MgtvSearchSuggestion suggestion)
        {
            var kind = (suggestion.TypeName ?? suggestion.JumpKind ?? string.Empty).Trim().ToLowerInvariant();
            return kind == "person" || kind == "artist" || kind == "navigation" || kind == "nav" ||
                kind == "keyword" || kind == "topic" || kind == "人物" || kind == "艺人" || kind == "导航";
        }
    }
}
