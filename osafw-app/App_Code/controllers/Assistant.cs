using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections;
using System.ComponentModel;
using System.Text.Json;

namespace osafw;

// Define response format types
public sealed class AssistantResult
{
    public string title { get; set; } = string.Empty;
    public string explanation { get; set; } = string.Empty;
    public string sql { get; set; } = string.Empty;
    public string redirect_url { get; set; } = string.Empty;
}

/// <summary>
/// Allows the LLM to look‑up values in any FW model at run‑time.
/// </summary>
public sealed class LookupPlugin
{
    private readonly FW fw;

    public LookupPlugin(FW fw) => this.fw = fw;

    /// <summary>
    /// Search a lookup model and return the matching ID / iname pairs.
    /// </summary>
    /// <param name="model">Model name, e.g. "IncidentsLocations"</param>
    /// <param name="query">Free‑text search, e.g. "bathroom"</param>
    /// <returns>JSON array of objects { id: int, iname: string }</returns>
    [KernelFunction, Description("Search a lookup model and return id & iname pairs")]
    public ArrayList lookup(
        [Description("Model name, e.g. IncidentsLocations")] string model,
        [Description("Search phrase, e.g. bathroom")] string query)
    {
        //fw.logger("############# Assistant lookup model:", model, ", query:", query);
        return fw.model(model).listSelectOptionsAutocomplete(query);
    }
}

public class AssistantController : FwController
{
    public static new int access_level = Users.ACL_MEMBER;
    public static new string route_default_action = FW.ACTION_INDEX;

    public override void init(FW fw)
    {
        base.init(fw);

        is_readonly = fw.model<Users>().isReadOnly();

        base_url = "/Assistant";

        return_url = reqs("return_url");
        related_id = reqs("related_id");
        export_format = reqs("export");
    }
    public override void checkAccess()
    {
        //true - allow access to all members
    }

    public Hashtable IndexAction()
    {
        //todo show list of history
        Hashtable ps = [];
        Hashtable item = reqh("item");

        var userPrompt = fw.G["user_prompt"].toStr();

        if (!Utils.isEmpty(userPrompt))
        {
            ps["is_run"] = true;
            ps["user_prompt"] = userPrompt;

            var llm_sql = fw.G["llm_sql"].toStr();
            ps["llm_title"] = fw.G["llm_title"];
            ps["llm_explanation"] = fw.G["llm_explanation"];
            ps["llm_sql"] = llm_sql;
            ps["llm_form"] = fw.G["llm_form"];

            if (!string.IsNullOrEmpty(llm_sql))
            {
                ps["is_sql_result"] = true;

                // 6) Optionally run the SQL on your DB (be sure to sanitize or check carefully!)
                ArrayList rows = [];
                db.exec("BEGIN TRANSACTION");
                try
                {
                    rows = db.arrayp(llm_sql);
                    db.exec("ROLLBACK");//just always rollback
                }
                catch (Exception ex)
                {
                    logger(LogLevel.ERROR, "Assistant SQL error: " + ex.Message);
                    db.exec("ROLLBACK");
                    fw.flash("error", "Assistant produced insane SQL. Try again later or change your request");
                    fw.redirect(this.base_url);
                }

                var headers = new ArrayList();
                Utils.prepareRowsHeaders(rows, headers);

                ps["rows"] = rows;
                ps["headers"] = headers;
            }

        }

        ps["i"] = item;
        ps["user"] = fw.model<Users>().one(fw.userId);
        return ps;
    }

    public void ShowAction(string id = "")
    {
        var page_name = id.ToLower();

        string tpl_name = fw.G["PAGE_LAYOUT"].toStr();
        //override layout for specific pages - TODO control via Spages
        //if (page_name == "about")
        //    tpl_name = fw.config("PAGE_LAYOUT_PUBLIC").toStr();

        Hashtable ps = new();
        ps["hide_sidebar"] = true; // TODO control via Spages
        ps["page_name"] = page_name;

        fw.parser("/home/" + Utils.routeFixChars(page_name), tpl_name, ps);
    }

