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
        public void BasicTest()
        {
            var config = TestsHelper.GetConfiguration();
            var listener = new BlobTraceListener(
                config["AZURE_STORAGE_CONNECTIONSTRING"],
                config["AZURE_STORAGE_CONTAINER_NAME"]);

            for (int i = 0; i < 1000000; i++)
            {
                listener.WriteLine($"Hello world! {i}");
            }

            listener.Flush();

            if (listener.ErrorCount > 0) Assert.Fail(listener.Errors.Last());
        }
    }
}
