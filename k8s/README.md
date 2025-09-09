# 🚀 Kubernetes Deployment for ASP.NET Web API Template

This directory contains all the Kubernetes manifests and scripts needed to deploy your ASP.NET Web API application to a local Kubernetes cluster.

## 📋 **What's Included**

### **Kubernetes Manifests:**
- `namespace.yaml` - Creates the `apitemplate` namespace
- `mssql.yaml` - SQL Server database + persistent storage + services + loadbalancer
- `elasticsearch.yaml` - Elasticsearch for logging + services + loadbalancer
- `kibana.yaml` - Kibana for log visualization + services + loadbalancer
- `apm-server.yaml` - APM Server for application monitoring + services + loadbalancer
- `web-api.yaml` - ASP.NET Web API application + services + loadbalancer

### **Scripts:**
- `deploy-all.ps1` - Automated deployment script
- `cleanup.ps1` - Cleanup script to remove all resources

## 🛠️ **Prerequisites**

1. **Docker Desktop** - Running with Kubernetes enabled
2. **kubectl** - Kubernetes command-line tool

## 🚀 **Quick Start**

### **1. Deploy Everything (Automated)**
```powershell
# From the project root directory
.\k8s\deploy-all.ps1
```

## ✅ **Current Working Status**

| Service | Status | External Access | LoadBalancer |
|---------|--------|----------------|--------------|
| **MSSQL** | ✅ Working | `localhost:1433` | ✅ Active |
| **Elasticsearch** | ✅ Working | `localhost:9200` | ✅ Active |
| **Kibana** | ✅ Working | `localhost:5601` | ✅ Active |
| **APM Server** | ✅ Working | `localhost:8200` | ✅ Active |
| **Web API** | ✅ Working | `localhost:5000` | ✅ Active |

## 🌐 **Accessing Your Application**

### **LoadBalancer Access (No Port Forwarding Needed!)**
All services are accessible via LoadBalancers - no manual port forwarding required!

### **Access URLs:**
- **MSSQL**: `localhost:1433` (Azure Data Studio)
  - Username: `sa`
  - Password: `Passw0rd123`
  - Database: `master`
- **Elasticsearch**: http://localhost:9200
- **Kibana**: http://localhost:5601
- **APM Server**: http://localhost:8200
- **Web API**: http://localhost:5000
  - Health Check: http://localhost:5000/health
  - API Version: http://localhost:5000/api/version

### **Manual Port Forwarding (Alternative)**
```powershell
# Web API (main application)
kubectl port-forward service/web-api-service 5000:5000 -n apitemplate

# Kibana (log visualization)
kubectl port-forward service/kibana-service 5601:5601 -n apitemplate

# Elasticsearch (search engine)
kubectl port-forward service/elasticsearch-service 9200:9200 -n apitemplate

# APM Server (monitoring)
kubectl port-forward service/apm-service 8200:8200 -n apitemplate

# MSSQL (database)
kubectl port-forward service/mssql-service 1433:1433 -n apitemplate
```

## 📊 **Monitoring and Debugging**

### **View Pod Status**
```powershell
kubectl get pods -n apitemplate
kubectl get services -n apitemplate
kubectl get deployments -n apitemplate
```

### **View Logs**
```powershell
# Web API logs
kubectl logs -f deployment/web-api-deployment -n apitemplate

# Database logs
kubectl logs -f deployment/mssql-deployment -n apitemplate

# Elasticsearch logs
kubectl logs -f deployment/elasticsearch-deployment -n apitemplate

# Kibana logs
kubectl logs -f deployment/kibana-deployment -n apitemplate

# APM Server logs
kubectl logs -f deployment/apm-server-deployment -n apitemplate
```

### **Debug Pod Issues**
```powershell
# Describe a pod for detailed information
kubectl describe pod <pod-name> -n apitemplate

# Execute commands in a pod
kubectl exec -it <pod-name> -n apitemplate -- /bin/bash
```

## 🔧 **Configuration**

### **Application Configuration**
The Web API configuration is managed through ASP.NET Core's native configuration system:
- **`appsettings.json`** - Base application configuration
- **`appsettings.kubernetes.json`** - Kubernetes-specific overrides
  - Database connection: `mssql-service:1433`
  - Elasticsearch: `elasticsearch-service:9200`
  - APM Server: `apm-service:8200`
  - JWT, SMTP, and other settings

### **Resource Limits**
Each deployment has resource requests and limits defined:
- **Web API**: 512Mi-1Gi memory, 250m-500m CPU
- **MSSQL**: 512Mi-1Gi memory, 500m-1000m CPU
- **Elasticsearch**: 512Mi-1Gi memory, 250m-500m CPU
- **Kibana**: 512Mi-1Gi memory, 200m-500m CPU
- **APM Server**: 256Mi-512Mi memory, 100m-500m CPU

## 🧹 **Cleanup**

### **Remove All Resources**
```powershell
.\k8s\cleanup.ps1
```

### **Manual Cleanup**
```powershell
kubectl delete namespace apitemplate
```

## 🎓 **Learning Kubernetes Concepts**

### **What Each Manifest Does:**

1. **Namespace** - Isolates resources (like folders in a file system)
2. **StorageClass** - Defines how storage is provisioned
3. **PersistentVolume** - Provides storage that survives pod restarts
4. **PersistentVolumeClaim** - Requests storage from available volumes
5. **Deployment** - Manages pod replicas and rolling updates
6. **Service (ClusterIP)** - Provides internal network access to pods
7. **Service (LoadBalancer)** - Provides external network access to pods

### **Key Kubernetes Commands:**
```powershell
# Get resources
kubectl get <resource-type> -n <namespace>

# Describe resources
kubectl describe <resource-type> <name> -n <namespace>

# Apply changes
kubectl apply -f <manifest-file>

# Delete resources
kubectl delete -f <manifest-file>

# Watch resources
kubectl get pods -w -n <namespace>
```

## 🚨 **Troubleshooting**

### **Common Issues:**

1. **Pods stuck in Pending state**
   - Check resource availability: `kubectl describe pod <pod-name> -n apitemplate`
   - Check node capacity: `kubectl top nodes`

2. **Database connection issues**
   - Verify MSSQL service is running: `kubectl get pods -n apitemplate`
   - Check MSSQL logs: `kubectl logs deployment/mssql-deployment -n apitemplate`
   - Test connection: `Test-NetConnection -ComputerName localhost -Port 1433`

3. **Web API not accessible**
   - Check if Web API pod is running: `kubectl get pods -n apitemplate`
   - Check Web API logs: `kubectl logs deployment/web-api-deployment -n apitemplate`
   - Test health endpoint: `curl http://localhost:5000/health`

4. **Kibana not accessible**
   - Check if Kibana pod is running: `kubectl get pods -n apitemplate`
   - Check Kibana logs: `kubectl logs deployment/kibana-deployment -n apitemplate`
   - Verify LoadBalancer: `kubectl get services -n apitemplate`

5. **Memory issues (OOMKilled)**
   - Check pod status: `kubectl describe pod <pod-name> -n apitemplate`
   - Increase memory limits in the respective YAML file

## 📚 **Next Steps**

1. **Learn about Helm** - Package manager for Kubernetes
2. **Explore Ingress Controllers** - For production traffic routing
3. **Study Service Mesh** - For advanced networking (Istio)
4. **Practice with different storage classes** - For production storage
5. **Learn about RBAC** - Role-based access control
6. **Explore Operators** - For complex application management
7. **Add monitoring and alerting** - Prometheus + Grafana
8. **Implement CI/CD pipelines** - GitHub Actions or Azure DevOps
