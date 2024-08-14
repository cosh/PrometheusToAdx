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
using PrometheusHelper.Helper;
using Prometheus;
using Snappy.Sharp;
using Kusto.Data.Common;
using Kusto.Data;
using Kusto.Data.Net.Client;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Newtonsoft.Json.Linq;

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

                log.LogMetric("querycount", readrequest.Queries.Count, new Dictionary<String, object>() { { "type", "count" } });

                ReadResponse response = CreateResponse(readrequest, log);

                log.LogMetric("result", response.Results.Count, new Dictionary<String, object>() { { "type", "count" } });
                log.LogMetric("timeseriesread", response.Results.Select(_ => _.Timeseries.Count).Sum(__ => __), new Dictionary<String, object>() { { "type", "count" } });

                MemoryStream ms = new MemoryStream();
                Google.Protobuf.CodedOutputStream output = new Google.Protobuf.CodedOutputStream(ms);
                response.WriteTo(output);

                output.Flush();

                var resultUncompressed = ms.ToArray();

                if (resultUncompressed.Length > 0)
                {
                    //should be at least the size of the uncompressed one
                    byte[] resultCompressed = new byte[resultUncompressed.Length * 2];

                    var compressedSize = compressor.Compress(resultUncompressed, 0, resultUncompressed.Length, resultCompressed);

                    Array.Resize(ref resultCompressed, compressedSize);

                    return resultCompressed;
                }
                else
                    return resultUncompressed;
            }

            return null;
        }

        private static void Initialize()
        {
            lock (_lock)
            {
                if (!_isInitialized)
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

        private static ReadResponse CreateResponse(ReadRequest readrequest, ILogger log)
        {
            ReadResponse result = new ReadResponse();

            List<Task<IDataReader>> tasklist = new List<Task<IDataReader>>();

            foreach (var aQuery in readrequest.Queries)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"SearchTimeseries({aQuery.StartTimestampMs}, {aQuery.EndTimestampMs})");
                sb.AppendLine("| where " + String.Join(" and ", aQuery.Matchers.Select(aMatch => $"({GenerateValueExpression(aMatch.Name, aMatch.Type, aMatch.Value)})")));
                sb.AppendLine("| project timeseries");

                log.LogInformation($"KQL: {sb.ToString()}");

                tasklist.Add(adx.ExecuteQueryAsync(databaseName: "sensordata", query: sb.ToString(), null));
            }

            Task.WaitAll(tasklist.ToArray());

            result.Results.AddRange(tasklist.Select(aTask => CreateQueryResult(aTask.Result)));

            return result;
        }

        private static QueryResult CreateQueryResult(IDataReader reader)
        {
            var result = new QueryResult();

            while (reader.Read())
            {
                var timeSeriesObject = (JObject)reader.GetValue(0);

                result.Timeseries.Add(JsonConvert.DeserializeObject<TimeSeries>(timeSeriesObject.ToString()));
            }

            reader.Close();

            return result;
        }

        private static String GenerateValueExpression(string name, LabelMatcher.Types.Type type, string value)
        {
            switch (type)
            {
                case LabelMatcher.Types.Type.Eq:
                    return $"tostring(label.{name}) == \"{value}\"";
                case LabelMatcher.Types.Type.Neq:
                    return $"tostring(label.{name}) != \"{value}\"";
                case LabelMatcher.Types.Type.Re:
                    return $"tostring(label.{name}) matches regex \"{value}\"";
                case LabelMatcher.Types.Type.Nre:
                    //operator missing
                    return $"tostring(label.{name}) !contains \"{value}\"";
                default:
                    break;
            }

            return String.Empty;
        }
    }
}
