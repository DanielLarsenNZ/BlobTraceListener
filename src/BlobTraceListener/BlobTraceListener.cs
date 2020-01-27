using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
    
[assembly:InternalsVisibleTo("BlobTraceListener.Tests")]

namespace DanielLarsenNZ
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Implementation follows https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.TextWriterTraceListener/src/System/Diagnostics/TextWriterTraceListener.cs </remarks>
    public class BlobTraceListener : TraceListener
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _errors = new ConcurrentQueue<string>();
        private readonly Timer _timer;
        //private Task _sendItems;
        private bool _containerExists;

        private const int MaxItemsToAppendAtATime = 1000;
        private const int MaxErrorMessagesToKeep = 100;

        public BlobTraceListener(string connectionString, string containerName) : this(connectionString, containerName, string.Empty)
        {
        }

        public BlobTraceListener(string connectionString, string containerName, string name) : base(name)
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _timer = new Timer(TimerCallback);
            ScheduleAppendLogs();
        }

        /// <summary>
        /// Gets a value indicating whether trace listener is thread safe. This trace listener is thread safe.
        /// </summary>
        public override bool IsThreadSafe => true;

        internal int ErrorCount { get; private set; }

        internal IEnumerable<string> Errors { get => _errors.ToArray(); }
        
        public override void Write(string message)
        {
            _queue.Enqueue(message);
        }

        public override void WriteLine(string message)
        {
            _queue.Enqueue(message + "\r\n");
        }

        /// <summary>
        /// Synchronously append all buffered logs to Blob Storage.
        /// </summary>
        /// <remarks>
        /// This is a blocking call and should be used sparingly. Buffered logs are automatically Flushed 
        /// periodically and when this TraceListener is disposed.
        /// </remarks>
        public override void Flush()
        {
            AppendLogs().GetAwaiter().GetResult();
            base.Flush();
        }

        private void ScheduleAppendLogs() => _timer.Change(5000, Timeout.Infinite);

        private void TimerCallback(object state) => AppendLogs();

        private async Task CreateContainerIfNotExists()
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        }

        private void BufferError(string errorMessage)
        {
            ErrorCount++;
            _errors.Enqueue(errorMessage);
            while (_errors.Count > MaxErrorMessagesToKeep) _errors.TryDequeue(out _);
        }

        private async Task AppendLogs()
        {
            // Async timer in Scheduler Background Service
            // https://stackoverflow.com/a/53844845

            try
            {
                if (_queue.IsEmpty) return;

                if (!_containerExists)
                {
                    _containerExists = true;
                    await CreateContainerIfNotExists();
                }

                var filename = $"{DateTime.UtcNow.ToString("yyyy/MM/dd/hh")}.log";

                var appendBlobClient = new AppendBlobClient(
                    _connectionString,
                    _containerName,
                    filename);

                await appendBlobClient.CreateIfNotExistsAsync(new BlobHttpHeaders { ContentType = "text/plain" });

                using (var stream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        while (!_queue.IsEmpty)
                        {
                            int i = 0;
                            while (i <= MaxItemsToAppendAtATime)
                            {
                                if (_queue.TryDequeue(out string result)) writer.Write(result);
                                else break;
                                i++;
                            }

                            if (i > 0)
                            {
                                writer.Flush();
                                stream.Seek(0, SeekOrigin.Begin);
                                try
                                {
                                    await appendBlobClient.AppendBlockAsync(stream);
                                }
                                catch (Azure.RequestFailedException ex) when (ex.ErrorCode == "RequestBodyTooLarge")
                                {
                                    // Blob has reached max append blob size ~1.5GB
                                    // Don't append any more. All logs in the buffer are lost
                                    // Blob writing will not resume until a new blob is created
                                    BufferError(ex.Message);
                                    break;
                                }
                            }
                            else
                            {
                                // Don't Trace!
                                Console.WriteLine("BlobTraceListener.SendItems: Could not TryDequeue any items");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BufferError(ex.Message);
                // Can't Trace here!
                Console.WriteLine($"BlobTraceListener.SendItems: {ex.Message}");
            }
            finally
            {
                ScheduleAppendLogs();
            }
        }
    }
}
