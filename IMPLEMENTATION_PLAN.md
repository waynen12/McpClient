# MCP Client Implementation Plan

**Repository:** `/home/wayne/repos/McpClient`
**Start Date:** 2025-10-14
**Target:** Phase 1 (STDIO) → Phase 2 (HTTP SSE)

---

## Current Status ✅

**Repository Setup Complete:**
- ✅ Solution: `McpClient_cs.sln`
- ✅ Project: `McpClient_cs.csproj` (.NET 9)
- ✅ NuGet packages installed:
  - `ModelContextProtocol` v0.4.0-preview.2
  - `Microsoft.Extensions.AI` v9.9.1
  - `Microsoft.Extensions.AI.Ollama` v9.7.0-preview.1
  - `Microsoft.Extensions.AI.OpenAI` v9.9.1-preview.1
- ✅ Git repository initialized
- ✅ .gitignore configured

---

## Phase 1: STDIO Transport Implementation

### Goal
Create an MCP client that connects to the Knowledge Manager MCP server via **stdin/stdout** (STDIO transport).

### Week 1: Core Infrastructure (Oct 14-18)

#### Day 1: Project Structure & Transport Interface ⏳

**Tasks:**
- [x] Review current project setup
- [ ] Create folder structure
- [ ] Define `ITransport` interface
- [ ] Add required NuGet packages
- [ ] Create basic models

**Folder Structure to Create:**
```
McpClient_cs/
├── Transports/
│   ├── ITransport.cs
│   ├── StdioTransport.cs
│   └── TransportException.cs
├── Services/
│   ├── McpClientService.cs
│   ├── DiscoveryService.cs
│   └── ExecutionService.cs
├── Models/
│   ├── ClientConfiguration.cs
│   ├── McpServerConfig.cs
│   └── ConnectionState.cs
└── Program.cs
```

**Additional NuGet Packages Needed:**
```bash
cd /home/wayne/repos/McpClient/McpClient_cs
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Spectre.Console  # For rich CLI UI
```

**ITransport Interface:**
```csharp
namespace McpClient.Transports;

public interface ITransport : IDisposable
{
    /// <summary>
    /// Connects to the MCP server
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the MCP server
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a JSON-RPC request and waits for response
    /// </summary>
    Task<JsonRpcResponse> SendRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Listens for messages from the server (notifications, errors)
    /// </summary>
    IAsyncEnumerable<JsonRpcMessage> ListenAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates if the transport is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event fired when transport encounters an error
    /// </summary>
    event EventHandler<TransportException>? OnError;
}
```

---

#### Day 2-3: STDIO Transport Implementation

