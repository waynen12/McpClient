using McpClient_cs;
using McpClient_cs.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
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
            var mcpServerDll =
                "/home/wayne/repos/ChatComplete/Knowledge.Mcp/bin/Debug/net8.0/Knowledge.Mcp.dll";
            if (!File.Exists(mcpServerDll))
            {
                throw new FileNotFoundException(
                    $"MCP Server not found at: {mcpServerDll}. Please build the Knowledge.Mcp project first."
                );
            }
            IChatClient chatClient = new ChatClientBuilder(
                new OpenAIClient(openAiKey).GetChatClient("gpt-4o").AsIChatClient()
            )
                .UseFunctionInvocation()
                .Build();
            // Connect to the MCP server via STDIO transport
            var mcpClient = await McpClient.CreateAsync(
                new StdioClientTransport(
                    new StdioClientTransportOptions()
                    {
                        Command = "dotnet",
                        Arguments = [mcpServerDll],
                    }
                )
            );
            var tools = await mcpClient.ListToolsAsync();
            foreach (var tool in tools)
            {
                Console.WriteLine(tool.Name);
            }

            var historyMessages = new List<ChatMessage>();
            while (true)
            {
                Console.WriteLine("Enter your message:");
                var message = Console.ReadLine();
                historyMessages.Add(new ChatMessage(ChatRole.User, message));
                await foreach (
                    var response in chatClient.GetStreamingResponseAsync(
                        historyMessages,
                        new ChatOptions() { Tools = [.. tools] }
                    )
                )
                {
                    Console.Write(response.Text);
                    historyMessages.Add(new ChatMessage(ChatRole.Assistant, response.Text));
                }
            }
        }
    }
}
