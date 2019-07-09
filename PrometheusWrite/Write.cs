using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Snappy;
using System.IO.Compression;

namespace PrometheusWrite
{
    public static class Write
    {
        [FunctionName("Write")]
        [return: EventHub("outputEventHubMessage", Connection = "EventHubConnectionAppSetting")]
        public static String Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var decompressed = DecompressBody(req.Body);

            return "testteerst";
        }

        private static String DecompressBody(Stream body)
        {
            MemoryStream ms = new MemoryStream();
            body.CopyTo(ms);

            var decompressor = new Snappy.Sharp.SnappyDecompressor();
            var source = ms.ToArray();

            var decompressed = decompressor.Decompress(source, 0, source.Length);

            return System.Text.Encoding.UTF8.GetString(decompressed);
        }
    }
}
