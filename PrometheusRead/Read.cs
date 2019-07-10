using System;
using System.Linq;
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
using Snappy.Sharp;

namespace PrometheusRead
{
    public static class Read
    {
        private static SnappyCompressor compressor = new SnappyCompressor();

        [FunctionName("Read")]
        public static byte[] Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var decompressed = Conversion.DecompressBody(req.Body);
            var readrequest = ReadRequest.Parser.ParseFrom(decompressed);

            ReadResponse response = CreateResponse(readrequest);

            MemoryStream ms = new MemoryStream();
            response.WriteTo(new Google.Protobuf.CodedOutputStream(ms));

            var resultUncompressed = ms.ToArray();

            //should be at least the size of the uncompressed one
            byte[] resultCompressed = new byte[resultUncompressed.Length];

            var compressedSize = compressor.Compress(resultUncompressed, 0, resultUncompressed.Length, resultCompressed);

            byte[] resultCompressedShortened = resultCompressed.Take(compressedSize).ToArray();

            return resultCompressedShortened;
        }

        private static ReadResponse CreateResponse(ReadRequest readrequest)
        {
            throw new NotImplementedException();
        }
    }
}
