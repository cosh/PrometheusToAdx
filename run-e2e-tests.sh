#/bin/bash
set -e
# validate the parameter exists or print usage
if [ -z "$1" ]; then
    echo "Usage: $0 <clusterName>"
    exit 1
fi
clusterName=$1
# get the access token for the cluster
pat=$(az account get-access-token --scope "$clusterName/.default" --query accessToken -o tsv)
cd KustoWriterApp
cp appsettings.json.template appsettings.json
sed -i -e 's|CLUSTER_NAME|'${clusterName}'|g' appsettings.json
sed -i -e 's|PERSONAL_ACCESS_TOKEN|'${pat}'|g' appsettings.json
cp appsettings.json appsettings.Development.json
# build this image
cd ..
docker build -t kustoremotewrite:latest . -f KustoWriterApp/Dockerfile
rm KustoWriterApp/appsettings.json
rm KustoWriterApp/appsettings.Development.json
cd PrometheusIntegrationTests
docker compose up
cd ..