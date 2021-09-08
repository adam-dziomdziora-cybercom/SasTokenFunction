using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace BioMedicalCloud.Function
{
    public static class SasTokenFunction
    {
        [FunctionName("SasTokenFunction")]
        public static async Task<SasTokenResponse> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
        {
            // Retrieve connection string from settings
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

            // Create a BlobServiceClient object which will be used to create a container client
            var blobServiceClient = new BlobServiceClient(connectionString);

            //Create a unique name for the container
            var containerName = "mlblobcontainer2137";
            // Create the container and return a container client object
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);


            //Create a unique name for the container Access policy
            var policyName = "mlsaspolicy2137";
            // Create the container Access policy
            var policyResponse = await CreateStoredAccessPolicyAsync(containerClient, policyName);
            // Generate SAS using the provided policy
            var sasTokenUri = GetServiceSasUriForContainer(containerClient, policyName);
            var sasTokenResponse = new SasTokenResponse
            {
                ResponseMessage = $"policy last modified: {policyResponse.Value.LastModified.UtcDateTime.ToString()}",
                SasToken = sasTokenUri.Query,
            };

            // return the SAS Token to the client app
            return sasTokenResponse;
        }

        private async static Task<Response<BlobContainerInfo>> CreateStoredAccessPolicyAsync(BlobContainerClient containerClient, string policyName)
        {
            try
            {
                await containerClient.CreateIfNotExistsAsync();

                // Create one or more stored access policies.
                var signedIdentifiers = new List<BlobSignedIdentifier>
                        {
                            new BlobSignedIdentifier
                                {
                                    Id = policyName,
                                    AccessPolicy = new BlobAccessPolicy
                                    {
                                        StartsOn = DateTimeOffset.UtcNow,
                                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                                        Permissions = "rwldac"
                                    }
                                }
                       };
                // Set the container's access policy.
                return await containerClient.SetAccessPolicyAsync(permissions: signedIdentifiers);
            }
            catch (RequestFailedException e)
            {
                Console.WriteLine(e.ErrorCode);
                Console.WriteLine(e.Message);
                throw e;
            }
        }

        private static string GetAccountSASToken(StorageSharedKeyCredential key)
        {
            // Create a SAS token that's valid for one hour.
            AccountSasBuilder sasBuilder = new AccountSasBuilder()
            {
                Services = AccountSasServices.Blobs | AccountSasServices.Files,
                ResourceTypes = AccountSasResourceTypes.Service,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Protocol = SasProtocol.Https
            };

            sasBuilder.SetPermissions(AccountSasPermissions.Read |
                AccountSasPermissions.Write);

            // Use the key to get the SAS token.
            string sasToken = sasBuilder.ToSasQueryParameters(key).ToString();

            Console.WriteLine("SAS token for the storage account is: {0}", sasToken);
            Console.WriteLine();

            return sasToken;
        }

        private static Uri GetServiceSasUriForContainer(BlobContainerClient containerClient,
                                          string storedPolicyName = null)
        {
            // Check whether this BlobContainerClient object has been authorized with Shared Key.
            if (containerClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = containerClient.Name,
                    Resource = "c"
                };

                if (storedPolicyName == null)
                {
                    sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
                    sasBuilder.SetPermissions(BlobContainerSasPermissions.All);
                }
                else
                {
                    sasBuilder.Identifier = storedPolicyName;
                }

                Uri sasUri = containerClient.GenerateSasUri(sasBuilder);
                Console.WriteLine("SAS URI for blob container is: {0}", sasUri);
                Console.WriteLine();

                return sasUri;
            }
            else
            {
                Console.WriteLine(@"BlobContainerClient must be authorized with Shared Key 
                          credentials to create a service SAS.");
                return null;
            }
        }

    }
}