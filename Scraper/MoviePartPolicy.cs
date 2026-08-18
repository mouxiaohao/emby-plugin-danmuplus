using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Scraper
{
    /// <summary>Provider-neutral final safety filter for Movie leaf choices.</summary>
    public static class MoviePartPolicy
    {
        public const int MaximumUsableParts = 64;

        private static readonly Regex ExplicitNonMainTitle = new Regex(
            @"预告|預告|预览|預覽|花絮|幕后|幕後|特别篇|特別篇|制作特辑|製作特輯|片段|访谈|訪談|采访|採訪|彩蛋|" +
            @"(^|[^a-z])(trailer|preview|behind[\s-]*the[\s-]*scenes|making[\s-]*of|special|clip|interview|bonus)([^a-z]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<ScraperMoviePart> GetUsableParts(IEnumerable<ScraperMoviePart> source)
        {
            return (source ?? Enumerable.Empty<ScraperMoviePart>())
                .Where(part => part != null && part.IsDownloadable && !part.IsExplicitNonMain &&
                    !string.IsNullOrWhiteSpace(part.Id) && !IsExplicitNonMain(part.Title))
                .GroupBy(part => part.Id.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(MaximumUsableParts)
                .ToList();
        }

        public static bool IsExplicitNonMain(string title)
        {
            return EpisodeContentClassifier.IsExplicitNonMain(title) ||
                   (!string.IsNullOrWhiteSpace(title) && ExplicitNonMainTitle.IsMatch(title));
        }
    }
}
