using System;

namespace Emby.Plugin.Danmu.Core
{
    public class DanmuDownloadErrorException : Exception
    {
        public DanmuDownloadErrorException(string message) : base(message)
        {
        }

        public DanmuDownloadErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Don't display call stack as it's irrelevant
        /// </summary>
        public override string StackTrace
        {
            get { return ""; }
        }
    }
}
