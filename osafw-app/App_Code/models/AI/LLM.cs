using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Embeddings;
using OpenAI.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace osafw;

/// <summary>
/// Central OpenAI helper used by assistant, KB indexing, and small framework LLM calls.
/// </summary>
public class LLM : FwModel
{
    public const string MODEL_GPT5 = "gpt-5";
    public const string MODEL_GPT5_MINI = "gpt-5-mini";
    public const string MODEL_GPT5_NANO = "gpt-5-nano";
    public const string MODEL_GPT4O = "gpt-4o";
    public const string MODEL_GPT4O_MINI = "gpt-4o-mini";
    public const string MODEL_GPT41 = "gpt-4.1";
    public const string MODEL_GPT41_MINI = "gpt-4.1-mini";
    public const string MODEL_TEXT_EMBEDDING_3_SMALL = "text-embedding-3-small";

    public override void init(FW fw)
    {
        base.init(fw);
        table_name = string.Empty;
        is_log_changes = false;
    }

    /// <summary>
    /// Returns whether a usable OpenAI key is configured without throwing.
    /// </summary>
    public bool isConfigured()
    {
        return !string.IsNullOrWhiteSpace(apiKey());
    }

    /// <summary>
    /// Sends one prompt pair to the selected model and returns plain text output.
    /// </summary>
    public async Task<string> responseTextAsync(string model, string system_prompt, string user_prompt, CancellationToken cancellationToken = default)
    {
        var agent = createAgent(model, system_prompt);
        var response = await agent.RunAsync(user_prompt ?? string.Empty, null, null, cancellationToken).ConfigureAwait(false);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// Sends one prompt pair with an optional JSON schema response format and returns a typed payload.
    /// </summary>
    /// <typeparam name="T">Expected top-level JSON shape.</typeparam>
    public async Task<T?> responseJsonAsync<T>(string model, string system_prompt, string user_prompt, string json_schema = "", CancellationToken cancellationToken = default)
    {
        var agent = createAgent(model, system_prompt);
        ChatClientAgentRunOptions? runOptions = null;
        if (!string.IsNullOrWhiteSpace(json_schema))
        {
            runOptions = new ChatClientAgentRunOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                    parseJsonSchema(json_schema),
                    "response_schema",
                    "Structured output schema for this response.")
            };
        }

        var response = await agent.RunAsync(user_prompt ?? string.Empty, null, runOptions, cancellationToken).ConfigureAwait(false);
        var jsonText = normalizeJsonResponse(response.Text ?? string.Empty);
        return JsonSerializer.Deserialize<T>(jsonText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Generates an embedding vector for semantic retrieval.
    /// </summary>
    public async Task<List<float>> embeddingForTextAsync(string text, string model = "", CancellationToken cancellationToken = default)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            throw new ApplicationException("Text is required for embedding generation.");

        var embeddingModel = string.IsNullOrWhiteSpace(model)
            ? fw.config("ASSISTANT_EMBEDDING_MODEL").toStr(MODEL_TEXT_EMBEDDING_3_SMALL)
            : model.Trim();
        if (string.IsNullOrWhiteSpace(embeddingModel))
            embeddingModel = MODEL_TEXT_EMBEDDING_3_SMALL;

        EmbeddingClient embeddingClient = openAiClient().GetEmbeddingClient(embeddingModel);
        try
        {
            var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
            return result.Value.ToFloats().ToArray().ToList();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Failed to generate embedding from OpenAI.", ex);
        }
    }

    public string responseText(string model, string system_prompt, string user_prompt)
    {
        return responseTextAsync(model, system_prompt, user_prompt).GetAwaiter().GetResult();
    }

    public T? responseJson<T>(string model, string system_prompt, string user_prompt, string json_schema = "")
    {
        return responseJsonAsync<T>(model, system_prompt, user_prompt, json_schema).GetAwaiter().GetResult();
    }

    public float[] embeddingForText(string text)
    {
        return embeddingForTextAsync(text).GetAwaiter().GetResult().ToArray();
    }

    /// <summary>
    /// Normalizes common JSON wrappers before deserialization.
    /// </summary>
    public static string normalizeJsonResponse(string jsonText)
    {
        jsonText = jsonText?.Trim() ?? string.Empty;
        if (jsonText.StartsWith("```", StringComparison.Ordinal))
        {
            jsonText = jsonText.Trim('`').Trim();
            if (jsonText.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                jsonText = jsonText[4..].Trim();
        }

        if (jsonText.Length == 0)
            throw new ApplicationException("Model returned an empty JSON response.");

        return jsonText;
    }

    private ChatClientAgent createAgent(string model, string system_prompt)
    {
        var modelId = string.IsNullOrWhiteSpace(model) ? MODEL_GPT5_MINI : model.Trim();
#pragma warning disable OPENAI001
        ResponsesClient responsesClient = openAiClient().GetResponsesClient();
        return responsesClient.AsAIAgent(modelId, system_prompt ?? string.Empty, "LLM");
#pragma warning restore OPENAI001
    }

    private OpenAIClient openAiClient()
    {
        var key = apiKey();
        if (string.IsNullOrWhiteSpace(key))
            throw new ApplicationException("OpenAI API key is not configured. Set appSettings.OPENAI_KEY or appSettings.OPENAI_API_KEY.");

        return new OpenAIClient(key);
    }

    private string apiKey()
    {
        var key = fw.config("OPENAI_KEY").toStr();
        if (string.IsNullOrWhiteSpace(key))
            key = fw.config("OPENAI_API_KEY").toStr();
        return key;
    }

    private static JsonElement parseJsonSchema(string json_schema)
    {
        if (string.IsNullOrWhiteSpace(json_schema))
            throw new ApplicationException("JSON schema is required for structured LLM responses.");

        try
        {
            using var doc = JsonDocument.Parse(json_schema);
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("Invalid JSON schema passed to LLM.responseJsonAsync.", ex);
        }
    }
}
