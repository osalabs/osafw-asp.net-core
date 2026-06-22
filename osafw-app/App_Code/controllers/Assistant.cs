using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw;

public class AssistantController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;
    public static new string route_default_action = FW.ACTION_INDEX;

    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/Assistant";
    }

    /// <summary>
    /// Allows all authenticated members to use the assistant without requiring a per-app RBAC resource.
    /// </summary>
    public override void checkAccess()
    {
        if (fw.userAccessLevel < access_level)
            throw new AuthException("Bad access - Not authorized");
    }

    /// <summary>
    /// Renders the threaded read-only assistant shell and bootstraps current history/thread data.
    /// </summary>
    public FwDict IndexAction()
    {
        var service = new AssistantAppService(fw);
        var status = service.RuntimeStatus();
        var history = service.ListHistory(fw.userId);
        AssistantThreadDto? thread = null;

        string share = reqs("share").Trim();
        int threadId = reqi("thread_id");
        if (string.IsNullOrWhiteSpace(share))
            threadId = threadId > 0 ? threadId : reqi("id");

        if (status.tables_ready)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(share))
                    thread = service.GetSharedThread(share, fw.userId);
                else if (threadId > 0)
                    thread = service.GetThread(threadId, fw.userId);
            }
            catch (Exception ex) when (ex is AuthException || ex is UserException || ex is NotFoundException)
            {
                fw.flash("error", ex.Message);
            }
        }

        return new FwDict
        {
            ["assistant_status_json"] = Utils.jsonEncode(status),
            ["assistant_history_json"] = Utils.jsonEncode(history),
            ["assistant_thread_json"] = thread == null ? "null" : Utils.jsonEncode(thread),
            ["assistant_status_message"] = status.message,
            ["is_assistant_available"] = status.enabled && status.tables_ready && status.openai_configured && status.worker_enabled,
            ["is_assistant_enabled"] = status.enabled,
            ["is_tables_ready"] = status.tables_ready,
            ["is_openai_configured"] = status.openai_configured,
            ["thread_id"] = thread?.id ?? 0,
            ["share"] = share,
        };
    }

    /// <summary>
    /// Queues a user turn with optional files and returns the updated thread state for polling.
    /// </summary>
    public FwDict? SaveAction(int id = 0)
    {
        enforcePost();

        int threadId = id > 0 ? id : reqi("thread_id");
        string prompt = reqs("prompt").Trim();
        var item = reqh("item");
        if (string.IsNullOrWhiteSpace(prompt))
            prompt = item["prompt"].toStr().Trim();

        FwDict clarification = reqh("clarification");
        var files = getPostedFiles();

        try
        {
            var service = new AssistantAppService(fw);
            var result = service.CreateOrContinueTurnAsync(
                fw.userId,
                threadId,
                prompt,
                clarification.Count > 0 ? clarification : null,
                files
            ).GetAwaiter().GetResult();

            if (fw.isJsonExpected())
            {
                return jsonResponse(new FwDict
                {
                    ["thread"] = result.thread,
                    ["message"] = result.message,
                    ["run"] = result.run,
                });
            }

            fw.redirect(base_url + "?thread_id=" + result.thread.id);
            return null;
        }
        catch (UserException ex) when (!fw.isJsonExpected())
        {
            fw.flash("error", ex.Message);
            fw.redirect(base_url + (threadId > 0 ? "?thread_id=" + threadId : string.Empty));
            return null;
        }
    }

    /// <summary>
    /// Returns one owned assistant thread as JSON.
    /// </summary>
    public FwDict ThreadAction(int id)
    {
        var thread = new AssistantAppService(fw).GetThread(id, fw.userId);
        return jsonResponse(new FwDict { ["thread"] = thread });
    }

    /// <summary>
    /// Returns messages and run events newer than the caller's known cursors.
    /// </summary>
    public FwDict PollAction(int id)
    {
        var service = new AssistantAppService(fw);
        var response = service.PollThread(
            id,
            fw.userId,
            reqs("share"),
            reqi("last_message_id"),
            reqi("last_event_id")
        );
        return jsonResponse(new FwDict { ["poll"] = response });
    }

    /// <summary>
    /// Returns current user's assistant thread history.
    /// </summary>
    public FwDict HistoryAction()
    {
        var items = new AssistantAppService(fw).ListHistory(fw.userId, reqs("q"));
        return jsonResponse(new FwDict { ["items"] = items });
    }

    /// <summary>
    /// Creates or returns the share URL for an owned thread.
    /// </summary>
    public FwDict ShareAction(int id)
    {
        enforcePost();
        var share = new AssistantAppService(fw).EnsureSharedThread(fw.userId, id);
        return jsonResponse(new FwDict { ["share"] = share });
    }

    /// <summary>
    /// Queues a fresh run for the latest user message without duplicating that message.
    /// </summary>
    public FwDict RetryAction(int id)
    {
        enforcePost();
        var result = new AssistantAppService(fw).RetryLastResponse(fw.userId, id);
        return jsonResponse(new FwDict
        {
            ["thread"] = result.thread,
            ["run"] = result.run,
        });
    }

    /// <summary>
    /// Stores review feedback for a run/message without mutating knowledge base content.
    /// </summary>
    public FwDict FeedbackAction()
    {
        enforcePost();
        new AssistantAppService(fw).SubmitFeedback(
            fw.userId,
            reqi("thread_id"),
            reqi("run_id"),
            reqi("message_id"),
            reqs("feedback_type"),
            reqs("comment")
        );
        return jsonResponse(new FwDict { ["success"] = true });
    }

    private List<IFormFile> getPostedFiles()
    {
        var requestFiles = fw.request?.Form?.Files;
        if (requestFiles == null || requestFiles.Count == 0)
            return [];

        return requestFiles.Where(static file => file.Length > 0).ToList();
    }

    private static FwDict jsonResponse(FwDict payload)
    {
        return new FwDict { ["_json"] = payload };
    }
}
