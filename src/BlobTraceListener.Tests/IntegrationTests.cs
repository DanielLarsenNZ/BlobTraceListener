using BlobTraceListener.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DanielLarsenNZ.Tests
{
    [TestClass]
    [TestCategory("Integration")]
    public class IntegrationTests
    {
        [TestMethod]
        [TestCategory("Very long running")]
        public void SoakTest()
        {
            // Soak test to 100K messages per minute
            const int messagesPerMinute = 100000;

            var config = TestsHelper.GetConfiguration();
            var listener = new BlobTraceListener(
                config["AZURE_STORAGE_CONNECTIONSTRING"],
                config["AZURE_STORAGE_CONTAINER_NAME"]);

            while (true)
            {
                if (listener.ErrorCount > 0)
                {
                    break;
                }
                for (int i = 0; i < messagesPerMinute / 60; i++)
                {
                    listener.WriteLine(i + " The value for one of the HTTP headers is not in the correct format.\nRequestId:f4b01ad3-301e-004c-22ba-d1fd61000000\nTime:2020-01-23T07:00:55.2475901Z\r\nStatus: 400 (The value for one of the HTTP headers is not in the correct format.)\r\n\r\nErrorCode: InvalidHeaderValue\r\n\r\nAdditional Information:\r\nHeaderName: Content-Length\r\nHeaderValue: 0\r\n\r\nHeaders:\r\nServer: Windows-Azure-Blob/1.0,Microsoft-HTTPAPI/2.0\r\nx-ms-request-id: f4b01ad3-301e-004c-22ba-d1fd61000000\r\nx-ms-client-request-id: 964cdd0a-ae29-4722-8b0b-4c6376fa0ff5\r\nx-ms-version: 2019-02-02\r\nx-ms-error-code: InvalidHeaderValue\r\nDate: Thu, 23 Jan 2020 07:00:54 GMT\r\nContent-Length: 321\r\nContent-Type: application/xml\r\n");
                }
                Thread.Sleep(1000);
            }

            //listener.Flush();
            Assert.Fail(listener.Errors.Last());
        }
    }
}
