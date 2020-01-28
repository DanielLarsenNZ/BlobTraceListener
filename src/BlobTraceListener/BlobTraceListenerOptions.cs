using System;
using System.Collections.Generic;
using System.Text;

namespace DanielLarsenNZ
{
    public class BlobTraceListenerOptions
    {
        private const int DefaultMaxErrorMessagesToQueue = 100;
        private const int DefaultMaxLogMessagesToQueue = 10000;
        private const string DefaultFileNameFormat = "yyyy/MM/dd/HH\\.\\l\\o\\g";
        private const string DefaultErrorsFileNameFormat = "yyyy/MM/dd/HH\\.\\e\\r\\r\\o\\r\\s\\.\\l\\o\\g";
        private const int DefaultBackgroundScheduleTimeoutMs = 5000;

        public BlobTraceListenerOptions()
        {
            MaxLogMessagesToKeep = DefaultMaxLogMessagesToQueue;
            MaxErrorMessagesToKeep = DefaultMaxErrorMessagesToQueue;
            FilenameFormat = DefaultFileNameFormat;
            TraceListenerErrorsFilenameFormat = DefaultErrorsFileNameFormat;
            BackgroundScheduleTimeoutMs = DefaultBackgroundScheduleTimeoutMs;
        }

        public int MaxItemsToAppendAtATime { get; set; }

        public int MaxErrorMessagesToKeep { get; set; }

        public int MaxLogMessagesToKeep { get; set; }

        /// <summary>
        /// When true, any errors thrown by the Trace Listener will attempt to be written to an errors 
        /// log. By default the same container name will be used, but this can be overridden by <seealso cref="TraceListenerErrorsContainerName"/>
        /// </summary>
        public bool AppendTraceListenerErrors { get; set; }

        public string TraceListenerErrorsContainerName { get; set; }

        public string FilenameFormat { get; set; }
        
        public string TraceListenerErrorsFilenameFormat { get; set; }

        public int BackgroundScheduleTimeoutMs { get; set; }

        /// <summary>
        /// Prevents rescheduling of the Time. For testing purposes only.
        /// </summary>
        internal bool DontReschedule { get; set; }
    }
}
