# Terraform Infrastructure

This folder contains Terraform configuration to provision an Azure Kubernetes Service (AKS) cluster.
The GitHub Actions pipeline automatically applies these files so infrastructure stays in sync with the code.

## Usage

1. Update `backend.tf` with your remote state storage details.
2. Initialize Terraform:
   ```bash
   terraform init
   ```
3. Review the execution plan:
   ```bash
   terraform plan
   ```
4. Apply the configuration:
   ```bash
   terraform apply
   ```

Ensure the Azure CLI is authenticated or provide service principal credentials via the `ARM_*` environment variables.

The CI pipeline authenticates using the `AZURE_CREDENTIALS` secret defined in your GitHub repository settings. Create this secret from the output of `az ad sp create-for-rbac` and ensure it has permission to manage the target resource group and AKS cluster.