**StdioTransport.cs:**
```csharp
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using ModelContextProtocol.Protocol;

namespace McpClient.Transports;

public class StdioTransport : ITransport
{
    private readonly string _serverCommand;
    private readonly string[] _serverArgs;
    private readonly ILogger<StdioTransport> _logger;

    private Process? _serverProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private readonly Channel<JsonRpcMessage> _messageChannel;
    private CancellationTokenSource? _listenerCts;

    public bool IsConnected => _serverProcess?.HasExited == false;
    public event EventHandler<TransportException>? OnError;

    public StdioTransport(
        string serverCommand,
        string[] serverArgs,
        ILogger<StdioTransport> logger)
    {
        _serverCommand = serverCommand;
        _serverArgs = serverArgs;
        _logger = logger;
        _messageChannel = Channel.CreateUnbounded<JsonRpcMessage>();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting MCP server: {Command} {Args}",
            _serverCommand, string.Join(" ", _serverArgs));

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _serverCommand,
                Arguments = string.Join(" ", _serverArgs),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _serverProcess.Start();
        _stdin = _serverProcess.StandardInput;
        _stdout = _serverProcess.StandardOutput;
        _stderr = _serverProcess.StandardError;

        // Start listening for messages
        _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ListenToStdoutAsync(_listenerCts.Token), cancellationToken);
        _ = Task.Run(() => ListenToStderrAsync(_listenerCts.Token), cancellationToken);

        // Send initialize request
        await InitializeAsync(cancellationToken);

        _logger.LogInformation("Connected to MCP server (PID: {Pid})", _serverProcess.Id);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var initRequest = new JsonRpcRequest
        {
            Id = 1,
            Method = "initialize",
            Params = new InitializeRequestParams
            {
                ProtocolVersion = "2024-11-05",
                ClientInfo = new ClientInfo
                {
                    Name = "McpClient",
                    Version = "1.0.0"
                },
                Capabilities = new ClientCapabilities
                {
                    Roots = new RootsCapability { ListChanged = true },
                    Sampling = new SamplingCapability()
                }
            }
        };

        var response = await SendRequestAsync(initRequest, cancellationToken);

        if (response.Error != null)
        {
            throw new TransportException($"Initialization failed: {response.Error.Message}");
        }

        _logger.LogInformation("MCP server initialized successfully");
    }

    public async Task<JsonRpcResponse> SendRequestAsync(
        JsonRpcRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_stdin == null || !IsConnected)
            throw new InvalidOperationException("Not connected to server");

        var json = JsonSerializer.Serialize(request);
        _logger.LogDebug("Sending request: {Json}", json);

        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();

        // Wait for response (simplified - should match by ID)
        var response = await ReadNextResponseAsync(request.Id, cancellationToken);
        return response;
    }

    private async Task<JsonRpcResponse> ReadNextResponseAsync(
        int requestId,
        CancellationToken cancellationToken)
    {
        // Read from message channel until we find matching response
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (message is JsonRpcResponse response && response.Id == requestId)
            {
                return response;
            }
        }

        throw new TimeoutException($"No response received for request {requestId}");
    }

    private async Task ListenToStdoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stdout != null)
            {
                var line = await _stdout.ReadLineAsync();
                if (line == null) break;

                _logger.LogDebug("Received from server: {Line}", line);

                try
                {
                    var message = JsonSerializer.Deserialize<JsonRpcMessage>(line);
                    if (message != null)
                    {
                        await _messageChannel.Writer.WriteAsync(message, cancellationToken);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse message: {Line}", line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from server stdout");
            OnError?.Invoke(this, new TransportException("Error reading stdout", ex));
        }
    }

    private async Task ListenToStderrAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stderr != null)
            {
                var line = await _stderr.ReadLineAsync();
                if (line == null) break;

                _logger.LogWarning("Server stderr: {Line}", line);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from server stderr");
        }
    }

    public async IAsyncEnumerable<JsonRpcMessage> ListenAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from MCP server");

        _listenerCts?.Cancel();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _stdin?.Close();

            // Give process time to exit gracefully
            await Task.Delay(1000, cancellationToken);

            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill();
            }

            _serverProcess.Dispose();
        }

        _messageChannel.Writer.Complete();

        _logger.LogInformation("Disconnected from MCP server");
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _serverProcess?.Dispose();
        _listenerCts?.Dispose();
    }
}
```

---

#### Day 4-5: Service Layer

