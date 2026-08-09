using System;
using Emby.Plugin.Danmu.Scraper.Entity;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuDownloadContent
    {
        public static bool HasUsableItems(ScraperDanmaku danmaku)
        {
            return danmaku?.Items != null && danmaku.Items.Count > 0;
        }

        public static byte[] Serialize(ScraperDanmaku danmaku)
        {
            if (!HasUsableItems(danmaku))
            {
                throw new DanmuDownloadErrorException("弹幕来源未返回有效弹幕");
            }

            try
            {
                var bytes = danmaku.ToXml();
                if (bytes == null || bytes.Length == 0)
                {
                    throw new DanmuDownloadErrorException("弹幕 XML 序列化结果为空");
                }

                return bytes;
            }
            catch (DanmuDownloadErrorException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DanmuDownloadErrorException("弹幕 XML 序列化失败", ex);
            }
        }
    }
}
