using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DanielLarsenNZ
{
    public sealed class BlobTraceListener : TraceListener
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private readonly Timer _timer;
        //private Task _sendItems;
        private bool _containerExists;

        private const int MaxItemsToAppendAtATime = 500;

        public BlobTraceListener(string connectionString, string containerName)
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _timer = new Timer(TimerCallback);
            ScheduleAppendLogsLater();
        }

        /// <summary>
        /// Gets a value indicating whether trace listener is thread safe. This trace listener is thread safe.
        /// </summary>
        public override bool IsThreadSafe => true;

        public override void Write(string message)
        {
            _queue.Enqueue(message);
        }

        public override void WriteLine(string message)
        {
            _queue.Enqueue(message + "\n");
        }

        public override void Flush()
        {
            ScheduleAppendLogsNow();
            base.Flush();
        }

        private void ScheduleAppendLogsNow()
            => _timer.Change(10, Timeout.Infinite);

        private void ScheduleAppendLogsLater()
            => _timer.Change(5000, Timeout.Infinite);

        private void TimerCallback(object state) => AppendLogs();

        private async Task CreateContainerIfNotExists()
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
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
                        int i = 0;
                        while (!_queue.IsEmpty)
                        {
                            if (_queue.TryDequeue(out string result)) writer.Write(result);
                            else break;
                            i++;
                            if (i > MaxItemsToAppendAtATime) break;
                        }

                        if (i > 0)
                        {
                            writer.Flush();
                            stream.Seek(0, SeekOrigin.Begin);
                            await appendBlobClient.AppendBlockAsync(stream);
                        }
                        else
                        {
                            // Don't Trace!
                            Console.WriteLine("BlobTraceListener.SendItems: Could not TryDequeue any items");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Can't Trace here!
                Console.WriteLine($"BlobTraceListener.SendItems: {ex.Message}");
            }
            finally
            {
                ScheduleAppendLogsLater();
            }
        }
    }
}