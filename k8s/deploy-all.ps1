# Deploy all services in the correct order
Write-Host "Deploying API Template to Kubernetes..." -ForegroundColor Green

# 1. Create namespace
Write-Host "Creating namespace..." -ForegroundColor Yellow
kubectl apply -f k8s/namespace.yaml

# 2. Configuration is now handled by appsettings.kubernetes.json
Write-Host "Configuration handled by appsettings.kubernetes.json..." -ForegroundColor Yellow

# 3. Deploy MSSQL (database first - includes persistent volumes)
Write-Host "Deploying MSSQL..." -ForegroundColor Yellow
kubectl apply -f k8s/mssql.yaml

# 4. Deploy Elasticsearch
Write-Host "Deploying Elasticsearch..." -ForegroundColor Yellow
kubectl apply -f k8s/elasticsearch.yaml

# 5. Deploy Kibana (depends on Elasticsearch)
Write-Host "Deploying Kibana..." -ForegroundColor Yellow
kubectl apply -f k8s/kibana.yaml

# 6. Deploy APM Server (depends on Elasticsearch)
Write-Host "Deploying APM Server..." -ForegroundColor Yellow
kubectl apply -f k8s/apm-server.yaml

# 7. Build and deploy Web API
Write-Host "Building Web API image..." -ForegroundColor Yellow
docker build -t apitemplate-web:latest -f ApiTemplate.Presentation.Web/Dockerfile .

Write-Host "Deploying Web API..." -ForegroundColor Yellow
kubectl apply -f k8s/web-api.yaml

Write-Host "Deployment completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Services with LoadBalancers:" -ForegroundColor Cyan
Write-Host "  - MSSQL: localhost:1433" -ForegroundColor White
Write-Host "  - Elasticsearch: localhost:9200" -ForegroundColor White
Write-Host "  - Kibana: localhost:5601" -ForegroundColor White
Write-Host "  - APM Server: localhost:8200" -ForegroundColor White
Write-Host "  - Web API: localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "Check status with: kubectl get all -n apitemplate" -ForegroundColor Cyan
