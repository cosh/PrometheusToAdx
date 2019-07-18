# Azure Data Explorer Storage adapter for Prometheus.io

This is a a set of two [functions](https://azure.microsoft.com/en-us/services/functions/) which are needed for the "remote_write" and "remote_read" configuration of [Prometheus](https://prometheus.io/). It enables the user to use [Azure Data Explorer](https://azure.microsoft.com/en-us/services/data-explorer/) to read and write metrics.

## Architecture
![Alt text](https://raw.githubusercontent.com/cosh/PrometheusToAdx/master/pic/prometheusArch.svg?sanitize=true)

## HowTo

### 1. *Deploy* the infrastructure
(take a look at the architecure and have fun in the Azure portal or contribute some proper ARM templates :D)

### 2. *Create* tables in ADX
``` 
.create table RawData ingestion json mapping "RawDataMapping" '[{ "column" : "d", "datatype" : "dynamic", "path" : "$"}]'

.create function Update_SearchExplosion() 
{ 
RawData
| extend id = new_guid()
| mv-expand d.Samples
| extend  value=todouble(d_Samples.Value), ts=datetime(1970-01-01) + tolong(d_Samples.Timestamp)*1ms, labels=d.Labels
| project-away d_Samples
| mv-expand labels
| extend label=tostring(labels.Name), labelValue=tostring(labels.Value)
| project-away labels
| extend p = pack(label, labelValue)
| summarize label=make_bag(p), timeseries=any(d), value=any(value) by ['id'], ts
| project-away ['id']
}

.create table SearchExplosion (ts: datetime, label: dynamic, timeseries: dynamic, value: real)

.alter table SearchExplosion policy update
@'[{"IsEnabled": true, "Source": "RawData", "Query": "Update_SearchExplosion()", "IsTransactional": false, "PropagateIngestionProperties": false}]'
``` 

### 3. *Configure* prometheus

Sample config extension for prometheus.yml:
``` 
remote_write:
  - url: "https://<writeFunc>.azurewebsites.net/api/Write"
    remote_timeout: 30s
    queue_config:
        capacity: 100000
        max_shards: 1000
        max_samples_per_send: 1000
        batch_send_deadline: 5s
        max_retries: 10
        min_backoff: 30ms
        max_backoff: 100ms

remote_read:
  - url: "https://<readFunc>.azurewebsites.net/api/Read" 
    read_recent: true
```
