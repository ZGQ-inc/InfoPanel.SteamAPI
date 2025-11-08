using InfoPanel.SteamAPI.Tests;
using System;
using System.Threading.Tasks;

namespace InfoPanel.SteamAPI.TestRunner
{
    /// <summary>
    /// Simple test runner to verify Steam API functionality
    /// Run this to test the implementation without InfoPanel dependencies
    /// </summary>
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("InfoPanel Steam API Plugin - Test Runner");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            // Test configuration
            await SteamApiTest.TestConfigurationService();
            Console.WriteLine();
            
            // Test data model
            SteamApiTest.TestSteamDataModel();
            Console.WriteLine();
            
            // Test Steam API (only if configured)
            var configService = new InfoPanel.SteamAPI.Services.ConfigurationService(@"e:\GitHub\MyRepos\InfoPanel.SteamAPI\test-config.ini");
            if (!string.IsNullOrWhiteSpace(configService.SteamApiKey) && 
                configService.SteamApiKey != "<your-steam-api-key-here>")
            {
                await SteamApiTest.TestSteamApiService(configService.SteamApiKey, configService.SteamId64);
            }
            else
            {
                Console.WriteLine("Steam API Key not configured in test-config.ini");
                Console.WriteLine("To test API functionality:");
                Console.WriteLine("1. Get a Steam Web API key from: https://steamcommunity.com/dev/apikey");
                Console.WriteLine("2. Find your Steam ID64 using: https://steamid.io/");
                Console.WriteLine("3. Update test-config.ini with your API key and Steam ID64 (17 digits)");
            }
            
            Console.WriteLine();
            Console.WriteLine("Test completed. Press any key to exit...");
            Console.ReadKey();
        }
    }
}