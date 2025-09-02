# Deploy all services in the correct order
Write-Host "Deploying API Template to Kubernetes..." -ForegroundColor Green

# 1. Create namespace
Write-Host "Creating namespace..." -ForegroundColor Yellow
kubectl apply -f k8s/namespace.yaml

# 2. Create configuration
Write-Host "Creating configuration..." -ForegroundColor Yellow
kubectl apply -f k8s/configmap.yaml

# 3. Deploy MSSQL (database first - includes persistent volumes)
Write-Host "Deploying MSSQL..." -ForegroundColor Yellow
kubectl apply -f k8s/mssql.yaml

# 4. Deploy Elasticsearch
Write-Host "Deploying Elasticsearch..." -ForegroundColor Yellow
kubectl apply -f k8s/elasticsearch.yaml

# 5. Deploy Kibana (depends on Elasticsearch)
Write-Host "Deploying Kibana..." -ForegroundColor Yellow
kubectl apply -f k8s/kibana.yaml

Write-Host "Deployment completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Services with LoadBalancers:" -ForegroundColor Cyan
Write-Host "  - MSSQL: localhost:1433" -ForegroundColor White
Write-Host "  - Elasticsearch: localhost:9200" -ForegroundColor White
Write-Host "  - Kibana: localhost:5601" -ForegroundColor White
Write-Host ""
Write-Host "Note: Web API deployment removed - will be added later" -ForegroundColor Yellow
Write-Host ""
Write-Host "Check status with: kubectl get all -n apitemplate" -ForegroundColor Cyan
