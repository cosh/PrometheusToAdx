using Kusto.Data;
using Kusto.Ingest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrmoetheusHelper.Helper;
using Prometheus;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace KustoWriterApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KustoIngestController : ControllerBase
    {
        private readonly ILogger<KustoIngestController> _logger;
        private readonly SettingsKusto _settings;
        private static readonly ConcurrentQueue<Prometheus.TimeSeries> _timeseriesQueue = new ConcurrentQueue<Prometheus.TimeSeries>();
        private static readonly ConcurrentQueue<string> _filesToIngest = new ConcurrentQueue<string>();
        private const int MaxQueueSize = 1000000;
        private const int MinutesToFlush = 5;
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public KustoIngestController(ILogger<KustoIngestController> logger, IOptions<SettingsKusto> options)
        {
            _logger = logger;
            _settings = options.Value;
            StartBackgroundQueueFlusher();
            StartBackgroundFileIngestor();
        }

        [HttpPost(Name = "IngestIntoKusto")]
        public async Task<IActionResult> PostTelemetry()
        {
            using (var memoryStream = new MemoryStream())
            {
                await Request.Body.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                var decompressed = Conversion.DecompressBody(memoryStream);

                var writerequest = WriteRequest.Parser.ParseFrom(decompressed);

                foreach (var aTimeseries in writerequest.Timeseries)
                {
                    _timeseriesQueue.Enqueue(aTimeseries);

                    if (_timeseriesQueue.Count > MaxQueueSize)
                    {
                        await WriteQueueToFileAsync();
                    }
                }
            }

            return Ok();
        }

        private void StartBackgroundQueueFlusher()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(MinutesToFlush), _cancellationTokenSource.Token);
                    await WriteQueueToFileAsync();
                }
            }, _cancellationTokenSource.Token);
        }

        private void StartBackgroundFileIngestor()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    while (_filesToIngest.TryDequeue(out var filePath))
                    {
                        await IngestFileIntoKustoAsync(filePath);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token); // Check every 30 seconds
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task WriteQueueToFileAsync()
        {
            var filePath = Path.Combine("path_to_your_directory", $"timeseries_{DateTime.UtcNow:yyyyMMddHHmmss}.json");

            var timeseriesList = new List<Prometheus.TimeSeries>();
            while (_timeseriesQueue.TryDequeue(out var timeseries))
            {
                timeseriesList.Add(timeseries);
            }

            var json = JsonSerializer.Serialize(timeseriesList);
            await System.IO.File.WriteAllTextAsync(filePath, json);
            _filesToIngest.Enqueue(filePath);
        }

        private async Task IngestFileIntoKustoAsync(string filePath)
        {
            // Implement your Kusto ingestion logic here
            // For example, you can use the Kusto Data SDK to ingest the file into a Kusto database
            // This is a placeholder for the actual ingestion logic
            _logger.LogInformation($"Ingesting file {filePath} into Kusto...");

            int retries = 0;

            var kustoConnectionStringBuilderEngine =
                new KustoConnectionStringBuilder($"https://{_settings.ClusterName}.kusto.windows.net").WithAadApplicationKeyAuthentication(
                    applicationClientId: _settings.ClientId,
                    applicationKey: _settings.ClientSecret,
                    authority: _settings.TenantId);

            using (IKustoIngestClient client = KustoIngestFactory.CreateDirectIngestClient(kustoConnectionStringBuilderEngine))
            {
                var kustoIngestionProperties = new KustoIngestionProperties(databaseName: _settings.DbName, tableName: _settings.TableName);
                kustoIngestionProperties.SetAppropriateMappingReference(_settings.MappingName, Kusto.Data.Common.DataSourceFormat.json);
                kustoIngestionProperties.Format = Kusto.Data.Common.DataSourceFormat.json;

                while (retries < _settings.MaxRetries)
                {
                    try
                    {
                        client.IngestFromStorage(filePath, kustoIngestionProperties);
                        return;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Could not ingest {filePath} into table {_settings.TableName}.");
                        retries++;
                        Thread.Sleep(_settings.MsBetweenRetries);
                    }
                }

                _logger.LogInformation($"File {filePath} ingested into Kusto.");
            }
        }
    }
}
