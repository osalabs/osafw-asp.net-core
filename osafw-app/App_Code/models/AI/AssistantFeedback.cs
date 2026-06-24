using System;

namespace osafw;

public class AssistantFeedback : FwModel<AssistantFeedback.Row>
{
    public const string TYPE_HELPFUL = "helpful";
    public const string TYPE_NOT_HELPFUL = "not_helpful";
    public const string TYPE_COMMENT = "comment";

    public class Row
    {
        public int id { get; set; }
        public int? assistant_threads_id { get; set; }
        public int? assistant_runs_id { get; set; }
        public int? assistant_messages_id { get; set; }
        public string feedback_type { get; set; } = string.Empty;
        public string comment { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int? add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int? upd_users_id { get; set; }
    }

    public AssistantFeedback()
    {
        table_name = "assistant_feedback";
        is_log_changes = false;
    }

    public int addFeedback(int usersId, int threadId, int runId, int messageId, string feedbackType, string comment)
    {
        return add(DB.h(
            "assistant_threads_id", threadId > 0 ? threadId : null,
            "assistant_runs_id", runId > 0 ? runId : null,
            "assistant_messages_id", messageId > 0 ? messageId : null,
            "feedback_type", string.IsNullOrWhiteSpace(feedbackType) ? TYPE_COMMENT : feedbackType.Trim(),
            "comment", comment ?? string.Empty,
            "add_users_id", usersId > 0 ? usersId : null
        ));
    }
}
