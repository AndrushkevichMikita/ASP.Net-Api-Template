# Cleanup all services
Write-Host "Cleaning up API Template deployment..." -ForegroundColor Red

# Delete services in reverse order
Write-Host "Deleting Web API..." -ForegroundColor Yellow
kubectl delete -f k8s/web-api.yaml --ignore-not-found=true

Write-Host "Deleting APM Server..." -ForegroundColor Yellow
kubectl delete -f k8s/apm-server.yaml --ignore-not-found=true

Write-Host "Deleting Kibana..." -ForegroundColor Yellow
kubectl delete -f k8s/kibana.yaml --ignore-not-found=true

Write-Host "Deleting Elasticsearch..." -ForegroundColor Yellow
kubectl delete -f k8s/elasticsearch.yaml --ignore-not-found=true

Write-Host "Deleting MSSQL..." -ForegroundColor Yellow
kubectl delete -f k8s/mssql.yaml --ignore-not-found=true

Write-Host "Configuration cleanup not needed (using appsettings.kubernetes.json)..." -ForegroundColor Yellow

Write-Host "Deleting namespace..." -ForegroundColor Yellow
kubectl delete -f k8s/namespace.yaml --ignore-not-found=true

Write-Host "Cleanup completed!" -ForegroundColor Green