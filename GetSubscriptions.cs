// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using Azure.Identity;
using Azure.Core;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace MultiTenantSubscriptions
{
	public static class GetSubscriptions
	{
		[FunctionName("GetSubscriptions")]
		public static async Task<IActionResult> Run(
				[HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
				ILogger log)
		{
			log.LogInformation("C# HTTP trigger function processed a request.");

			string[] tenantIds = Environment.GetEnvironmentVariable("TenantIds").Split(",", StringSplitOptions.TrimEntries);
			var clientId = Environment.GetEnvironmentVariable("ClientId");
			var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
			var apiVersion = "2022-12-01";
			var subscriptions = new List<object>();

			foreach (var tenantId in tenantIds)
			{
				string accessToken;	

				log.LogInformation("Getting token for tenant {0}", tenantId);

				try
				{
					//Enable Azure.Identity logging (when needed)
					//using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();

					var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
					var token = await credential.GetTokenAsync(
							new TokenRequestContext(scopes: new string[] { "https://management.azure.com/.default" }) {}
						);
					accessToken = token.Token.ToString();

					//TODO: Implement token caching
				}
				catch (Exception ex)
				{
					log.LogError("Error getting token {0}", ex.Message);
					continue;
				}

				log.LogInformation("Received accessToken {0}", string.IsNullOrEmpty(accessToken) ? "null" : "<encrypted>");
				log.LogInformation("Calling Azure Resource Manager API");

				var apiUrl = $"https://management.azure.com/subscriptions?api-version={apiVersion}";

				var httpClient = new HttpClient();
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

				var response = await httpClient.GetAsync(apiUrl);
				var responseBody = await response.Content.ReadAsStringAsync();

				if (response.StatusCode == System.Net.HttpStatusCode.OK) 
				{
					log.LogInformation("Retrieved response");

					dynamic json = JObject.Parse(responseBody);
					dynamic value = json.value;

					log.LogInformation("Retrieved {0} subscriptions", (int)value.Count);				

					subscriptions.AddRange(json.value);
				} 
				else
				{
					log.LogInformation("Retrieved error response {0}", responseBody);
				}
			}	

			log.LogInformation("Returning response");

			return new JsonResult(subscriptions);
		}
	}
}
