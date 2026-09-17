# Provider and CLI versions are pinned for the same reason global.json pins the SDK:
# an unpinned provider upgrades itself between a plan and an apply, and the diff that
# results is not the one that was reviewed.

terraform {
  required_version = "~> 1.14"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.51"
    }
  }

  # Remote state, always. Local state cannot be shared, cannot be locked, and is lost
  # with the machine. Fill these in from the storage account that holds state for this
  # solution; create it out of band, because the thing that stores state cannot be
  # managed by the state it stores.
  backend "azurerm" {
    resource_group_name  = "" # e.g. rg-<solution>-tfstate
    storage_account_name = ""
    container_name       = "tfstate"
    key                  = "" # e.g. <solution>.<environment>.tfstate
  }
}

provider "azurerm" {
  features {}
}
