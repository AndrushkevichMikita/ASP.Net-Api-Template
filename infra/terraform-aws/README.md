# Terraform AWS Infrastructure

This folder contains Terraform configuration to provision an Amazon ECS cluster running the application container on Fargate. The task definition also spins up sidecar containers for MSSQL, Elasticsearch, Kibana and the Elastic APM Server so the full stack can run in AWS.

## Usage

1. Ensure you have the AWS CLI configured with appropriate credentials.
2. Update variables in `variables.tf` if needed.
3. Initialize Terraform:
   ```bash
   terraform init
   ```
4. Review the plan:
   ```bash
   terraform plan -var=image_tag=<tag>
   ```
5. Apply the configuration:
   ```bash
   terraform apply -var=image_tag=<tag>
   ```

The `image_tag` variable determines which Docker image tag from the ECR repository will be deployed.
