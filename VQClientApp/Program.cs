using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VQClientApp
{
    class Program
    {
        static AppConfiguration appConfiguration;

        static async Task Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.dev.json", optional: true)
                .SetBasePath(Path.GetDirectoryName(typeof(Program).Assembly.Location));

            var config = configBuilder.Build();

            appConfiguration = new AppConfiguration();
            config.Bind(appConfiguration);

            if (string.IsNullOrWhiteSpace(appConfiguration.ApiUrl) || string.IsNullOrWhiteSpace(appConfiguration.DiscoveryUrl))
            {
                Console.WriteLine("Please make sure your settings are populated");
                return;
            }
            
            await CallAPIWithClientSecret();

        }
        
        static async Task CallAPIWithClientSecret()
        {
            var client = new HttpClient();
            var tokenEndpoint = await GetDiscoveryDocument(client);

            if (string.IsNullOrWhiteSpace(tokenEndpoint))
            {
                return;
            }

            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = tokenEndpoint,

                ClientId = appConfiguration.ClientId,
                ClientSecret = appConfiguration.ClientSecret,
                Scope = appConfiguration.Scope,
        
            });

            if (response.IsError)
            {
                Console.WriteLine("Error obtaining token:");
                Console.WriteLine(response.Error);
                return;
            }

            await AccessAPI(response, client);
        }
        
        static async Task<string> GetDiscoveryDocument(HttpClient client)
        {
            Console.WriteLine("Getting token to use for API communication");

            var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = appConfiguration.DiscoveryUrl,
                Policy = new DiscoveryPolicy {ValidateEndpoints = false, ValidateIssuerName = false, RequireKeySet = false}
            });

            if (!disco.IsError)
            {
                return disco.TokenEndpoint;
            }

            Console.WriteLine("Error talking to token provider:");
            Console.WriteLine(disco.Error);

            return string.Empty;
        }

        static async Task AccessAPI(TokenResponse response, HttpClient client)
        {
            var token = response.AccessToken;

            Console.WriteLine("For demo purposes let's try and access the API without the token");

            var apiResponse = await client.GetAsync(appConfiguration.ApiUrl);
            if (apiResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("API said we're not allowed to access the API. Let's add a token header");
            }

            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse("Bearer "+token);

            Console.WriteLine("Added token header");

            apiResponse = await client.GetAsync(appConfiguration.ApiUrl);

            Console.WriteLine("API Response:");

            var content = await apiResponse.Content.ReadAsStringAsync();

            content = JToken.Parse(content).ToString(Formatting.Indented);

            Console.WriteLine(content);
        }


    }
}