terraform {
  backend "azurerm" {
    resource_group_name  = "<RESOURCE_GROUP_FOR_STATE>"
    storage_account_name = "<STORAGE_ACCOUNT_NAME>"
    container_name       = "<CONTAINER_NAME>"
    key                  = "terraform.tfstate"
  }
}