    public void SaveAction()
    {
        Hashtable ps = [];

        Hashtable item = reqh("item");
        string userPrompt = item["prompt"].toStr();
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            fw.flash("error", "No user input provided.");
            fw.redirect(this.base_url);
            return;
        }

        string apiKey = fw.config("OPENAI_API_KEY").toStr();
        //string modelId = "gpt-4.1";
        string modelId = "gpt-4.1-mini";
        var metaps = new Hashtable
        {
            { "current_time", DateTime.Now },
            { "users_id", fw.userId },
        };
        string systemMsg = fw.parsePage("/assistant", "system_msg.md", metaps);
        //logger("SYSTEM MESSAGE:", systemMsg);

        var builder = Kernel.CreateBuilder();
        //builder.Services.AddLogging(services => services.AddSystemdConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));
        builder.AddOpenAIChatCompletion(
            modelId,
            apiKey
        );
        Kernel kernel = builder.Build();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        //add plugins if any
        //kernel.Plugins.AddFromType<LightsPlugin>("Lights");
        //or kernel.ImportPluginFromType<LightsPlugin>();
        var lookupPlugin = kernel.ImportPluginFromObject(new LookupPlugin(fw), "lookup");
        //fw.logger("Tools in request:", lookupPlugin.Name, "=>", lookupPlugin.FunctionCount);

        // Enable planning
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(AssistantResult),
            //MaxTokens = 3000,
            // Temperature = 1.0,
            // TopP = 1.0,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
            {
                AllowParallelCalls = false,
                AllowConcurrentInvocation = false
            }),
        };

        // Create a history store the conversation
        var history = new ChatHistory();
        history.AddSystemMessage(systemMsg);
        history.AddUserMessage(userPrompt);
        //history.AddAssistantMessage("response text");

        AssistantResult? parsedResult = null;
        try
        {
            // non-streaming completion
            var resultTask = chatCompletionService.GetChatMessageContentsAsync(history, executionSettings, kernel);
            resultTask.Wait();

            var result = resultTask.Result[^1];
            var content = result.Content ?? string.Empty;
            OpenAI.Chat.ChatTokenUsage? usage = null;
            if (result.Metadata?.TryGetValue("Usage", out var usageObj) == true)
                usage = usageObj as OpenAI.Chat.ChatTokenUsage;

            if (usage != null)
                logger("LLM usage:", usage.InputTokenCount, " + ", usage.OutputTokenCount, " = ", usage.TotalTokenCount);
            logger("LLM response:", content);

            //sometimes content can contain multiple json strings, split by "}\n{" and take the last one 
            var json_strings = content.Split(["}\n{"], StringSplitOptions.RemoveEmptyEntries);
            if (json_strings.Length > 1)
            {
                content = json_strings[^1];
                content = "{" + content;
            }

            parsedResult = JsonSerializer.Deserialize<AssistantResult>(content);
            if (parsedResult != null)
                logger("AssistantResult:", parsedResult);
            else
                logger("AssistantResult parse failed");

            fw.model<FwActivityLogs>().addSimple(FwLogTypes.ICODE_ADDED, FwEntities.ICODE_ASSISTANT, 0, userPrompt, (Hashtable?)Utils.jsonDecode(content));
        }
        catch (Exception ex)
        {
            fw.logger(LogLevel.ERROR, "Exception in chatCompletion:", ex.Message);
            fw.flash("error", "Assistant having a hard day. Try again later.");
            fw.redirect(this.base_url);
        }

        // If it looks good, store it
        if (parsedResult != null)
        {

            // If redirect, read "redirect_url" from JSON, do your fw.redirect. If done, just break.
            if (!string.IsNullOrEmpty(parsedResult.redirect_url))
            {
                fw.redirect(parsedResult.redirect_url);
                return;
            }

            fw.G["llm_sql"] = parsedResult.sql;
            fw.G["llm_title"] = parsedResult.title;
            fw.G["llm_explanation"] = parsedResult.explanation;
            //fw.G["llm_form"] = parsedResult.html_form;
        }

        //fw.flash("info", "Assistant completed conversation. Last JSON=" + lastJSON);
        fw.G["user_prompt"] = userPrompt; // pass user prompt to the view
        fw.routeRedirect(FW.ACTION_INDEX);
    }

}