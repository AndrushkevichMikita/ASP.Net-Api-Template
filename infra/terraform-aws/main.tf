terraform {
  required_version = ">= 1.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 4.0"
    }
  }
}

provider "aws" {
  region = var.region
}

resource "aws_ecr_repository" "web" {
  name = "apitemplate-web"
}

data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

resource "aws_ecs_cluster" "app" {
  name = "apitemplate-cluster"
}

data "aws_iam_policy_document" "ecs_task_exec_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "ecs_task_exec" {
  name               = "ecsTaskExecutionRole"
  assume_role_policy = data.aws_iam_policy_document.ecs_task_exec_assume_role.json
}

resource "aws_iam_role_policy_attachment" "ecs_task_exec" {
  role       = aws_iam_role.ecs_task_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_security_group" "web" {
  name        = "apitemplate-sg"
  description = "Allow inbound traffic to containers"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    from_port   = 1433
    to_port     = 1433
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    from_port   = 9200
    to_port     = 9200
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    from_port   = 5601
    to_port     = 5601
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    from_port   = 8200
    to_port     = 8200
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_ecs_task_definition" "web" {
  family                   = "apitemplate"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "2048"
  memory                   = "8192"
  execution_role_arn       = aws_iam_role.ecs_task_exec.arn

  container_definitions = jsonencode([
    {
      name      = "web"
      image     = "${aws_ecr_repository.web.repository_url}:${var.image_tag}"
      essential = true
      portMappings = [{
        containerPort = 80
        hostPort      = 80
      }]
    },
    {
      name      = "mssql"
      image     = "mcr.microsoft.com/mssql/server:2017-latest-ubuntu"
      essential = false
      environment = [
        { name = "ACCEPT_EULA", value = "Y" },
        { name = "SA_PASSWORD", value = "Passw0rd123" },
        { name = "MSSQL_PID", value = "Express" }
      ]
      portMappings = [{
        containerPort = 1433
        hostPort      = 1433
      }]
    },
    {
      name      = "elasticsearch"
      image     = "docker.elastic.co/elasticsearch/elasticsearch:7.9.3"
      essential = false
      environment = [
        { name = "bootstrap.memory_lock", value = "true" },
        { name = "cluster.name", value = "docker-cluster" },
        { name = "cluster.routing.allocation.disk.threshold_enabled", value = "false" },
        { name = "discovery.type", value = "single-node" },
        { name = "ES_JAVA_OPTS", value = "-XX:UseAVX=2 -Xms1g -Xmx1g" }
      ]
      ulimits = [{
        name      = "memlock"
        softLimit = -1
        hardLimit = -1
      }]
      portMappings = [{
        containerPort = 9200
        hostPort      = 9200
      }]
    },
    {
      name      = "kibana"
      image     = "docker.elastic.co/kibana/kibana:7.9.3"
      essential = false
      environment = [
        { name = "ELASTICSEARCH_URL", value = "http://localhost:9200" },
        { name = "ELASTICSEARCH_HOSTS", value = "http://localhost:9200" }
      ]
      portMappings = [{
        containerPort = 5601
        hostPort      = 5601
      }]
    },
    {
      name      = "apmserver"
      image     = "docker.elastic.co/apm/apm-server:7.9.3"
      essential = false
      command = [
        "apm-server", "-e",
        "-E", "apm-server.rum.enabled=true",
        "-E", "setup.kibana.host=kibana:5601",
        "-E", "setup.template.settings.index.number_of_replicas=0",
        "-E", "apm-server.kibana.enabled=true",
        "-E", "apm-server.kibana.host=kibana:5601",
        "-E", "output.elasticsearch.hosts=[\"localhost:9200\"]"
      ]
      portMappings = [{
        containerPort = 8200
        hostPort      = 8200
      }]
    }
  ])
}

resource "aws_ecs_service" "web" {
  name            = "apitemplate-service"
  cluster         = aws_ecs_cluster.app.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets         = data.aws_subnets.default.ids
    security_groups = [aws_security_group.web.id]
    assign_public_ip = true
  }
}

output "ecr_repository_url" {
  value = aws_ecr_repository.web.repository_url
}
