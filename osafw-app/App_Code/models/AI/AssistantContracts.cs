using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace osafw;

public sealed class AssistantResult
{
    public string title { get; set; } = string.Empty;
    public string explanation { get; set; } = string.Empty;
    public string information { get; set; } = string.Empty;
    public List<AssistantSource> sources { get; set; } = [];
    public double confidence { get; set; }

    [JsonIgnore]
    public static string JsonSchema => """
    {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "title": { "type": "string" },
        "explanation": { "type": "string" },
        "information": { "type": "string" },
        "confidence": { "type": "number" },
        "sources": {
          "type": "array",
          "items": {
            "type": "object",
            "additionalProperties": false,
            "properties": {
              "name": { "type": "string" },
              "url": { "type": "string" },
              "page": { "type": "integer" },
              "section": { "type": "string" },
              "filename": { "type": "string" },
              "file_url": { "type": "string" },
              "article_name": { "type": "string" },
              "article_url": { "type": "string" },
              "article_id": { "type": ["integer", "null"] },
              "att_id": { "type": ["integer", "null"] },
              "source_id": { "type": ["integer", "null"] },
              "chunk_id": { "type": ["integer", "null"] },
              "source_type": { "type": "string" },
              "score": { "type": ["number", "null"] }
            },
            "required": ["name", "url", "page", "section", "filename", "file_url", "article_name", "article_url", "article_id", "att_id", "source_id", "chunk_id", "source_type", "score"]
          }
        }
      },
      "required": ["title", "explanation", "information", "confidence", "sources"]
    }
    """;
}

public sealed class AssistantSource
{
    public string name { get; set; } = string.Empty;
    public string url { get; set; } = string.Empty;
    public int page { get; set; }
    public string section { get; set; } = string.Empty;
    public string filename { get; set; } = string.Empty;
    public string file_url { get; set; } = string.Empty;
    public string article_name { get; set; } = string.Empty;
    public string article_url { get; set; } = string.Empty;
    public int? article_id { get; set; }
    public int? att_id { get; set; }
    public int? source_id { get; set; }
    public int? chunk_id { get; set; }
    public string source_type { get; set; } = string.Empty;
    public double? score { get; set; }
}

public sealed class AssistantAttachmentDto
{
    public int id { get; set; }
    public string icode { get; set; } = string.Empty;
    public string iname { get; set; } = string.Empty;
    public string url { get; set; } = string.Empty;
    public string ext { get; set; } = string.Empty;
    public long fsize { get; set; }
    public bool is_image { get; set; }
    public bool is_indexed { get; set; }
    public string index_status { get; set; } = string.Empty;
}

public sealed class AssistantClarificationOption
{
    public string value { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
}

public sealed class AssistantClarificationField
{
    public string id { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string type { get; set; } = "text";
    public bool required { get; set; }
    public string placeholder { get; set; } = string.Empty;
    public List<AssistantClarificationOption> options { get; set; } = [];
}

public sealed class AssistantClarificationDto
{
    public string title { get; set; } = string.Empty;
    public string prompt { get; set; } = string.Empty;
    public string submit_label { get; set; } = "Continue";
    public List<AssistantClarificationField> fields { get; set; } = [];
}

public sealed class AssistantRunEventDto
{
    public int id { get; set; }
    public int assistant_runs_id { get; set; }
    public string event_type { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
    public string payload_json { get; set; } = string.Empty;
    public string add_time { get; set; } = string.Empty;
}

public sealed class AssistantMessageDto
{
    public int id { get; set; }
    public string role { get; set; } = string.Empty;
    public string message_type { get; set; } = string.Empty;
    public string content_markdown { get; set; } = string.Empty;
    public string add_time { get; set; } = string.Empty;
    public double? confidence { get; set; }
    public List<AssistantSource> sources { get; set; } = [];
    public AssistantClarificationDto? clarification { get; set; }
    public List<AssistantAttachmentDto> attachments { get; set; } = [];
}

public sealed class AssistantRunDto
{
    public int id { get; set; }
    public int assistant_threads_id { get; set; }
    public int assistant_messages_id { get; set; }
    public int status_id { get; set; }
    public string status { get; set; } = string.Empty;
    public string error_message { get; set; } = string.Empty;
    public string started_at { get; set; } = string.Empty;
    public string completed_at { get; set; } = string.Empty;
    public int duration_seconds { get; set; }
    public AssistantClarificationDto? clarification { get; set; }
}

public sealed class AssistantThreadDto
{
    public int id { get; set; }
    public string icode { get; set; } = string.Empty;
    public string iname { get; set; } = string.Empty;
    public string last_message_at { get; set; } = string.Empty;
    public int? last_run_status_id { get; set; }
    public string last_run_status { get; set; } = string.Empty;
    public bool is_shared { get; set; }
    public string share_url { get; set; } = string.Empty;
    public bool is_readonly { get; set; }
    public bool is_owner { get; set; }
    public List<AssistantMessageDto> messages { get; set; } = [];
    public List<AssistantRunEventDto> events { get; set; } = [];
    public AssistantRunDto? active_run { get; set; }
}

public sealed class AssistantThreadPreviewDto
{
    public int id { get; set; }
    public string iname { get; set; } = string.Empty;
    public string preview { get; set; } = string.Empty;
    public string last_message_at { get; set; } = string.Empty;
    public int? last_run_status_id { get; set; }
    public string last_run_status { get; set; } = string.Empty;
    public bool is_shared { get; set; }
}

public sealed class AssistantThreadShareDto
{
    public int thread_id { get; set; }
    public string icode { get; set; } = string.Empty;
    public string share_url { get; set; } = string.Empty;
}

public sealed class AssistantPollingResponse
{
    public AssistantThreadDto? thread { get; set; }
    public AssistantRunDto? run { get; set; }
    public List<AssistantMessageDto> messages { get; set; } = [];
    public List<AssistantRunEventDto> events { get; set; } = [];
    public int last_message_id { get; set; }
    public int last_event_id { get; set; }
}

public sealed class AssistantRuntimeStatus
{
    public bool enabled { get; set; }
    public bool tables_ready { get; set; }
    public bool openai_configured { get; set; }
    public bool worker_enabled { get; set; }
    public string message { get; set; } = string.Empty;
}
