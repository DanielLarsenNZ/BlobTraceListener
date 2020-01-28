using BlobTraceListener.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading;

namespace DanielLarsenNZ.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class IntegrationTests
    {
        [TestMethod]
        public void BasicUsage()
        {
            var config = TestsHelper.GetConfiguration();
            var listener = new BlobTraceListener(
                config["AZURE_STORAGE_CONNECTIONSTRING"],
                config["AZURE_STORAGE_CONTAINER_NAME"]);

            listener.WriteLine("Hello world!");
            listener.Flush();
        }

        [TestMethod]
        public void OptionsUsage()
        {
            var config = TestsHelper.GetConfiguration();
            var listener = new BlobTraceListener(
                config["AZURE_STORAGE_CONNECTIONSTRING"],
                config["AZURE_STORAGE_CONTAINER_NAME"],
                string.Empty,
                new BlobTraceListenerOptions
                {
                    BackgroundScheduleTimeoutMs = 4000,
                    FilenameFormat = "yyyy/MM/dd/HH\\.\\l\\o\\g",
                    MaxLogMessagesToQueue = 20000
                });

            listener.WriteLine("Hello world!");
            listener.Flush();
        }


        [TestMethod]
        [TestCategory("Very long running")]
        public void SoakTest()
        {
            // Soak test at ~100K messages per minute for an hour
            const int messagesPerMinute = 100000;

            var config = TestsHelper.GetConfiguration();
            var listener = new BlobTraceListener(
                config["AZURE_STORAGE_CONNECTIONSTRING"],
                config["AZURE_STORAGE_CONTAINER_NAME"]);

            for (int i = 0; i < 3600; i++)
            {
                if (listener.ErrorCount > 0)
                {
                    break;
                }
                for (int j = 0; j < messagesPerMinute / 60; j++)
                {
                    listener.WriteLine($"{i} - {j} - The value for one of the HTTP headers is not in the correct format.\nRequestId:f4b01ad3-301e-004c-22ba-\nTime:2020-01-23T07:00:55.2475901Z\r\nStatus: 400 (The value for one of the HTTP headers is not in the correct format.)\r\n\r\nErrorCode: InvalidHeaderValue\r\n\r\nAdditional Information:\r\nHeaderName: Content-Length\r\nHeaderValue: 0\r\n\r\nHeaders:\r\nServer: Windows-Azure-Blob/1.0,Microsoft-HTTPAPI/2.0\r\nx-ms-request-id: f4b01ad3-301e-004c-22ba-\r\nx-ms-client-request-id: 964cdd0a-ae29-4722-8b0b-\r\nx-ms-version: 2019-02-02\r\nx-ms-error-code: InvalidHeaderValue\r\nDate: Thu, 23 Jan 2020 07:00:54 GMT\r\nContent-Length: 321\r\nContent-Type: application/xml\r\n");
                }
                Thread.Sleep(1000);
            }

            //listener.Flush();
            Assert.Fail(listener.Errors.Last());
        }
    }
}
