namespace McpClient_cs.Models
{
    public class AppSettings
    {
        public AiSettings AiSettings { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public CorsSettings Cors { get; set; } = new();
    }

    public class AiSettings
    {
        public string OpenAIModel { get; set; } = string.Empty;
        public string GoogleModel { get; set; } = string.Empty;
        public string OllamaModel { get; set; } = string.Empty;
        public string OllamaBaseUrl { get; set; } = string.Empty;
        public string AnthropicModel { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public int ChatMaxTurns { get; set; }
        public EmbeddingProviders EmbeddingProviders { get; set; } = new();

        // Environment variable names for API keys
        public string OpenAIApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";
        public string GoogleApiKeyEnvironmentVariable { get; set; } = "GOOGLE_API_KEY";
        public string AnthropicApiKeyEnvironmentVariable { get; set; } = "ANTHROPIC_API_KEY";
    }

    public class EmbeddingProviders
    {
        public string ActiveProvider { get; set; } = string.Empty;
        public EmbeddingProvider OpenAI { get; set; } = new();
        public EmbeddingProvider Ollama { get; set; } = new();
        public EmbeddingProvider Alternative { get; set; } = new();
    }

    public class EmbeddingProvider
    {
        public string ModelName { get; set; } = string.Empty;
        public int Dimensions { get; set; }
        public double MinRelevanceScore { get; set; }
        public int MaxTokens { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class LoggingSettings
    {
        public LogLevelSettings LogLevel { get; set; } = new();
    }

    public class LogLevelSettings
    {
        public string Default { get; set; } = "Information";
        public string MicrosoftAspNetCore { get; set; } = "Warning";
        public string MicrosoftSemanticKernel { get; set; } = "Debug";
        public string MicrosoftSemanticKernelPlugins { get; set; } = "Trace";
        public string MicrosoftSemanticKernelFunctions { get; set; } = "Trace";
        public string MicrosoftSemanticKernelConnectors { get; set; } = "Debug";
    }

    public class CorsSettings
    {
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
        public string[] AllowedHeaders { get; set; } = Array.Empty<string>();
        public int MaxAgeHours { get; set; }
    }
}
