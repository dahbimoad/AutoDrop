using System.Text.Json.Serialization;

namespace AutoDrop.Models;

/// <summary>
/// Supported AI providers for file analysis.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiProvider
{
    /// <summary>
    /// OpenAI GPT models (GPT-4o, GPT-4o-mini).
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic Claude models (Claude 3.5 Sonnet, Claude 3 Haiku).
    /// </summary>
    Claude,

    /// <summary>
    /// Google Gemini models (Gemini 1.5 Pro, Gemini 1.5 Flash).
    /// </summary>
    Gemini,

    /// <summary>
    /// Groq-hosted models (Llama 3.3 70B, Llama 3.2 Vision).
    /// </summary>
    Groq,

    /// <summary>
    /// Local models via Ollama (privacy-focused, no API calls).
    /// </summary>
    Ollama
}
