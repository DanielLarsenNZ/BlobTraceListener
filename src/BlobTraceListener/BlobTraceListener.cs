using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
    
[assembly:InternalsVisibleTo("BlobTraceListener.Tests")]

namespace DanielLarsenNZ
{
    /// <summary>
    /// Writes log messages to Azure Blob Storage.
    /// </summary>
    /// <remarks>Implementation follows https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.TextWriterTraceListener/src/System/Diagnostics/TextWriterTraceListener.cs </remarks>
    public class BlobTraceListener : TraceListener
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _errorsQueue = new ConcurrentQueue<string>();
        private readonly Timer _timer;
        private readonly Timer _errorsTimer;
        private bool _containerExists;
        private bool _errorsContainerExists;
        private readonly BlobTraceListenerOptions _options;

        public BlobTraceListener(string connectionString, string containerName) : this(
            connectionString,
            containerName,
            string.Empty,
            new BlobTraceListenerOptions())
        {
        }

        public BlobTraceListener(
            string connectionString,
            string containerName,
            string name,
            BlobTraceListenerOptions options) : base(name)
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _options = options;
            _timer = new Timer(TimerCallback);
            
            ScheduleAppendLogs();
            
            if (_options.AppendTraceListenerErrors)
            {
                _errorsTimer = new Timer(ErrorsTimerCallback);
                ScheduleAppendErrors();
            }
        }

        /// <summary>
        /// Gets a value indicating whether trace listener is thread safe. This trace listener is thread safe.
        /// </summary>
        public override bool IsThreadSafe => true;

        internal int ErrorCount { get; private set; }

        internal IEnumerable<string> Errors { get => _errorsQueue.ToArray(); }
        
        public override void Write(string message)
        {
            if (_queue.Count > _options.MaxLogMessagesToKeep)
            {
                Debug.WriteLine("Dropping message because Queue is full");
                return;
            }
            _queue.Enqueue(message);
        }

        public override void WriteLine(string message) => Write(message + "\r\n");

        /// <summary>
        /// Synchronously append all buffered logs to Blob Storage.
        /// </summary>
        /// <remarks>
        /// This is a blocking call and should be used sparingly. Buffered logs are automatically Flushed 
        /// periodically and when this TraceListener is disposed.
        /// TraceListener Errors Queue will not be flushed.
        /// </remarks>
        public override void Flush()
        {
            AppendLogs().GetAwaiter().GetResult();
            base.Flush();
        }

        //private void DequeueToMaxItems()
        //{
        //    int dequeueCount = _queue.Count - _options.MaxLogMessagesToKeep;
        //    if (dequeueCount > 0)
        //    {
        //        Debug.WriteLine($"De-queueing {dequeueCount} items because Queue Count is greater than MaxLogMessagesToKeep ({_options.MaxLogMessagesToKeep})");
        //        for (int i = 0; i < dequeueCount; i++)
        //            if (!_queue.TryDequeue(out _)) break;
        //    }
        //}

        private void ScheduleAppendLogs(bool speedUp = false) => _timer.Change(speedUp
                ? Math.Min(1000, _options.BackgroundScheduleTimeoutMs)
                : _options.BackgroundScheduleTimeoutMs,
                Timeout.Infinite);

        private void ScheduleAppendErrors() => _errorsTimer?.Change(_options.BackgroundScheduleTimeoutMs, Timeout.Infinite);

        private void TimerCallback(object state) => AppendLogs();
        private void ErrorsTimerCallback(object state) => AppendErrors();

        private async Task CreateContainerIfNotExists(string containerName)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        }

        private void BufferError(string errorMessage)
        {
            ErrorCount++;
            _errorsQueue.Enqueue(errorMessage);
            while (_errorsQueue.Count > _options.MaxErrorMessagesToKeep) _errorsQueue.TryDequeue(out _);
            Debug.WriteLine(errorMessage);
        }

        private async Task AppendLogs()
        {
            bool itemsLeftInQueue = false;

            try
            {
                if (_queue.IsEmpty) return;

                if (!_containerExists)
                {
                    await CreateContainerIfNotExists(_containerName);
                    _containerExists = true;
                }

                itemsLeftInQueue = await AppendLogs(_queue, _containerName, _options.FilenameFormat);
            }
            catch (Exception ex)
            {
                BufferError(ex.Message);
            }
            finally
            {
                if (!_options.DontReschedule) ScheduleAppendLogs(itemsLeftInQueue);
            }
        }

        private async Task AppendErrors()
        {
            try
            {
                if (_errorsQueue.IsEmpty) return;

                if (!_errorsContainerExists)
                {
                    await CreateContainerIfNotExists(_options.TraceListenerErrorsContainerName);
                    _errorsContainerExists = true;
                }

                await AppendLogs(_errorsQueue, _options.TraceListenerErrorsContainerName, _options.TraceListenerErrorsFilenameFormat);
            }
            catch (Exception ex)
            {
                BufferError(ex.Message);
            }
            finally
            {
                if (!_options.DontReschedule) ScheduleAppendErrors();
            }
        }

        private async Task<bool> AppendLogs(ConcurrentQueue<string> queue, string containerName, string filenameFormat)
        {
            // About Append Blobs: 
            // https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-append-blobs
            // Async timer in Scheduler Background Service:
            // https://stackoverflow.com/a/53844845

            var filename = DateTime.UtcNow.ToString(filenameFormat);
            var appendBlobClient = new AppendBlobClient(_connectionString, containerName, filename);
            await appendBlobClient.CreateIfNotExistsAsync(new BlobHttpHeaders { ContentType = "text/plain" });

            int itemsLeftInQueue = 0;
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    //while (!queue.IsEmpty)
                    var queueCount = queue.Count;
                    {
                        int totalBytes = 0;
                        int itemCount = 0;
                        while (totalBytes < 4000000)
                        {
                            if (queue.TryPeek(out string peekResult))
                            {
                                if (totalBytes + Encoding.Unicode.GetByteCount(peekResult) > 4000000) break;
                                // there is an edge case here...
                            }

                            if (queue.TryDequeue(out string result)) writer.Write(result);
                            else break;

                            itemCount++;
                            totalBytes += Encoding.Unicode.GetByteCount(result);
                        }

                        if (itemCount > 0)
                        {
                            itemsLeftInQueue = queueCount - itemCount;
                            Debug.WriteLine($"BlobTraceListener.AppendLogs: Appending {itemCount} items, {totalBytes} bytes, leaving {itemsLeftInQueue} in the queue");
                            writer.Flush();
                            stream.Seek(0, SeekOrigin.Begin);
                            try
                            {
                                await appendBlobClient.AppendBlockAsync(stream);
                            }
                            catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "RequestBodyTooLarge")
                            {
                                // Blob has reached max append block count (50,000) or max blob size ~195GB
                                // Don't append any more. All logs in the buffer are lost
                                // Blob writing will not resume until a new blob is created
                                BufferError(ex.Message);
                            }
                        }
                        else
                        {
                            // Don't Trace!
                            Debug.WriteLine("BlobTraceListener.AppendLogs: Could not TryDequeue any items");
                        }
                    }
                }
            }

            return itemsLeftInQueue > 0;
        }
    }
}
