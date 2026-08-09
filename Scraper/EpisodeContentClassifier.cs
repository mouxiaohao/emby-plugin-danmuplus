using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scraper
{
    public static class EpisodeContentClassifier
    {
        private static readonly Regex ExplicitNonMainRegex = new Regex(
            @"预告|预览|先导片|宣传片|花絮|彩蛋|幕后|制作特辑|番外花絮|抢先看|非正片|直播回顾|(^|[\s\[\]【】()（）:：_-])PV([\s\[\]【】()（）:：_\-0-9]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex EpisodeNumberRegex = new Regex(
            @"^\s*(?:第\s*)?(\d{1,4})(?:\s*(?:集|话|話|期))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsExplicitNonMain(string title)
        {
            return !string.IsNullOrWhiteSpace(title) && ExplicitNonMainRegex.IsMatch(title);
        }

        public static int? TryGetEpisodeNumber(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            var match = EpisodeNumberRegex.Match(title);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var number) && number > 0)
            {
                return number;
            }

            return null;
        }
    }
}
