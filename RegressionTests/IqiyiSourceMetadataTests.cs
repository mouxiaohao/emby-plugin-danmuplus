using System;
using System.Runtime.Serialization;
using Emby.Plugin.Danmu.Scraper.Iqiyi;
using Emby.Plugin.Danmu.Scraper.Iqiyi.Entity;

namespace Emby.Plugin.Danmu.RegressionTests
{
    internal static class IqiyiSourceMetadataTests
    {
        internal static void Run()
        {
            var video = new IqiyiHtmlVideoInfo
            {
                VideoName = "Episode 2",
                channelName = "电视剧",
            };
            var album = new IqiyiHtmlAlbumInfo
            {
                albumName = "Legacy Parent Season",
                VideoCount = 12,
            };

            IqiyiApi.ApplyLegacyAlbumInfo(video, album);
            var metadata = Iqiyi.BuildEpisodeSourceMetadata(video);
            var parentProperty = typeof(IqiyiHtmlVideoInfo).GetProperty("ParentTitle");
            if (video.ParentTitle != "Legacy Parent Season" || video.VideoCount != 12 ||
                metadata?.Title != "Legacy Parent Season" || metadata.Category != "电视剧" ||
                Attribute.IsDefined(parentProperty, typeof(IgnoreDataMemberAttribute)))
            {
                throw new InvalidOperationException(
                    "legacy albumInfo must survive the internal DTO round-trip and become exact Episode parent SourceMetadata");
            }
        }
    }
}
