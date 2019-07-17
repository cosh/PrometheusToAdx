using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Prometheus;
using Newtonsoft.Json;
using PrmoetheusHelper.Helper;
using System.Collections.Generic;
using System;

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
            var decompressed = Conversion.DecompressBody(req.Body);

            var writerequest = WriteRequest.Parser.ParseFrom(decompressed);

            log.LogMetric("timeserieswrite", writerequest.Timeseries.Count, new Dictionary<String, object>() { { "type", "count" } });

            foreach (var aTimeseries in writerequest.Timeseries)
            {
                await outputEvents.AddAsync(JsonConvert.SerializeObject(aTimeseries));
            }
        }
    }
}
