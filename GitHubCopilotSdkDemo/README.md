# GitHub Copilot Integration Demo for C#

This project demonstrates how to integrate GitHub Copilot functionality into C# applications using the GitHub API and Octokit.

## Prerequisites

1. **GitHub Account**: You need a GitHub account
2. **GitHub Personal Access Token**: Create a token with appropriate permissions
3. **.NET 10.0 SDK**: Ensure you have .NET 10.0 or later installed
4. **GitHub Copilot Subscription** (optional): Some features require Copilot access

## Getting Your GitHub Token

1. Go to [GitHub Settings > Developer Settings > Personal Access Tokens](https://github.com/settings/tokens)
2. Click "Generate new token" (classic)
3. Select the following scopes:
   - `repo` - Full control of private repositories
   - `gist` - Create gists
   - `codespaces` - Manage codespaces (for Example 3)
4. Generate and save the token securely

## Installation

Restore NuGet packages:

```bash
dotnet restore
```

## Configuration

### Option 1: Environment Variable (Recommended for production)

Set your GitHub token as an environment variable:

**Windows (Command Prompt):**
```cmd
set GITHUB_TOKEN=your_token_here
```

**Windows (PowerShell):**
```powershell
$env:GITHUB_TOKEN="your_token_here"
```

**Linux/Mac:**
```bash
export GITHUB_TOKEN="your_token_here"
```

### Option 2: User Secrets (Recommended for development)

```bash
# Navigate to the project directory
cd GitHubCopilotSdkDemo

# Set the token
dotnet user-secrets set "GitHub:Token" "your_token_here"

# Initialize user secrets if needed
dotnet user-secrets init
```

### Option 3: appsettings.json (Not recommended - security risk)

Create an `appsettings.json` file:

```json
{
  "GitHub": {
    "Token": "your_token_here"
  },
  "Copilot": {
    "Enabled": true,
    "Model": "gpt-4",
    "MaxTokens": 200
  }
}
```

**⚠️ Security Warning**: Never commit `appsettings.json` with tokens to source control! Add it to `.gitignore`.

## Running the Demo

```bash
dotnet run --project GitHubCopilotSdkDemo.csproj
```

## Examples Overview

### Example 1: GitHub API Authentication
- Initialize GitHub client using Octokit
- Authenticate with Personal Access Token
- Get current user information
- Check API rate limits

### Example 2: Copilot REST API Integration
- Demonstrate how to call Copilot API endpoints
- Check Copilot subscription status
- Example of completion API structure
- Handle different response scenarios

### Example 3: GitHub Codespaces Integration
- List existing Codespaces
- Show how Copilot is pre-configured in Codespaces
- Display Codespace details (name, state, location)

### Example 4: GitHub Gist Integration
- List user's gists
- Create new gists programmatically
- Explain how Copilot learns from public gists

## Key Features Demonstrated

✓ GitHub API authentication using Octokit
✓ REST API integration patterns
✓ Codespaces management
✓ Gist creation and management
✓ Error handling and best practices
✓ Secure token management
✓ Configuration options

## Libraries Used

- **Octokit** (v13.0.1): Official GitHub API client for .NET
- **Microsoft.Extensions.Configuration**: Configuration management
- **System.Text.Json**: JSON serialization

## Common Use Cases

### 1. Automating Repository Workflows

```csharp
var client = new GitHubClient(new ProductHeaderValue("MyApp"));
client.Credentials = new Credentials(token);

// Create an issue
var newIssue = new NewIssue("Bug in production")
{
    Body = "Description of the bug..."
};
var issue = await client.Issue.Create("owner", "repo", newIssue);
```

### 2. Managing Gists for Code Snippets

```csharp
var gist = new NewGist
{
    Description = "My code snippet",
    Public = true,
    Files = new Dictionary<string, NewGistFile>
    {
        { "snippet.cs", new NewGistFile { Content = "public void Hello() { }" } }
    }
};
var createdGist = await client.Gist.Create(gist);
```

### 3. Working with Codespaces

```csharp
var codespaces = await client.Codespaces.GetAll();
foreach (var codespace in codespaces)
{
    Console.WriteLine($"{codespace.Name}: {codespace.State}");
}
```

## Copilot Integration Tips

While there's no official C# SDK for GitHub Copilot, you can:

1. **Use the GitHub API**: Interact with GitHub services that Copilot uses
2. **Store code in Gists**: Copilot can learn from your public gists
3. **Use Codespaces**: Pre-configured Copilot environment
4. **Build VS Extensions**: Create extensions that interact with Copilot

## Security Best Practices

1. **Never commit tokens** to version control
2. **Use environment variables** for production
3. **Use User Secrets** for development
4. **Rotate tokens regularly**
5. **Use minimal required scopes**
6. **Implement rate limiting** to avoid API limits
7. **Log API calls** for security auditing

## Troubleshooting

### Authentication Errors
- Verify your token is valid
- Check token has required scopes
- Ensure token hasn't expired

### Rate Limiting
GitHub API has rate limits:
- Authenticated requests: 5,000 requests/hour
- Check `client.GetLastApiInfo().RateLimit` for remaining calls

### Network Issues
- Check internet connectivity
- Verify firewall settings
- Ensure proxy configuration if needed

## Next Steps

1. **Explore Octokit Documentation**: https://octokitnet.readthedocs.io/
2. **GitHub API Docs**: https://docs.github.com/en/rest
3. **Copilot Documentation**: https://docs.github.com/en/copilot
4. **Build Your Own Integration**: Use these examples as a starting point

## Resources

- [Octokit.NET GitHub](https://github.com/octokit/octokit.net)
- [GitHub REST API](https://docs.github.com/en/rest)
- [GitHub Copilot Documentation](https://docs.github.com/en/copilot)
- [.NET Configuration](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [.NET User Secrets](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)

## License

This demo project is provided as-is for educational purposes.
