using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Prometheus;
using Newtonsoft.Json;

namespace PrometheusWrite
{
    public static class Write
    {
        [FunctionName("Write")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [EventHub("dest", Connection = "EventHubConnectionAppSetting")]IAsyncCollector<string> outputEvents,
            ILogger log)
        {
            var decompressed = DecompressBody(req.Body);

            var writerequest = WriteRequest.Parser.ParseFrom(decompressed);

            foreach (var aTimeseries in writerequest.Timeseries)
            {
                await outputEvents.AddAsync(JsonConvert.SerializeObject(aTimeseries));
            }
        }

        private static byte[] DecompressBody(Stream body)
        {
            MemoryStream ms = new MemoryStream();
            body.CopyTo(ms);

            var decompressor = new Snappy.Sharp.SnappyDecompressor();
            var source = ms.ToArray();

            var decompressed = decompressor.Decompress(source, 0, source.Length);

            return decompressed;
        }
    }
}
