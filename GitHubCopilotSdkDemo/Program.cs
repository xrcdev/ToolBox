using Octokit;
using System.Text;
using System.Linq;

namespace GitHubCopilotSdkDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("GitHub Copilot Integration Demo for C#");
            Console.WriteLine("======================================\n");

            // Example 1: GitHub API Authentication with Octokit
            await Example1_GitHubApiAuthentication();

            // Example 2: Accessing Copilot via REST API
            await Example2_CopilotRestApi();

            // Example 3: Using GitHub Codespaces API
            await Example3_CodespacesIntegration();

            // Example 4: Working with GitHub Gists (Copilot stores snippets here)
            await Example4_GistIntegration();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Example 1: GitHub API Authentication with Octokit
        /// Demonstrates how to authenticate with GitHub using a Personal Access Token
        /// </summary>
        static async Task Example1_GitHubApiAuthentication()
        {
            Console.WriteLine("Example 1: GitHub API Authentication");
            Console.WriteLine("------------------------------------");

            try
            {
                // Get token from environment variable or user secrets
                var token = GetGitHubToken();

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("⚠ GitHub token not found. Please set GITHUB_TOKEN environment variable.");
                    Console.WriteLine("You can create a token at: https://github.com/settings/tokens");
                    return;
                }

                // Initialize GitHub client
                var client = new GitHubClient(new ProductHeaderValue("CopilotDemoApp"));
                client.Credentials = new Credentials(token);

                // Test authentication
                var user = await client.User.Get("github");
                Console.WriteLine($"✓ Authenticated successfully!");
                Console.WriteLine($"✓ API Rate Limit: {client.GetLastApiInfo()?.RateLimit.Remaining} requests remaining");

                // Get current user info
                var currentUser = await client.User.Current();
                Console.WriteLine($"✓ Current User: {currentUser.Login}");
                Console.WriteLine($"✓ Name: {currentUser.Name}");
                Console.WriteLine($"✓ Copilot Available: {HasCopilotAccess(currentUser)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 2: Accessing Copilot via REST API
        /// Demonstrates how to interact with Copilot's API endpoints directly
        /// Note: Copilot API requires authentication and proper licensing
        /// </summary>
        static async Task Example2_CopilotRestApi()
        {
            Console.WriteLine("Example 2: Copilot REST API Integration");
            Console.WriteLine("----------------------------------------");

            try
            {
                var token = GetGitHubToken();
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("⚠ GitHub token not found. Skipping this example.");
                    Console.WriteLine();
                    return;
                }

                // Copilot API base URL
                const string copilotApiUrl = "https://api.github.com/copilot";

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "CopilotDemoApp");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                // Example: Check Copilot subscription status
                Console.WriteLine("Checking Copilot subscription status...");
                var response = await httpClient.GetAsync($"{copilotApiUrl}/subscription");
                Console.WriteLine($"Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"✓ Copilot is enabled for this account");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("⚠ Unauthorized. Please check your token has Copilot permissions.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("ℹ This endpoint may require a Copilot Business subscription.");
                }

                // Example: Get Copilot completion
                Console.WriteLine("\nExample: Getting code completion...");

                // Example JSON payload for completion API
                var jsonPayload = @"{
  ""prompt"": ""public static void CalculateSum(int a, int b)"",
  ""suffix"": ""}"",
  ""language"": ""csharp"",
  ""max_tokens"": 100
}";
                var content2 = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Note: This is a demonstration - actual endpoint may differ
                var completionResponse = await httpClient.PostAsync(
                    $"{copilotApiUrl}/completions",
                    content2
                );

                Console.WriteLine($"Completion API Status: {completionResponse.StatusCode}");
                Console.WriteLine("ℹ Note: Copilot completion API may require additional setup");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 3: Using GitHub Codespaces API
        /// Shows how to interact with Codespaces where Copilot is pre-configured
        /// </summary>
        static async Task Example3_CodespacesIntegration()
        {
            Console.WriteLine("Example 3: GitHub Codespaces Integration");
            Console.WriteLine("-----------------------------------------");

            try
            {
                var token = GetGitHubToken();
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("⚠ GitHub token not found. Skipping this example.");
                    Console.WriteLine();
                    return;
                }

                var client = new GitHubClient(new ProductHeaderValue("CopilotDemoApp"));
                client.Credentials = new Credentials(token);

                // List all codespaces for the authenticated user
                Console.WriteLine("Fetching Codespaces...");
                try
                {
                    var codespaces = await client.Codespaces.GetAll();

                    Console.WriteLine($"✓ Found {codespaces.Count} Codespace(s)");
                    Console.WriteLine("ℹ Copilot is pre-configured in Codespaces!");
                    Console.WriteLine("ℹ Create a Codespace at: https://github.com/codespaces");
                }
                catch
                {
                    Console.WriteLine("ℹ Could not fetch Codespaces (this may require additional permissions)");
                    Console.WriteLine("ℹ Copilot is pre-configured in Codespaces regardless!");
                }

                Console.WriteLine("\nℹ Tip: Copilot works out-of-the-box in Codespaces!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 4: Working with GitHub Gists
        /// Demonstrates how to use Gists for storing and sharing code snippets
        /// Copilot can learn from your public gists
        /// </summary>
        static async Task Example4_GistIntegration()
        {
            Console.WriteLine("Example 4: GitHub Gist Integration");
            Console.WriteLine("------------------------------------");

            try
            {
                var token = GetGitHubToken();
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("⚠ GitHub token not found. Skipping this example.");
                    Console.WriteLine();
                    return;
                }

                var client = new GitHubClient(new ProductHeaderValue("CopilotDemoApp"));
                client.Credentials = new Credentials(token);

                // List user's gists
                Console.WriteLine("Fetching your Gists...");
                var gists = await client.Gist.GetAll();

                if (gists.Count == 0)
                {
                    Console.WriteLine("ℹ No gists found. Creating a sample gist...");

                    // Create a sample gist
                    var gistFiles = new Dictionary<string, string>
                    {
                        {
                            "Example.cs",
@"
// Sample C# code
public class Calculator
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public int Multiply(int a, int b) => a * b;

    public double Divide(int a, int b) => b != 0 ? (double)a / b : throw new DivideByZeroException();
}
"
                        }
                    };

                    var newGist = new NewGist
                    {
                        Description = "Sample C# code for Copilot demo",
                        Public = true
                    };

                    foreach (var file in gistFiles)
                    {
                        newGist.Files.Add(file.Key, file.Value);
                    }

                    var createdGist = await client.Gist.Create(newGist);
                    Console.WriteLine($"✓ Gist created: {createdGist.HtmlUrl}");
                    Console.WriteLine("ℹ Copilot can learn from your public gists!");
                }
                else
                {
                    Console.WriteLine($"✓ Found {gists.Count} gist(s):");
                    foreach (var gist in gists.Take(3))
                    {
                        Console.WriteLine($"  - {gist.Description ?? "Untitled"} ({gist.Files.Count} files)");
                        Console.WriteLine($"    URL: {gist.HtmlUrl}");
                        Console.WriteLine($"    Public: {gist.Public}");
                    }
                }

                Console.WriteLine("\nℹ Tip: Create code snippets in gists to help Copilot understand your patterns!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Helper: Get GitHub token from environment variable
        /// </summary>
        static string? GetGitHubToken()
        {
            return Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_PA_TOKEN");
        }

        /// <summary>
        /// Helper: Check if user has Copilot access
        /// This is a simplified check - actual implementation would verify subscription
        /// </summary>
        static bool HasCopilotAccess(User user)
        {
            // In a real implementation, you would check:
            // 1. User's subscription status via GitHub API
            // 2. Organization's Copilot Business subscription
            // 3. Copilot Trial eligibility
            return !string.IsNullOrEmpty(user.Plan?.Name);
        }

        /// <summary>
        /// Example: How to structure a Copilot-aware application
        /// </summary>
        static void Example5_ApplicationStructure()
        {
            Console.WriteLine("Example 5: Recommended Application Structure");
            Console.WriteLine("---------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("For integrating with Copilot in your C# applications:");
            Console.WriteLine();
            Console.WriteLine("1. Use Octokit for GitHub API interactions:");
            Console.WriteLine("   - Authentication and authorization");
            Console.WriteLine("   - Repository and gist management");
            Console.WriteLine("   - User and organization data");
            Console.WriteLine();
            Console.WriteLine("2. Store configuration securely:");
            Console.WriteLine("   - Use environment variables for tokens");
            Console.WriteLine("   - Leverage .NET User Secrets for development");
            Console.WriteLine("   - Use Azure Key Vault or similar for production");
            Console.WriteLine();
            Console.WriteLine("3. Handle Copilot features:");
            Console.WriteLine("   - Check for Copilot subscription");
            Console.WriteLine("   - Implement fallback when Copilot unavailable");
            Console.WriteLine("   - Cache completions to reduce API calls");
            Console.WriteLine();
            Console.WriteLine("4. Best practices:");
            Console.WriteLine("   - Never commit tokens to source control");
            Console.WriteLine("   - Implement exponential backoff for rate limits");
            Console.WriteLine("   - Log API interactions for debugging");
            Console.WriteLine("   - Provide user feedback for long operations");
            Console.WriteLine();
            Console.WriteLine("Example .NET Configuration (appsettings.json):");
            Console.WriteLine("{");
            Console.WriteLine("  \"GitHub\": {");
            Console.WriteLine("    \"Token\": \"your-token-here\",");
            Console.WriteLine("    \"Organization\": \"your-org\"");
            Console.WriteLine("  },");
            Console.WriteLine("  \"Copilot\": {");
            Console.WriteLine("    \"Enabled\": true,");
            Console.WriteLine("    \"Model\": \"gpt-4\",");
            Console.WriteLine("    \"MaxTokens\": 200");
            Console.WriteLine("  }");
            Console.WriteLine("}");
            Console.WriteLine();
            Console.WriteLine("Example User Secrets:");
            Console.WriteLine("dotnet user-secrets set \"GitHub:Token\" \"your-token-here\"");
            Console.WriteLine("dotnet user-secrets set \"Copilot:Enabled\" \"true\"");
            Console.WriteLine();
        }
    }
}
