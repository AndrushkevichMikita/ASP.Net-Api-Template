# 🚀 Kubernetes Deployment for ASP.NET Web API Template

This directory contains all the Kubernetes manifests and scripts needed to deploy your ASP.NET Web API application to a local Kubernetes cluster.

## 📋 **What's Included**

### **Kubernetes Manifests:**
- `namespace.yaml` - Creates the `apitemplate` namespace
- `configmap.yaml` - Application configuration (non-sensitive)
- `mssql.yaml` - SQL Server database + persistent storage + services + loadbalancer
- `elasticsearch.yaml` - Elasticsearch for logging + services + loadbalancer
- `kibana.yaml` - Kibana for log visualization + services + loadbalancer

### **Scripts:**
- `deploy-all.ps1` - Automated deployment script
- `cleanup.ps1` - Cleanup script to remove all resources

## 🛠️ **Prerequisites**

1. **Docker Desktop** - Running and accessible
2. **Minikube** - Local Kubernetes cluster
3. **kubectl** - Kubernetes command-line tool

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
| **Web API** | ⚠️ Pending | `localhost:80` | ⚠️ To be added |

## 🌐 **Accessing Your Application**

### **LoadBalancer Access (No Port Forwarding Needed!)**
All services are accessible via LoadBalancers - no manual port forwarding required!

### **Access URLs:**
- **MSSQL**: `localhost:1433` (Azure Data Studio)
  - Username: `sa`
  - Password: `Passw0rd123`
  - Database: `master`
- **Kibana**: http://localhost:5601
- **Elasticsearch**: http://localhost:9200

### **Manual Port Forwarding (Alternative)**
```powershell
# Kibana (log visualization)
kubectl port-forward service/kibana-service 5601:5601 -n apitemplate

# Elasticsearch (search engine)
kubectl port-forward service/elasticsearch-service 9200:9200 -n apitemplate

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
# Database logs
kubectl logs -f deployment/mssql-deployment -n apitemplate

# Elasticsearch logs
kubectl logs -f deployment/elasticsearch-deployment -n apitemplate

# Kibana logs
kubectl logs -f deployment/kibana-deployment -n apitemplate
```

### **Debug Pod Issues**
```powershell
# Describe a pod for detailed information
kubectl describe pod <pod-name> -n apitemplate

# Execute commands in a pod
kubectl exec -it <pod-name> -n apitemplate -- /bin/bash
```

## 🔧 **Configuration**

### **Environment Variables**
The application configuration is managed through:
- **ConfigMap** (`configmap.yaml`) - Non-sensitive configuration
- **appsettings.json** - Application-specific configuration

### **Resource Limits**
Each deployment has resource requests and limits defined:
- **MSSQL**: 512Mi-1Gi memory, 500m-1000m CPU
- **Elasticsearch**: 512Mi-1Gi memory, 250m-500m CPU
- **Kibana**: 512Mi-1Gi memory, 200m-500m CPU

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
2. **ConfigMap** - Stores configuration data as key-value pairs
3. **StorageClass** - Defines how storage is provisioned
4. **PersistentVolume** - Provides storage that survives pod restarts
5. **PersistentVolumeClaim** - Requests storage from available volumes
6. **Deployment** - Manages pod replicas and rolling updates
7. **Service (ClusterIP)** - Provides internal network access to pods
8. **Service (LoadBalancer)** - Provides external network access to pods

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

3. **Kibana not accessible**
   - Check if Kibana pod is running: `kubectl get pods -n apitemplate`
   - Check Kibana logs: `kubectl logs deployment/kibana-deployment -n apitemplate`
   - Verify LoadBalancer: `kubectl get services -n apitemplate`

4. **Memory issues (OOMKilled)**
   - Check pod status: `kubectl describe pod <pod-name> -n apitemplate`
   - Increase memory limits in the respective YAML file

## 📚 **Next Steps**

1. **Add Web API deployment** - Deploy your ASP.NET application
2. **Learn about Helm** - Package manager for Kubernetes
3. **Explore Ingress Controllers** - For production traffic routing
4. **Study Service Mesh** - For advanced networking (Istio)
5. **Practice with different storage classes** - For production storage
6. **Learn about RBAC** - Role-based access control
7. **Explore Operators** - For complex application management
