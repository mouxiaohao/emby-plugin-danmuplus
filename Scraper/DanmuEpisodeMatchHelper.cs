namespace Emby.Plugin.Danmu.Scraper
{
    public static class DanmuEpisodeMatchHelper
    {
        public static int? SuggestSourceEpisodeNumber(int? localEpisodeNumber, int availableEpisodes)
        {
            var number = localEpisodeNumber ?? 0;
            return IsValidSourceEpisodeNumber(number, availableEpisodes) ? number : (int?)null;
        }

        public static bool IsValidSourceEpisodeNumber(int sourceEpisodeNumber, int availableEpisodes)
        {
            return sourceEpisodeNumber > 0 && sourceEpisodeNumber <= availableEpisodes;
        }
    }
}
