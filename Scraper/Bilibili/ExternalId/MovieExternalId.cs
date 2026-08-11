using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.ExternalId
{
    public class MovieExternalId : IExternalId
    {
        public string Name => Bilibili.ScraperProviderName;
        public string Key => Bilibili.ScraperProviderId;
        public string UrlFormatString => "#";
        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
