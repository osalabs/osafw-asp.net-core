using System.Collections;

namespace osafw;

public static class LoginHelper
{
    public static void PrepareExtLoginParameters(FW fw, Hashtable ps)
    {
        var google_auth = (Hashtable)fw.config("GoogleAuth");

        if (Utils.f2bool(google_auth["Enabled"]))
        {
            ps["auth_external_logins"] = true;

            ps["auth_google_enabled"] = true;
            ps["auth_google_client_id"] = Utils.toStr(google_auth["ClientId"]);
            ps["auth_google_redirect_uri"] = Utils.toStr(google_auth["RedirectUri"]);
        }

        var microsoft_auth = (Hashtable)fw.config("MicrosoftAuth");

        if (Utils.f2bool(microsoft_auth["Enabled"]))
        {
            ps["auth_external_logins"] = true;

            ps["auth_microsoft_enabled"] = true;
            ps["auth_microsoft_client_id"] = Utils.toStr(microsoft_auth["ClientId"]);
            ps["auth_microsoft_redirect_uri"] = Utils.toStr(microsoft_auth["RedirectUri"]);
        }
    }
}