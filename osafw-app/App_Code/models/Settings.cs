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
