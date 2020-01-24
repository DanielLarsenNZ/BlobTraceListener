using Microsoft.Extensions.Configuration;
using System.IO;

namespace BlobTraceListener.Tests
{
    internal static class TestsHelper
    {
        public static IConfiguration GetConfiguration()
        {
            // load configuration from appsettings.json file
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
        }
    }
}
