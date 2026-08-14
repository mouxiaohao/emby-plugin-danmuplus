using System;
using Emby.Plugin.Danmu.Core;
using Emby.Plugin.Danmu.Scraper.Entity;
using Emby.Plugin.Danmu.Scraper.Bilibili;
using BilibiliScraper = Emby.Plugin.Danmu.Scraper.Bilibili.Bilibili;

namespace Emby.Plugin.Danmu.Scraper
{
    public static class DanmuMovieMatchHelper
    {
        public static string ResolveEpisodeLookupId(string providerId, ScraperMedia media)
        {
            if (media == null)
            {
                return string.Empty;
            }

            var isBilibili = string.Equals(
                providerId, BilibiliScraper.ScraperProviderId, System.StringComparison.OrdinalIgnoreCase);
            var value = !string.IsNullOrWhiteSpace(media.SelectedMoviePartId)
                ? media.SelectedMoviePartId
                : isBilibili
                ? BilibiliPgcIdPolicy.ResolveMovieEpisodeId(media)
                : media.Id;
            if (!isBilibili && string.IsNullOrWhiteSpace(value) && media.Episodes.Count > 0)
            {
                value = media.Episodes[0].CommentId;
            }
            return value ?? string.Empty;
        }

        /// <summary>
        /// Final download decision after the provider leaf lookup. An explicitly
        /// selected server-verified leaf is fail-closed and can never degrade to
        /// the parent search result's CommentId/default part.
        /// </summary>
        public static ScraperEpisode ResolveEpisodeForDownload(
            ScraperMedia media,
            ScraperEpisode resolvedEpisode,
            string lookupId,
            Exception lookupFailure = null)
        {
            if (media == null) throw new ArgumentNullException(nameof(media));

            var hasExplicitPart = !string.IsNullOrWhiteSpace(media.SelectedMoviePartId);
            if (hasExplicitPart)
            {
                if (lookupFailure != null)
                {
                    throw new DanmuDownloadErrorException(
                        "已选择的电影正片版本无法重新验证", lookupFailure);
                }
                if (resolvedEpisode == null || string.IsNullOrWhiteSpace(resolvedEpisode.CommentId))
                {
                    throw new DanmuDownloadErrorException("已选择的电影正片版本已失效或没有弹幕 ID");
                }
                return resolvedEpisode;
            }

            if (resolvedEpisode != null && !string.IsNullOrWhiteSpace(resolvedEpisode.CommentId))
            {
                return resolvedEpisode;
            }
            if (!string.IsNullOrWhiteSpace(media.CommentId))
            {
                return new ScraperEpisode
                {
                    Id = string.IsNullOrWhiteSpace(media.Id) ? lookupId : media.Id,
                    CommentId = media.CommentId,
                };
            }
            if (lookupFailure != null)
            {
                throw new DanmuDownloadErrorException("弹幕来源查询电影播放条目失败", lookupFailure);
            }
            throw new DanmuDownloadErrorException("弹幕来源没有返回电影弹幕 ID");
        }
    }
}
