using System;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuDownloadPolicy
    {
        public static bool ShouldSkipExistingDanmu(
            bool forceRefresh,
            bool fileExists,
            DateTime lastWriteTime,
            DateTime now)
        {
            return !forceRefresh && fileExists && (now - lastWriteTime).TotalDays < 7;
        }
    }
}
