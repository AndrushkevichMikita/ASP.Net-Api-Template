variable "resource_group_name" {
  description = "Resource group name"
  type        = string
  default     = "api-template-rg"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "cluster_name" {
  description = "AKS cluster name"
  type        = string
  default     = "api-template-aks"
}

variable "dns_prefix" {
  description = "DNS prefix for AKS"
  type        = string
  default     = "apitemplate"
}
