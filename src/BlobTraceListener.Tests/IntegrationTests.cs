using BlobTraceListener.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
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

            for (int i = 0; i < 1000; i++)
            {
                listener.WriteLine($"Hello world! {i}");
            }

            listener.Flush();
        }

        //[TestMethod]
        //public void TextWriterTest()
        //{
        //    Stream myFile = File.Create("c:\\Users\\dalars\\Downloads\\test.log");

        //    /* Create a new text writer using the output stream, and add it to
        //     * the trace listeners. */
        //    TextWriterTraceListener myTextListener = new TextWriterTraceListener(myFile);
        //    Trace.Listeners.Add(myTextListener);

        //    // Write output to the file.
        //    Trace.Write("Test output ");

        //    // Flush the output.
        //    Trace.Flush();
        //}
    }
}
