// Application Settings model. Keep application-specific helpers and presentation choices here.
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class Settings : FwSettings
{
    public const string ICAT_AI = "AI";
    public const string ICODE_TEST_EMAIL = "test_email";

    /// <summary>Shared runtime/editor defaults for application integrations; configured overrides win.</summary>
    public override string getDefault(string icode)
    {
        if (fw.config(icode) != null)
            return base.getDefault(icode);

        return icode switch
        {
            "mail.port" => "587",
            "mail.is_ssl" => "1",
            "AWS_CREDENTIAL_SOURCE" => "sdk",
            "ASSISTANT_MODEL" => LLM.MODEL_GPT5_MINI,
            "ASSISTANT_VECTOR_MODE" => RagChunks.VECTOR_MODE_AUTO,
            "ASSISTANT_RUN_TIMEOUT_SECONDS" => AssistantRuns.DEFAULT_RUN_TIMEOUT_SECONDS.toStr(),
            "ASSISTANT_MAX_FILES_PER_MESSAGE" => AssistantAppService.DEFAULT_MAX_FILES_PER_MESSAGE.toStr(),
            "ASSISTANT_MAX_INDEXED_FILE_BYTES" => DocumentEmbeddingService.DEFAULT_MAX_INDEXED_FILE_BYTES.toStr(),
            "ASSISTANT_MAX_INDEX_CHARS" => DocumentEmbeddingService.DEFAULT_MAX_INDEX_CHARS.toStr(),
            "ASSISTANT_MAX_INDEX_CHUNKS" => DocumentEmbeddingService.DEFAULT_MAX_INDEX_CHUNKS.toStr(),
            _ => "",
        };
    }

    private bool isGlobalsLoaded;

    /// <summary>
    /// Applies shared display overrides once per request from the application-cached snapshot.
    /// Unavailable storage leaves bootstrap defaults; user theme/mode preferences take precedence.
    /// </summary>
    public virtual void applyGlobals()
    {
        if (isGlobalsLoaded)
            return;

        isGlobalsLoaded = true;
        try
        {
            fw.G["SITE_NAME"] = read("SITE_NAME");
            fw.G["feedback_email"] = read("feedback_email");
            fw.G["support_email"] = read("support_email");
            fw.G["is_list_btn_left"] = readBool("is_list_btn_left");

            if (string.IsNullOrEmpty(fw.Session("ui_theme")))
                fw.G["ui_theme"] = readInt("ui_theme");
            if (string.IsNullOrEmpty(fw.Session("ui_mode")))
                fw.G["ui_mode"] = readInt("ui_mode");
        }
        catch (Exception)
        {
            // These presentation overrides are optional; operational Settings reads still report database failures.
            logger(LogLevel.WARN, "Site display settings are unavailable; using configured defaults.");
        }
    }

    /// <summary>Also refreshes this request's display globals on the next render.</summary>
    public override void invalidate()
    {
        base.invalidate();
        isGlobalsLoaded = false;
    }
}
