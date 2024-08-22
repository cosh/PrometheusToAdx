using Kusto.Data;
using Kusto.Ingest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PrometheusHelper.Helper;
using Prometheus;
using System.Collections.Concurrent;
using System.Text.Json;
using System.IO;
using System.Timers;


namespace KustoWriterApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class KustoIngestController : ControllerBase
    {
        private readonly ILogger<KustoIngestController> _logger;
        private readonly KustoConnectionStringBuilder _kustoConnectionStringBuilderEngine;
        private readonly KustoIngestionProperties _kustoIngestionProperties;
        private readonly SettingsKusto _settings;
        private static readonly ConcurrentQueue<Prometheus.TimeSeries> _timeseriesQueue = new ConcurrentQueue<Prometheus.TimeSeries>();
        private static readonly ConcurrentDictionary<string, System.Guid> _sourceIdFileCache = new ConcurrentDictionary<string, System.Guid>();
        private readonly ReaderWriterLock _locker = new ReaderWriterLock();

        private static readonly ConcurrentQueue<string> _filesToIngest = new ConcurrentQueue<string>();
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        private static System.Timers.Timer? _queueFlushTimer;
        private static System.Timers.Timer? _ingestFlushTimer;

        public KustoIngestController(ILogger<KustoIngestController> logger, IOptions<SettingsKusto> options)
        {
            _logger = logger;
            _settings = options.Value;
            var baseKcsb =
                new KustoConnectionStringBuilder(_settings.ClusterName);

            if (_settings.UseManagedIdentity)
            {
                if (string.IsNullOrEmpty(_settings.AppId))
                {
                    _kustoConnectionStringBuilderEngine = baseKcsb.WithAadSystemManagedIdentity();
                }
                else
                {
                    _kustoConnectionStringBuilderEngine = baseKcsb.WithAadUserManagedIdentity(_settings.AppId);
                }
            }
            else if (!string.IsNullOrEmpty(_settings.AccessToken))
            {
                _kustoConnectionStringBuilderEngine = baseKcsb.WithAadUserTokenAuthentication(_settings.AccessToken);
            }
            else
            {
                _kustoConnectionStringBuilderEngine = baseKcsb.WithAadApplicationKeyAuthentication(
                    _settings.ClientId, _settings.ClientSecret, _settings.TenantId);
            }
            _kustoConnectionStringBuilderEngine.SetConnectorDetails("KustoWriterApp", "1.0.0");
            _kustoIngestionProperties = new KustoIngestionProperties(databaseName: _settings.DbName, tableName: _settings.TableName);
            if (!string.IsNullOrEmpty(_settings.MappingName))
            {
                _kustoIngestionProperties.SetAppropriateMappingReference(_settings.MappingName, Kusto.Data.Common.DataSourceFormat.json);
            }
            _kustoIngestionProperties.Format = Kusto.Data.Common.DataSourceFormat.multijson;
            // Every interval that is configured for the aggregation, add the flush to the queue
            if (_queueFlushTimer == null)
            {
                _logger.LogInformation($"Initializing timer for flushing queue every {_settings.MaxBatchIntervalSeconds} seconds");
                _queueFlushTimer = new System.Timers.Timer(_settings.MaxBatchIntervalSeconds * 1000);
                _queueFlushTimer.Elapsed += StartBackgroundQueueFlusher;
                _queueFlushTimer.Enabled = true;
            }
            // The timer for ingesting files into Kusto
            if (_ingestFlushTimer == null)
            {
                _logger.LogInformation($"Initializing timer for ingest thread every 30 seconds");
                _ingestFlushTimer = new System.Timers.Timer(30 * 1000);
                _ingestFlushTimer.Elapsed += StartBackgroundFileIngestor;
                _ingestFlushTimer.Enabled = true;
            }
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
                }
            }
            return Ok();
        }

        private async void StartBackgroundQueueFlusher(object? source, ElapsedEventArgs e)
        {
            if (_timeseriesQueue.Count > 0)
            {
                _logger.LogDebug($"Firing time based flusher. Queue size {_timeseriesQueue.Count} at time {DateTime.UtcNow}");
                await WriteQueueToFileAsync();
            }
        }

        private async void StartBackgroundFileIngestor(object? source, ElapsedEventArgs e)
        {
            while (_filesToIngest.TryDequeue(out var filePath))
            {
                await IngestFileIntoKustoAsync(filePath);
            }
        }

        private async Task WriteQueueToFileAsync()
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"timeseries_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
            var timeseriesList = new List<Prometheus.TimeSeries>();
            while (_timeseriesQueue.TryDequeue(out var timeseries))
            {
                timeseriesList.Add(timeseries);
            }
            if (timeseriesList.Count > 0)
            {
                var json = JsonSerializer.Serialize(timeseriesList, options);
                Task fileWriteTask = System.IO.File.WriteAllTextAsync(filePath, json);
                await fileWriteTask.ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        _logger.LogInformation($"Wrote {timeseriesList.Count} timeseries to file {filePath}");
                        _filesToIngest.Enqueue(filePath);
                    }
                    else
                    {
                        _logger.LogError(task.Exception, $"Could not write to file {filePath}");
                    }
                });
            }
        }

        private async Task IngestFileIntoKustoAsync(string filePath)
        {
            // Implement Kusto ingestion logic here
            int retries = 0;
            using (IKustoIngestClient client = KustoIngestFactory.CreateQueuedIngestClient(_kustoConnectionStringBuilderEngine, new QueueOptions { MaxRetries = _settings.MaxRetries }))
            {
                while (retries < _settings.MaxRetries) // TODO Should we have this retry since non perm failures are retried automatically 
                {
                    var sourceGuid = _sourceIdFileCache.GetOrAdd(filePath, Guid.NewGuid());
                    var sourceOptions = new StorageSourceOptions
                    {
                        DeleteSourceOnSuccess = true,
                        SourceId = sourceGuid,
                    };
                    try
                    {
                        _logger.LogInformation($"Ingesting {filePath} into table {_settings.TableName}. Source id {sourceGuid} at time {DateTime.UtcNow}");
                        // Fix for the file not found issue
                        if (!System.IO.File.Exists(filePath))
                        {
                            _logger.LogWarning($"File {filePath} does not exist. Skipping ingestion.");
                            return;
                        }
                        Task<IKustoIngestionResult> ingestTask = client.IngestFromStorageAsync(filePath, _kustoIngestionProperties, sourceOptions);
                        await ingestTask.ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            {
                                _logger.LogDebug(task.Exception, $"Could not ingest {filePath} into table {_settings.TableName}. Failed source id {sourceGuid}");
                                retries++;
                                Thread.Sleep(_settings.MsBetweenRetries);
                            }
                        });
                        return;

                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Could not ingest {filePath} into table {_settings.TableName}.");
                        retries++;
                        Thread.Sleep(_settings.MsBetweenRetries);
                    }
                }
                _sourceIdFileCache.TryRemove(filePath, out _);
                _logger.LogInformation($"File {filePath} ingested into Kusto.");
            }
        }
    }
}
