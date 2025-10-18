using McpClient_cs;
using McpClient_cs.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

namespace McpClient_cs
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration from appsettings.json and user secrets
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>()
                .Build();

            // Load all settings from configuration
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);

            // Display loaded configuration (optional - for debugging)
            Console.WriteLine($"Using OpenAI Model: {appSettings.AiSettings.OpenAIModel}");
            Console.WriteLine($"Temperature: {appSettings.AiSettings.Temperature}");
            Console.WriteLine(
                $"API Key Environment Variable: {appSettings.AiSettings.OpenAIApiKeyEnvironmentVariable}"
            );

            // Get the OpenAI API key from environment variable
            var openAiKey =
                Environment.GetEnvironmentVariable(
                    appSettings.AiSettings.OpenAIApiKeyEnvironmentVariable
                )
                ?? throw new InvalidOperationException(
                    $"Environment variable '{appSettings.AiSettings.OpenAIApiKeyEnvironmentVariable}' not found"
                );

            // Verify the MCP server DLL exists before attempting to connect
            var mcpServerDll = appSettings.McpServer.DllPath;
            if (string.IsNullOrEmpty(mcpServerDll) || !File.Exists(mcpServerDll))
            {
                throw new FileNotFoundException(
                    $"MCP Server not found at: {mcpServerDll}. Please build the Knowledge.Mcp project first and update appsettings.json."
                );
            }

            Console.WriteLine($"MCP Server: {mcpServerDll}");
            // Create chat client with configured model
            IChatClient chatClient = new ChatClientBuilder(
                new OpenAIClient(openAiKey)
                    .GetChatClient(appSettings.AiSettings.OpenAIModel)
                    .AsIChatClient()
            )
                .UseFunctionInvocation()
                .Build();
            // Connect to the MCP server via STDIO transport
            Console.WriteLine("Connecting to MCP server...");
            var mcpClient = await McpClient.CreateAsync(
                new StdioClientTransport(
                    new StdioClientTransportOptions()
                    {
                        Command = appSettings.McpServer.Command,
                        Arguments = [mcpServerDll],
                    }
                )
            );
            Console.WriteLine("Connected to MCP server successfully!");
            Console.WriteLine();

            // Discover and display tools
            var tools = await mcpClient.ListToolsAsync();
            Console.WriteLine($"Discovered {tools.Count} MCP tools:");
            foreach (var tool in tools)
            {
                Console.WriteLine($"  - {tool.Name}");
            }
            Console.WriteLine();

            // Discover and display resources
            var resources = await mcpClient.ListResourcesAsync();
            Console.WriteLine($"Discovered {resources.Count} MCP resources:");
            foreach (var resource in resources)
            {
                Console.WriteLine($"  - {resource.Name}: {resource.Uri}");
            }
            Console.WriteLine();

            // Read a sample resource (system health)
            try
            {
                Console.WriteLine("Reading system health resource...");
                var healthUri = "resource://system/health";
                var healthResult = await mcpClient.ReadResourceAsync(healthUri);
                if (healthResult.Contents?.Count > 0)
                {
                    var content = healthResult.Contents[0];
                    if (content is TextResourceContents textContent)
                    {
                        Console.WriteLine($"System Health:\n{textContent.Text}");
                    }
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read system health: {ex.Message}");
                Console.WriteLine();
            }

            var historyMessages = new List<ChatMessage>();
            while (true)
            {
                Console.WriteLine("\nEnter your message (or 'exit' to quit):");
                var message = Console.ReadLine();

                // Exit condition
                if (string.IsNullOrWhiteSpace(message) || message.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                historyMessages.Add(new ChatMessage(ChatRole.User, message));

                // Accumulate response chunks before adding to history
                var fullResponse = new System.Text.StringBuilder();
                await foreach (
                    var response in chatClient.GetStreamingResponseAsync(
                        historyMessages,
                        new ChatOptions() { Tools = [.. tools] }
                    )
                )
                {
                    Console.Write(response.Text);
                    fullResponse.Append(response.Text);
                }

                Console.WriteLine(); // New line after response
                historyMessages.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
            }
        }
    }
}
