using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PrmoetheusHelper.Helper;
using Prometheus;

namespace PrometheusRead
{
    public static class Read
    {
        [FunctionName("Read")]
        public static async Task<ReadResponse> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var decompressed = Conversion.DecompressBody(req.Body);

            var readrequest = ReadRequest.Parser.ParseFrom(decompressed);



            return null;
        }
    }
}
