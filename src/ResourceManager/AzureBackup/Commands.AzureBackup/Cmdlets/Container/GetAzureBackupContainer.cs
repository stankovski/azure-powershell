﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.AzureBackup.Helpers;
using Microsoft.Azure.Commands.AzureBackup.Library;
using Microsoft.Azure.Commands.AzureBackup.Models;
using Microsoft.Azure.Management.BackupServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.Commands.AzureBackup.Cmdlets
{
    /// <summary>
    /// Get list of containers
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "AzureBackupContainer"), OutputType(typeof(List<AzureBackupContainer>))]
    public class GetAzureBackupContainer : AzureBackupVaultCmdletBase
    {
        [Parameter(Mandatory = false, HelpMessage = AzureBackupCmdletHelpMessage.ManagedResourceName)]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, HelpMessage = AzureBackupCmdletHelpMessage.ContainerType)]
        [ValidateNotNullOrEmpty]
        public AzureBackupContainerType Type { get; set; }

        [Parameter(Mandatory = false, HelpMessage = AzureBackupCmdletHelpMessage.ManagedResourceGroupName)]
        [ValidateNotNullOrEmpty]
        public string ManagedResourceGroupName { get; set; }

        [Parameter(Mandatory = false, HelpMessage = AzureBackupCmdletHelpMessage.ContainerRegistrationStatus)]
        [ValidateNotNullOrEmpty]
        public AzureBackupContainerRegistrationStatus Status { get; set; }

        public override void ExecuteCmdlet()
        {
            ExecutionBlock(() =>
            {
                base.ExecuteCmdlet();

                List<AzureBackupContainer> containers = new List<AzureBackupContainer>();

                switch (Type)
                {
                    case AzureBackupContainerType.Windows:
                    case AzureBackupContainerType.SCDPM:
                        containers.AddRange(GetMachineContainers());
                        break;
                    case AzureBackupContainerType.AzureVM:
                        containers.AddRange(GetManagedContainers());
                        break;
                    default:
                        break;
                }

                if (containers.Count == 1)
                {
                    WriteObject(containers.First());
                }
                else
                {
                    WriteObject(containers);
                }
            });
        }

        private List<AzureBackupContainer> GetMachineContainers()
        {
            List<MarsContainerResponse> marsContainerResponses = new List<MarsContainerResponse>();

            // Machine containers are always registered. 
            // So if requested Status is not Registered, return an empty list.
            // Machine containers don't have a resource group.
            // So, if a resource group is passed, return an empty list.
            if (Status == AzureBackupContainerRegistrationStatus.NotRegistered ||
                Status == AzureBackupContainerRegistrationStatus.Registering ||
                !string.IsNullOrEmpty(ManagedResourceGroupName))
            {
                return new List<AzureBackupContainer>();
            }

            if (string.IsNullOrEmpty(Name))
            {
                marsContainerResponses.AddRange(AzureBackupClient.ListMachineContainers());
            }
            else
            {
                marsContainerResponses.AddRange(AzureBackupClient.ListMachineContainers(Name));
            }

            return marsContainerResponses.ConvertAll<AzureBackupContainer>(marsContainerResponse =>
            {
                return new AzureBackupContainer(Vault, marsContainerResponse);
            }).Where(container => container.ContainerType == Type.ToString()).ToList();
        }

        private List<AzureBackupContainer> GetManagedContainers()
        {
            List<AzureBackupContainer> managedContainers = new List<AzureBackupContainer>();

            ContainerQueryParameters parameters = new ContainerQueryParameters();
            parameters.ContainerType = ManagedContainerType.IaasVM.ToString();
            parameters.FriendlyName = Name;
            if (Status != 0)
            {
                parameters.Status = Status.ToString();
            }

            List<CSMContainerResponse> containers = new List<CSMContainerResponse>();
            containers.AddRange(AzureBackupClient.ListContainers(parameters));
            WriteDebug(string.Format("Fetched {0} containers", containers.Count()));

            // When resource group name is specified, remove all containers whose resource group name
            // doesn't match the given resource group name
            if (!string.IsNullOrEmpty(ManagedResourceGroupName))
            {
                containers.RemoveAll(container =>
                {
                    string rgName = ContainerHelpers.GetRGNameFromId(container.Properties.ParentContainerId);
                    return rgName != ManagedResourceGroupName;
                });
                WriteDebug(string.Format("Count of containers after resource group filter = {0}", containers.Count));
            }

            // TODO: Container friendly name is not captures in Container response
            // BUG: Friendly name was previously assigned to ResourceName (vault name)
            managedContainers.AddRange(containers.ConvertAll(container =>
            {
                return new AzureBackupContainer(Vault, container);
            }));

            return managedContainers;
        }
    }
}
