namespace McpClient_cs;

/// <summary>
/// Contains constants used throughout the Knowledge API.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// API route paths.
    /// </summary>
    public static class Routes
    {
        public const string Api = "/api";
        public const string Knowledge = "/knowledge";
        public const string Chat = "/chat";
        public const string Ollama = "/ollama";
        public const string Analytics = "/analytics";
        public const string Health = "/health";
        public const string Ping = "/ping";
        public const string OllamaModels = "/models";
    }

    /// <summary>
    /// Configuration section names.
    /// </summary>
    public static class ConfigSections
    {
        public const string ChatCompleteSettings = "ChatCompleteSettings";
        public const string Cors = "Cors";
    }

    /// <summary>
    /// CORS policy names.
    /// </summary>
    public static class CorsPolicies
    {
        public const string DevFrontend = "DevFrontend";
    }

    /// <summary>
    /// Environment variable names.
    /// </summary>
    public static class EnvironmentVariables
    {
        public const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        public const string DotNetRunningInContainer = "DOTNET_RUNNING_IN_CONTAINER";
        public const string OpenAiApiKey = "OPENAI_API_KEY";
    }

    /// <summary>
    /// Swagger and OpenAPI tags.
    /// </summary>
    public static class Tags
    {
        public const string Knowledge = "Knowledge";
        public const string Chat = "Chat";
        public const string Health = "Health";
        public const string Ollama = "Ollama";
        public const string Analytics = "Analytics";
    }

    /// <summary>
    /// File system paths.
    /// </summary>
    public static class Paths
    {
        public const string AppData = "/app/data";
        public const string SwaggerEndpoint = "/swagger/v1/swagger.json";
    }

    /// <summary>
    /// Status values for health checks.
    /// </summary>
    public static class HealthStatus
    {
        public const string Healthy = "healthy";
        public const string Warning = "warning";
        public const string Unhealthy = "unhealthy";
        public const string Degraded = "degraded";
        public const string Error = "error";
        public const string Unknown = "unknown";
    }

    /// <summary>
    /// Health check component names.
    /// </summary>
    public static class HealthComponents
    {
        public const string VectorStore = "VectorStore";
        public const string DiskSpace = "DiskSpace";
        public const string Memory = "Memory";
    }

    /// <summary>
    /// Common response messages.
    /// </summary>
    public static class Messages
    {
        public const string NoFilesUploaded = "No files uploaded.";
        public const string Pong = "pong";
        public const string DataDirectoryNotFound = "Data directory not found";
        public const string FailedToStartOllamaCommand = "Failed to start ollama command";
        public const string FailedToFetchOllamaModels = "Failed to fetch Ollama models";
    }

    /// <summary>
    /// Content types and media types.
    /// </summary>
    public static class ContentTypes
    {
        public const string MultipartFormData = "multipart/form-data";
        public const string ApplicationJson = "application/json";
    }
}