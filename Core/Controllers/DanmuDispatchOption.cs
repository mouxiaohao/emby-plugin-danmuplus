namespace Emby.Plugin.Danmu.Core.Controllers
{
    public class DanmuDispatchOption
    {
     /**
      * 获取对应id的json弹幕信息
      */
     public static string GetJsonById = "GetJsonById";
     
     /**
      * 获取对应id的json弹幕信息
      */
     public static string DownloadXml = "DownloadXml";
        
        /**
         * 获取支持的全部站点信息
         */
        public static string GetAllSupportSite = "GetAllSupportSite";
        
        /**
         * 刷新某个id
         */
        public static string Refresh = "Refresh";
        
        /**
         * 查询某个弹幕
         */
        public static string SearchDanmu = "SearchDanmu";

        /**
         * 预览电视剧/季的智能匹配候选
         */
        public static string MatchPreview = "MatchPreview";

        /**
         * Resolves source episodes for one explicitly selected Episode
         * candidate. This is read-only and never creates a binding.
         */
        public static string GetSelectedCandidatePreview = "GetSelectedCandidatePreview";

        /**
         * Cancels a bounded smart-match search by its browser operation id.
         */
        public static string CancelSearch = "CancelSearch";

        /**
         * 保存选择结果并下载本季弹幕
         */
        public static string BindMatch = "BindMatch";

        /**
         * 保存选择并启动可查询进度的本季下载任务
         */
        public static string StartTrackedDownload = "StartTrackedDownload";

        /**
         * 查询可跟踪下载任务的季/集进度
         */
        public static string GetDownloadProgress = "GetDownloadProgress";

        /**
         * 强制重新下载可跟踪任务中的某一集，并把新结果写回原任务。
         */
        public static string RetryTrackedEpisode = "RetryTrackedEpisode";

        /**
         * 中止所有由智能匹配界面创建的等待中或执行中的下载任务
         */
        public static string StopAllTrackedDownloads = "StopAllTrackedDownloads";
    }
}
