variable "region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "image_tag" {
  description = "Docker image tag to deploy"
  type        = string
  default     = "latest"
}
