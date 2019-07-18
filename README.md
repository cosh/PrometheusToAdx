# Azure Data Explorer Storage adapter for Prometheus.io

This is a a set of two [functions](https://azure.microsoft.com/en-us/services/functions/) which are needed for the "remote_write" and "remote_read" configuration of [Prometheus](https://prometheus.io/). It enables the user to use [Azure Data Explorer](https://azure.microsoft.com/en-us/services/data-explorer/) to read and write metrics.

## Architecture
![Alt text](https://raw.githubusercontent.com/cosh/PrometheusToAdx/master/pic/prometheusArch.svg?sanitize=true)

## HowTo

### 1. *Deploy* the infrastructure
### 2. *Create* tables in ADX
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
