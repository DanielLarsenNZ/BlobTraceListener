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
        private bool _containerExists;
        private readonly BlobTraceListenerOptions _options;
        private string _lastFilename;

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
        }

        /// <summary>
        /// Gets a value indicating whether trace listener is thread safe. This trace listener is thread safe.
        /// </summary>
        public override bool IsThreadSafe => true;

        /// <summary>
        /// A count of errors thrown by this TraceListener. 
        /// </summary>
        internal uint ErrorCount { get; private set; }

        /// <summary>
        /// An in memory queue of errors thrown by this TraceListener.
        /// </summary>
        internal IEnumerable<string> Errors { get => _errorsQueue.ToArray(); }
        
        /// <summary>
        /// Write a message to this TraceListener.
        /// </summary>
        /// <param name="message"></param>
        public override void Write(string message)
        {
            // short-circuit to drop messages when queue is full.
            if (_queue.Count > _options.MaxLogMessagesToQueue)
            {
                Debug.WriteLine("Dropping message because Queue is full");
                return;
            }

            _queue.Enqueue(message);
        }

        /// <summary>
        /// Writes a message to this TraceListener.
        /// </summary>
        /// <param name="message"></param>
        public override void WriteLine(string message) => Write(message + "\r\n");

        /// <summary>
        /// Synchronously append all buffered logs to Blob Storage.
        /// </summary>
        /// <remarks>
        /// This is a blocking call and should be used sparingly. Buffered logs are automatically Flushed 
        /// periodically and when this TraceListener is disposed.
        /// </remarks>
        public override void Flush()
        {
            while (AppendLogs(_queue, _containerName, _options.FilenameFormat).GetAwaiter().GetResult());
            base.Flush();
        }

        /// <summary>
        /// Starts a new timeout for the background task. This is <seealso cref="BlobTraceListenerOptions.BackgroundScheduleTimeoutMs"/>,
        /// or 200 if speedUp = true.
        /// </summary>
        /// <param name="speedUp">When true, timeout is shortened to 200 ms</param>
        private void ScheduleAppendLogs(bool speedUp = false) => 
            _timer.Change(speedUp
                ? Math.Min(200, _options.BackgroundScheduleTimeoutMs)
                : _options.BackgroundScheduleTimeoutMs,
                Timeout.Infinite);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        // This is "fire and forget" as per https://stackoverflow.com/a/53844845
        private void TimerCallback(object state) => AppendLogs();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        private async Task CreateContainerIfNotExists(string containerName)
        {
            if (!_containerExists)
            {
                var blobServiceClient = new BlobServiceClient(_connectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                _containerExists = true;
            }
        }

        /// <summary>
        /// Write error messages to an in-memory queue.
        /// </summary>
        /// <param name="errorMessage"></param>
        private void BufferError(string errorMessage)
        {
            ErrorCount++;
            _errorsQueue.Enqueue(errorMessage);
            Debug.WriteLine(errorMessage);
            while (_errorsQueue.Count > _options.MaxTraceListenerErrorMessagesToKeep) _errorsQueue.TryDequeue(out _);
        }

        private async Task AppendLogs()
        {
            if (_queue.IsEmpty) return;
            
            bool itemsLeftInQueue = false;

            try
            {
                await CreateContainerIfNotExists(_containerName);
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

        private async Task<bool> AppendLogs(ConcurrentQueue<string> queue, string containerName, string filenameFormat)
        {
            // About Append Blobs: 
            // https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs#about-append-blobs
            // Async timer in Scheduler Background Service:
            // https://stackoverflow.com/a/53844845

            var filename = DateTime.UtcNow.ToString(filenameFormat);
            var appendBlobClient = await GetAppendBlobClient(_connectionString, containerName, filename);

            int itemsLeftInQueue = 0;
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    var queueCount = queue.Count;
                    int totalBytes = 0;
                    int itemCount = 0;

                    // Append one block of messages up to a maximum of 4MB
                    while (totalBytes < appendBlobClient.AppendBlobMaxAppendBlockBytes)
                    {
                        // peek to see if greater than max append block size
                        if (queue.TryPeek(out string peekResult))
                        {
                            if (totalBytes + Encoding.Unicode.GetByteCount(peekResult) > appendBlobClient.AppendBlobMaxAppendBlockBytes) break;
                            // there is an edge case here during Flush...
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

            return itemsLeftInQueue > 0;
        }

        private async Task<AppendBlobClient> GetAppendBlobClient(
            string connectionString,
            string containerName,
            string filename)
        {
            // AppendBlobClient HTTP connection is cached under the hood
            var appendBlobClient = new AppendBlobClient(connectionString, containerName, filename);
            await CreateBlobIfNotExists(appendBlobClient, filename);
            return appendBlobClient;
        }

        private async Task CreateBlobIfNotExists(AppendBlobClient appendBlobClient, string filename)
        {
            if (filename == _lastFilename) return;

            await appendBlobClient.CreateIfNotExistsAsync(new BlobHttpHeaders { ContentType = "text/plain" });
            _lastFilename = filename;
        }
    }
}
