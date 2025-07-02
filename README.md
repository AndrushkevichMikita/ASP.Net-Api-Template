## About The Project

**Note**: This project is a non-commercial application based on authors personal interests of technologies.

It is specifically designed to serve as a starting point for real-world projects, providing a robust and scalable foundation that adheres to industry best practices, to ensure scalability, maintainability, and performance. The application is built using [Onion Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) and [Domain-Driven Design (DDD)](https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/february/best-practice-an-introduction-to-domain-driven-design) principles, providing a clean, modular structure that aligns with modern software engineering standards.

The backend is developed using ASP.NET Core 6.0 WebAPI, utilizing Entity Framework for database management and MSSQL as the database provider. Authentication and authorization are implemented using Identity Server Core, ensuring robust and secure user management.

The project employs a cutting-edge technology stack, including:

* Docker for containerization, enabling seamless deployment and scalability.
* Elasticsearch and Kibana for centralized logging and monitoring.
* Elastic APM for application performance tracking.
* Testcontainers for integration testing.

The application is cloud-ready, leveraging Azure as the primary cloud provider. It is deployed to Kubernetes using Azure Kubernetes Service (AKS). Tools like Automapper streamline object mapping, and xUnit is used for comprehensive unit testing, ensuring code quality throughout the development process.

By adhering to DDD approaches, the project emphasizes the core business logic and domain, ensuring a strong alignment between the code and the problem space it addresses.

Additionally, the project includes comprehensive support for containerized environments, allowing easy deployment using docker-compose. Logs are written to files, displayed in output windows during debugging, and stored in Elasticsearch, with visualization available through Kibana.

This project serves as a demonstration of modern software engineering practices, offering a strong foundation for building scalable, secure, and observable web applications.

### Built With

* [C# 10](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-10);
* [ASP.Net 6.0 WebAPI](https://docs.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-6.0?view=aspnetcore-6.0);
* [Docker](https://www.docker.com/);
* [Elasticsearch](https://www.elastic.co/elasticsearch);
* [Docker Hub](https://hub.docker.com/);
* [Kibana](https://www.elastic.co/kibana);
* [Elastic Application Performance Monitoring (APM)](https://www.elastic.co/observability/application-performance-monitoring);
* [MSSQL](https://www.microsoft.com/en-us/sql-server/sql-server-2017?rtc=1);
* [Entity Framework](https://entityframeworkcore.com);
* [Identity Server Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-6.0);
* [Azure Kubernetes Service (AKS)](https://azure.microsoft.com/en-us/services/kubernetes-service/);
* [Testcontainers](https://testcontainers.com/);
* [xUnit](https://xunit.net/);

### Prerequisites

Before launching this application make sure you have prepared the following components:

* Windows | macOS | Linux;
* [.Net 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0);
* [Docker](https://www.docker.com);
* [Visual Studio](https://visualstudio.microsoft.com/) | [Visual Studio Code](https://code.visualstudio.com/) | [Rider](https://www.jetbrains.com/rider/);
* [SSMS](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms?view=sql-server-ver15) | [DBeaver](https://dbeaver.io/) | [Azure Data Studio]("https://azure.microsoft.com/en-us/products/data-studio");

### Installation and launch

1. Clone repository:

   ```sh
   git clone https://github.com/AndrushkevichMikita/ASP.Net-Api-Template.git
   ```

2. Run **ApiTemplate.sln** in the root directory;
3. Run Docker Desktop, otherwise you will not be able to launch app;
4. Choose **docker-compose** profile and run the application;
5. Start the application, browser should load swagger page automatically;

### Docker launch

As an alternative you can run docker-compose without Visual Studio:
    Run from root of project via terminal

    docker-compose up --build
If you need to stop Docker containers, you can just press `ctrl + C` keyboard combination in your terminal and wait until containers will be stopped.
    To terminate containers, enter (or stop via Docker dashboard)

    docker-compose down

### Logging

Application does write logs to files, output window (if runs in debug mode), in elasticsearch logs accessible via Kibana (try to access Kibana via Docker desktop)

### EF Core migrations

In case if you need to introduce new migrations or remove old ones:

1. Your dotnet SDK should be corresponding with project's SDK version
2. Run from root of project via terminal:

    Add migration

    ```
    dotnet ef migrations add <name of migration> --startup-project Web --project Infrastructure
    ```

    Remove last migration

    ```
    dotnet ef migrations add <name of migration> --startup-project Web --project Infrastructure
    ```

### Azure Deployment

Continuous deployment to Azure Kubernetes Service is handled by the `azure-k8s-ci-cd-pipeline.yml` workflow. Once the test workflow completes successfully, this pipeline builds the Docker image, pushes it to Docker Hub, runs Terraform to provision the AKS infrastructure, and updates the cluster.

### CI/CD Secrets

The pipeline requires several GitHub repository secrets for authentication:

| Secret Name | Purpose |
|-------------|---------|
| `DOCKER_HUB_USERNAME` | Docker Hub account used to push images |
| `DOCKER_HUB_ACCESS_TOKEN` | Personal access token for Docker Hub |
| `AZURE_CREDENTIALS` | JSON credentials generated by `az ad sp create-for-rbac` |
| `AZURE_AKS_CLUSTER` | Name of the AKS cluster to deploy to |
| `AZURE_RESOURCE_GROUP` | Resource group containing the AKS cluster |

Create these secrets under your GitHub repository settings before running the pipeline.

### Infrastructure as Code

Terraform configurations in `infra/terraform` define the required Azure resources. The CI pipeline automatically applies these configurations. To run them locally, configure the backend in `backend.tf` and execute:

```bash
terraform init
terraform apply
```