**McpClientService.cs:**
```csharp
using ModelContextProtocol.Protocol;

namespace McpClient.Services;

public class McpClientService : IDisposable
{
    private readonly ITransport _transport;
    private readonly ILogger<McpClientService> _logger;
    private readonly DiscoveryService _discovery;
    private readonly ExecutionService _execution;
    private ServerInfo? _serverInfo;

    public McpClientService(
        ITransport transport,
        ILogger<McpClientService> logger)
    {
        _transport = transport;
        _logger = logger;
        _discovery = new DiscoveryService(transport, logger);
        _execution = new ExecutionService(transport, logger);
    }

    public async Task<ServerInfo> ConnectAsync(CancellationToken ct = default)
    {
        await _transport.ConnectAsync(ct);
        _serverInfo = await _discovery.GetServerInfoAsync(ct);

        _logger.LogInformation(
            "Connected to MCP server: {Name} v{Version}",
            _serverInfo.Name,
            _serverInfo.Version);

        return _serverInfo;
    }

    public async Task<IReadOnlyList<Tool>> DiscoverToolsAsync(CancellationToken ct = default)
    {
        var tools = await _discovery.ListToolsAsync(ct);
        _logger.LogInformation("Discovered {Count} tools", tools.Count);
        return tools;
    }

    public async Task<IReadOnlyList<Resource>> DiscoverResourcesAsync(CancellationToken ct = default)
    {
        var resources = await _discovery.ListResourcesAsync(ct);
        _logger.LogInformation("Discovered {Count} resources", resources.Count);
        return resources;
    }

    public async Task<IReadOnlyList<ResourceTemplate>> DiscoverTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await _discovery.ListResourceTemplatesAsync(ct);
        _logger.LogInformation("Discovered {Count} resource templates", templates.Count);
        return templates;
    }

    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        object? arguments = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Executing tool: {ToolName}", toolName);
        return await _execution.CallToolAsync(toolName, arguments, ct);
    }

    public async Task<ResourceContents> ReadResourceAsync(
        string uri,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Reading resource: {Uri}", uri);
        return await _execution.ReadResourceAsync(uri, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _transport.DisconnectAsync(ct);
    }

    public void Dispose()
    {
        _transport?.Dispose();
    }
}
```

---

### Week 2: CLI Interface (Oct 21-25)

#### Interactive CLI with Spectre.Console

**Features:**
- Rich, colored terminal UI
- Interactive menu system
- Tool/resource discovery
- Execute tools with parameters
- Read resources with URI
- Connection management

**Program.cs Example:**
```csharp
using Spectre.Console;
using McpClient.Services;
using McpClient.Transports;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var transport = new StdioTransport(
    "dotnet",
    new[] { "run", "--project", "/home/wayne/repos/ChatComplete/Knowledge.Mcp/Knowledge.Mcp.csproj" },
    loggerFactory.CreateLogger<StdioTransport>());

var client = new McpClientService(
    transport,
    loggerFactory.CreateLogger<McpClientService>());

try
{
    AnsiConsole.Write(new FigletText("MCP Client").Centered().Color(Color.Blue));

    AnsiConsole.MarkupLine("[green]Connecting to Knowledge Manager MCP Server...[/]");
    var serverInfo = await client.ConnectAsync();

    AnsiConsole.MarkupLine($"[green]✓[/] Connected to [bold]{serverInfo.Name}[/] v{serverInfo.Version}");

    while (true)
    {
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[blue]What would you like to do?[/]")
                .AddChoices(new[]
                {
                    "📋 List Tools",
                    "📚 List Resources",
                    "🔧 Execute Tool",
                    "📖 Read Resource",
                    "🔍 Search Knowledge Bases",
                    "❌ Disconnect"
                }));

        switch (action)
        {
            case "📋 List Tools":
                await ListToolsAsync(client);
                break;
            case "📚 List Resources":
                await ListResourcesAsync(client);
                break;
            case "🔧 Execute Tool":
                await ExecuteToolAsync(client);
                break;
            case "📖 Read Resource":
                await ReadResourceAsync(client);
                break;
            case "🔍 Search Knowledge Bases":
                await SearchKnowledgeAsync(client);
                break;
            case "❌ Disconnect":
                await client.DisconnectAsync();
                AnsiConsole.MarkupLine("[yellow]Disconnected[/]");
                return;
        }
    }
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
}
```

---

### Week 3: Testing & Documentation (Oct 28 - Nov 1)

#### Unit Tests
```bash
cd /home/wayne/repos/McpClient
dotnet new xunit -n McpClient.Tests -o tests/McpClient.Tests
dotnet sln add tests/McpClient.Tests/McpClient.Tests.csproj
```

#### Integration Tests
```bash
dotnet new xunit -n McpClient.IntegrationTests -o tests/McpClient.IntegrationTests
dotnet sln add tests/McpClient.IntegrationTests/McpClient.IntegrationTests.csproj
```

