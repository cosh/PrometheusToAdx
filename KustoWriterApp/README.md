# Prometheus Remote Write Plugin

## Introduction
The Prometheus Remote Write plugin allows you to send metrics data from Prometheus to remote storage systems, specifically using Kusto (Azure Data Explorer) as the backend. This plugin is useful for long-term storage, advanced querying, and integrating with other monitoring systems.

The KustoWriterApp project uses a direct controller and does not use any additional 

## Purpose
The primary purpose of this plugin is to enable the remote storage of Prometheus metrics in Kusto, ensuring that you can retain and analyze your metrics data over extended periods.

## Settings
To configure the Prometheus Remote Write plugin, you need to set the following parameters in your `appsettings.json` file when the docker image is built.

1. **KustoClusterUrl**: The URL of the Kusto cluster.
2. **DatabaseName**: The name of the Kusto database where metrics will be stored.
3. **TableName**: The name of the Kusto table where metrics will be ingested.
4. **ClientId**: The client ID for Azure AD authentication.
5. **ClientSecret**: The client secret for Azure AD authentication.
6. **TenantId**: The tenant ID for Azure AD authentication.
7. **IngestionMappingReference**: The reference to the ingestion mapping in Kusto.
8. **UseManagedIdentity**: Whether user managed identity has to be used. Defaults to false
9. **AppId**: User managed identity client id.
10. **AccessToken**: Use access token based authentication. Only for development use cases.
11. **MaxRetries**: In case of any errors, the number of retries to be attempted. Note that there is retry enabled from Prometheus as well.
12. **MsBetweenRetries**: Interval between retries. Defaults to 1 minute.
13. **MaxBatchIntervalSeconds**: To aggregate to larger batch sizes and reduce the number of batches written and ingested to Kusto. Defaults to 5 min

## Example Configuration

The appsettings.json looks similar to the following.

```json

{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kusto": {
    "ClusterName": "CLUSTER_NAME",
    "DbName": "e2e",
    "TableName": "RawData",
    "MaxRetries": 10,
    "MsBetweenRetries": 60000,
    "MaxBatchIntervalSeconds": 120,
    "MaxBatchSize": 100000,
    "MappingName": "RawData_mapping",
    "AccessToken": "PERSONAL_ACCESS_TOKEN"
  }
}

```

This can alsp be overriden using environment variables set on the docker container. For example, this can be set as **Kusto__ClusterName** as the cluster name.

## Setting up table and mapping

The example uses a simple table and mapping semantic. This can be customized using an ingestion mapping in case data needs to be transformed.

```
.create table RawData (Labels:dynamic,Samples:dynamic)
.create table RawData ingestion json mapping "RawDataMapping" '[{ "column" : "d", "datatype" : "dynamic", "path" : "$"}]'
```

## Running a sample load test

If you have AZ cli and docker installed on a workstation. A load test run can be performed by running the script **run-e2e-tests.sh**.

```bash
./run-e2e-tests.sh <kusto/kql fabric cluster url>
```