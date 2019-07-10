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
using Kusto.Data.Common;
using Kusto.Data;
using Kusto.Data.Net.Client;

namespace PrometheusRead
{
    public static class Read
    {
        private static SnappyCompressor compressor = new SnappyCompressor();
        private static ICslQueryProvider adx;

        private static Object _lock = new object();
        private static Boolean _isInitialized = false;

        [FunctionName("Read")]
        public static byte[] Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var decompressed = Conversion.DecompressBody(req.Body);
            if (decompressed != null)
            {
                Initialize();

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

            return null;
        }

        private static void Initialize()
        {
            lock (_lock)
            {
                if(!_isInitialized)
                {
                    KustoConnectionStringBuilder connection =
                        new KustoConnectionStringBuilder(Environment.GetEnvironmentVariable("kustoUrl", EnvironmentVariableTarget.Process)).WithAadApplicationKeyAuthentication(
                        applicationClientId: Environment.GetEnvironmentVariable("appClientId", EnvironmentVariableTarget.Process),
                        applicationKey: Environment.GetEnvironmentVariable("appClientSecret", EnvironmentVariableTarget.Process),
                        authority: Environment.GetEnvironmentVariable("tenantId", EnvironmentVariableTarget.Process));

                    adx = KustoClientFactory.CreateCslQueryProvider(connection);

                    _isInitialized = true;
                }
            }
        }

        private static ReadResponse CreateResponse(ReadRequest readrequest)
        {
            throw new NotImplementedException();
        }
    }
}