---

## Phase 2: HTTP SSE Transport (Weeks 4-6)

### Week 4: Server-Side HTTP Endpoints

**Add to Knowledge.Api (Knowledge Manager):**
```csharp
// POST /mcp - Handle JSON-RPC requests
app.MapPost("/mcp", async ([FromBody] JsonRpcRequest request, IMcpServer mcpServer) =>
{
    var response = await mcpServer.HandleRequestAsync(request);
    return Results.Json(response);
});

// GET /mcp/events - Server-Sent Events stream
app.MapGet("/mcp/events", async (HttpContext context, IMcpServer mcpServer) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");

    await foreach (var message in mcpServer.GetEventStreamAsync(context.RequestAborted))
    {
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(message)}\n\n");
        await context.Response.Body.FlushAsync();
    }
});
```

### Week 5: Client-Side HTTP Transport

**HttpSseTransport.cs:**
- HTTP POST for requests
- SSE GET for server messages
- Reconnection logic
- Authentication support

### Week 6: Integration & Polish

- Cross-transport tests
- Performance benchmarks
- Documentation
- Examples

---

## Success Criteria

### Phase 1 Checklist
- [ ] ITransport interface defined
- [ ] StdioTransport implemented and working
- [ ] McpClientService implemented
- [ ] Can connect to Knowledge Manager MCP server
- [ ] Can discover all 11 tools
- [ ] Can execute tools successfully
- [ ] Can discover 6 resources (3 static + 3 parameterized)
- [ ] Can read resource content
- [ ] Interactive CLI working
- [ ] Unit tests (>80% coverage)
- [ ] Integration tests passing

### Phase 2 Checklist
- [ ] Server HTTP endpoints added
- [ ] HttpSseTransport implemented
- [ ] SSE streaming working
- [ ] Reconnection logic working
- [ ] Both transports configurable
- [ ] Performance acceptable (<100ms)
- [ ] Documentation complete

---

## Configuration

**appsettings.json:**
```json
{
  "McpServers": {
    "knowledge-manager": {
      "transport": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/home/wayne/repos/ChatComplete/Knowledge.Mcp/Knowledge.Mcp.csproj"
      ]
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "McpClient": "Debug"
    }
  }
}
```

---

## Daily Progress Tracking

Update this section daily:

### Day 1 (Oct 14, 2025)
- [x] Repository setup complete
- [ ] Created folder structure
- [ ] ITransport interface defined
- [ ] Added required NuGet packages

### Day 2 (Oct 15, 2025)
- [ ] StdioTransport implementation started
- [ ] Process management working
- [ ] Initialize request implemented

### Day 3 (Oct 16, 2025)
- [ ] StdioTransport completed
- [ ] Request/response handling working
- [ ] Message channel implemented

---

## Notes & Decisions

### Why .NET 9?
- Latest features
- Performance improvements
- Better async support

### Why Spectre.Console?
- Rich terminal UI
- Interactive menus
- Better UX than plain console

### Architecture Decisions
- **Separate transports**: Easy to add HTTP later
- **Service layer**: Clean separation of concerns
- **Async throughout**: Better performance
- **Channel-based messaging**: Efficient message handling

---

## Next Steps

**Tomorrow (Oct 15):**
1. Create folder structure
2. Define ITransport interface
3. Add remaining NuGet packages
4. Start StdioTransport implementation

**This Week:**
- Complete StdioTransport
- Implement service layer
- Basic CLI interface

**Next Week:**
- Rich CLI with Spectre.Console
- Tool execution
- Resource reading

---

## Resources

- **Knowledge Manager MCP Server:** `/home/wayne/repos/ChatComplete/Knowledge.Mcp`
- **MCP SDK Documentation:** https://github.com/modelcontextprotocol/dotnet-sdk
- **MCP Specification:** https://spec.modelcontextprotocol.io/
- **Spectre.Console Docs:** https://spectreconsole.net/

---

**Last Updated:** 2025-10-14
**Next Update:** Daily during implementation
