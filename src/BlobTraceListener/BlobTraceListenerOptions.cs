namespace DanielLarsenNZ
{
    /// <summary>
    /// Options for <seealso cref="BlobTraceListener"/>
    /// </summary>
    public class BlobTraceListenerOptions
    {
        private const int DefaultMaxLogMessagesToQueue = 10000;
        private const string DefaultFileNameFormat = "yyyy/MM/dd/HH\\.\\l\\o\\g";
        private const int DefaultBackgroundScheduleTimeoutMs = 4000;
        private const int DefaultMaxTraceListenerErrorMessagesToKeep = 20;

        public BlobTraceListenerOptions()
        {
            MaxLogMessagesToQueue = DefaultMaxLogMessagesToQueue;
            FilenameFormat = DefaultFileNameFormat;
            BackgroundScheduleTimeoutMs = DefaultBackgroundScheduleTimeoutMs;
            MaxTraceListenerErrorMessagesToKeep = DefaultMaxTraceListenerErrorMessagesToKeep;
        }

        /// <summary>
        /// The maximum number of log messages to queue. Any messages that attempt to be written when 
        /// queue is full will be dropped. Default is 10,000.
        /// </summary>
        public int MaxLogMessagesToQueue { get; set; }

        /// <summary>
        /// The format string passed to `DateTime.UtcNow.ToString()` to derive the blob filename. Default 
        /// is "yyyy/MM/dd/HH\\.\\l\\o\\g".
        /// </summary>
        public string FilenameFormat { get; set; }

        /// <summary>
        /// The number of milliseconds to wait before starting a background task to append logs to Blob 
        /// storage. Default is 4,000 milliseconds.
        /// </summary>
        /// <remarks>If all queued messages cannot be appended in one blob (~4MB) then the timeout is 
        /// shortened to 100 ms until the queue is cleared.</remarks>
        public int BackgroundScheduleTimeoutMs { get; set; }

        /// <summary>
        /// The maximum number of errors thrown by the listener to keep in memory. Default is 20. Errors 
        /// beyond this number will be dequeued FIFO.
        /// </summary>
        internal int MaxTraceListenerErrorMessagesToKeep { get; set; }

        /// <summary>
        /// Prevents rescheduling of the Time. For testing purposes only.
        /// </summary>
        internal bool DontReschedule { get; set; }
    }
}
