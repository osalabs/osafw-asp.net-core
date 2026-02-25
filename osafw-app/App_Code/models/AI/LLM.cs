using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using OpenAI.Responses;
using System;
using System.Text.Json;

namespace osafw;

/// <summary>
/// Centralized LLM model wrapper for OpenAI calls through Microsoft Agent Framework.
/// </summary>
public class LLM : FwModel
{
    // Preferred model constants for use across controllers/services.
    public const string MODEL_GPT5 = "gpt-5";
    public const string MODEL_GPT5_MINI = "gpt-5-mini";
    public const string MODEL_GPT5_NANO = "gpt-5-nano";
    public const string MODEL_GPT4O = "gpt-4o";
    public const string MODEL_GPT4O_MINI = "gpt-4o-mini";
    public const string MODEL_GPT41 = "gpt-4.1";
    public const string MODEL_GPT41_MINI = "gpt-4.1-mini";
    public const string MODEL_TEXT_EMBEDDING_3_SMALL = "text-embedding-3-small";

    /// <summary>
    /// Initializes non-DB model state.
    /// </summary>
    /// <param name="fw">Current request framework context used for config lookup and logging.</param>
    public override void init(FW fw)
    {
        base.init(fw);
        table_name = ""; // table-less model
    }

    /// <summary>
    /// Sends one prompt pair to the selected model and returns plain text output.
    /// </summary>
    /// <param name="model">Requested canonical model identifier (for example <c>gpt-5-mini</c>).</param>
    /// <param name="system_prompt">System instructions that define behavior and constraints for this call.</param>
    /// <param name="user_prompt">End-user prompt text that should be answered by the model.</param>
    /// <returns>Assistant text content extracted from the first non-streaming agent response.</returns>
    public string responseText(string model, string system_prompt, string user_prompt)
    {
        var agent = createAgent(model, system_prompt);
        var response = agent.RunAsync(user_prompt, null, null, default).GetAwaiter().GetResult();
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// Sends one prompt pair with a strict JSON schema response format and returns decoded JSON.
    /// </summary>
    /// <param name="model">Requested canonical model identifier (for example <c>gpt-4o-mini</c>).</param>
    /// <param name="system_prompt">System instructions applied to this model invocation.</param>
    /// <param name="user_prompt">End-user prompt text for this model invocation.</param>
    /// <param name="json_schema">JSON Schema document as a UTF-8 string. Expected to describe the full top-level response payload.</param>
    /// <returns>
    /// Parsed framework JSON object produced by <see cref="Utils.jsonDecode(string?)"/>:
    /// usually <see cref="FwDict"/> for object schemas or <see cref="FwList"/> for array schemas.
    /// </returns>
    public object? responseJson(string model, string system_prompt, string user_prompt, string json_schema)
    {
        var agent = createAgent(model, system_prompt);
        var schema = parseJsonSchema(json_schema);

        var runOptions = new ChatClientAgentRunOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schema,
                "response_schema",
                "Structured output schema for this response.")
        };

        var response = agent.RunAsync(user_prompt, null, runOptions, default).GetAwaiter().GetResult();
        return decodeJsonResponse(response.Text ?? string.Empty);
    }

    /// <summary>
    /// Generates an embedding vector for semantic search and similarity operations over arbitrary text.
    /// </summary>
    /// <param name="text">Input text that should be embedded; must contain non-whitespace characters.</param>
    /// <returns>
    /// Dense embedding vector returned by <see cref="MODEL_TEXT_EMBEDDING_3_SMALL"/> as a <see cref="float"/> array
    /// suitable for storage and similarity scoring.
    /// </returns>
    public float[] embeddingForText(string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new ApplicationException("Text is required for embedding generation.");

        var embeddingClient = getOpenAiClient().GetEmbeddingClient(MODEL_TEXT_EMBEDDING_3_SMALL);

        try
        {
            var result = embeddingClient.GenerateEmbedding(text);
            return result.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Failed to generate embedding from OpenAI.", ex);
        }
    }

    /// <summary>
    /// Builds a new OpenAI responses-backed agent for one request using configured API credentials.
    /// </summary>
    /// <param name="model">Canonical model identifier for client creation. Empty values default to <see cref="MODEL_GPT5_MINI"/>.</param>
    /// <param name="system_prompt">System instructions passed to the created agent.</param>
    /// <returns>Configured <see cref="ChatClientAgent"/> instance ready for immediate invocation.</returns>
    private ChatClientAgent createAgent(string model, string system_prompt)
    {
        var modelId = string.IsNullOrWhiteSpace(model) ? MODEL_GPT5_MINI : model.Trim();
#pragma warning disable OPENAI001
        var responsesClient = getOpenAiClient().GetResponsesClient(modelId);
#pragma warning restore OPENAI001
        return responsesClient.AsAIAgent(instructions: system_prompt, name: "LLM");
    }

    /// <summary>
    /// Builds an OpenAI client from framework configuration with backward-compatible key names.
    /// </summary>
    /// <returns>Configured <see cref="OpenAIClient"/> instance authenticated with the configured API key.</returns>
    private OpenAIClient getOpenAiClient()
    {
        var apiKey = fw.config("OPENAI_KEY").toStr();
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = fw.config("OPENAI_API_KEY").toStr();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ApplicationException("OpenAI API key is not configured. Set appSettings.OPENAI_KEY.");

        return new OpenAIClient(apiKey);
    }

    /// <summary>
    /// Parses a JSON schema string into an immutable element for chat response-format configuration.
    /// </summary>
    /// <param name="json_schema">Schema JSON as text.</param>
    /// <returns>Root schema element cloned from a temporary parse document.</returns>
    private static JsonElement parseJsonSchema(string json_schema)
    {
        if (string.IsNullOrWhiteSpace(json_schema))
            throw new ApplicationException("JSON schema is required for responseJson.");

        try
        {
            using var doc = JsonDocument.Parse(json_schema);
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Invalid JSON schema passed to LLM.responseJson.", ex);
        }
    }

    /// <summary>
    /// Decodes response text into framework-native JSON structures.
    /// </summary>
    /// <param name="jsonText">Raw model output expected to contain JSON.</param>
    /// <returns>Decoded framework JSON payload (<see cref="FwDict"/>, <see cref="FwList"/>, or scalar).</returns>
    private static object? decodeJsonResponse(string jsonText)
    {
        jsonText = jsonText?.Trim() ?? string.Empty;
        if (jsonText.StartsWith("```", StringComparison.Ordinal))
        {
            // Be tolerant to occasional markdown wrappers around JSON payloads.
            jsonText = jsonText.Trim('`').Trim();
            if (jsonText.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                jsonText = jsonText[4..].Trim();
        }

        if (jsonText.Length == 0)
            throw new ApplicationException("Model returned an empty JSON response.");

        try
        {
            return Utils.jsonDecode(jsonText);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Model returned malformed JSON.", ex);
        }
    }
}
