using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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
        private Task _sendItems;

        private const int MaxItemsToAppendAtATime = 500;

        public BlobTraceListener(string connectionString, string containerName)
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _timer = new Timer(TimerCallback, null, 5000, 5000);
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

        private void TimerCallback(object state)
        {
            if (_sendItems != null && _sendItems.Status == TaskStatus.Running)
            {
                // Don't Trace!
                Console.WriteLine("BlobTraceListener: SendItems() not called as Task status = Running");
            }

            _sendItems = SendItems();
        }

        private async Task SendItems()
        {
            // Async timer in Scheduler Background Service
            // https://stackoverflow.com/a/53844845

            try
            {
                if (_queue.IsEmpty) return;

                var now = DateTime.UtcNow;

                var appendBlobClient = new AppendBlobClient(
                    _connectionString, 
                    _containerName, 
                    now.ToString("yyyyMMddhh"));

                await appendBlobClient.CreateIfNotExistsAsync();

                using (var stream = new MemoryStream())
                {
                    using (var writer = new StreamWriter(new MemoryStream()))
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
                            stream.Seek(0, SeekOrigin.Begin);
                            await appendBlobClient.AppendBlockAsync(stream);
                        }
                        else
                        {
                            // Don't Trace!
                            Console.WriteLine("Could not TryDequeue any items");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Can't Trace here!
                Console.WriteLine($"BlobTraceListener: {ex.Message}");
            }
        }
    }
}