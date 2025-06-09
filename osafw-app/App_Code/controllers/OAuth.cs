// OAuth controller
// Allows login via an external OpenID Connect provider (Google, Microsoft, etc.)
// Only one provider should be configured in appsettings under `OAuth`
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace osafw;

public class OAuthController : FwController
{
    public override void init(FW fw)
    {
        base.init(fw);
        base_url = "/OAuth";
    }

    // redirect user to provider authorization url
    public void IndexAction()
    {
        Hashtable oauth = (Hashtable)fw.config("OAuth");
        string clientId = oauth["ClientId"].toStr();
        string authority = oauth["Authority"].toStr();
        string redirectUri = oauth["RedirectUri"].toStr();
        string scope = oauth["Scopes"].toStr("openid profile email");

        string nonce = Utils.uuid();
        string state = Utils.uuid();
        fw.Session("oauth_nonce", nonce);
        fw.Session("oauth_state", state);

        string confUrl = authority.TrimEnd('/') + "/.well-known/openid-configuration";
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(confUrl, new OpenIdConnectConfigurationRetriever());
        var t = Task.Run(() => configManager.GetConfigurationAsync());
        t.Wait();
        var config = t.Result;

        var authUrl = config.AuthorizationEndpoint
            + "?scope=" + Utils.urlescape(scope)
            + "&response_type=id_token"
            + "&response_mode=form_post"
            + "&client_id=" + Utils.urlescape(clientId)
            + "&redirect_uri=" + Utils.urlescape(redirectUri)
            + "&state=" + Utils.urlescape(state)
            + "&nonce=" + Utils.urlescape(nonce);

        fw.redirect(authUrl);
    }

    // callback from provider after authentication
    public void SaveAction(string form_id = "")
    {
        if (!string.IsNullOrEmpty(reqs("error")))
        {
            logger(LogLevel.ERROR, "OAuth error=", reqs("error"), ", description=", reqs("error_description"));
            fw.flash("error", reqs("error_description"));
            fw.redirect("/Login");
            return;
        }

        if (fw.Session("oauth_state").toStr() != reqs("state"))
        {
            logger(LogLevel.WARN, "state in session [" + fw.Session("oauth_state") + "] <> state [" + reqs("state") + "]");
            fw.redirect(base_url);
            return;
        }

        string id_token = reqs("id_token");
        Hashtable oauth = (Hashtable)fw.config("OAuth");
        string clientId = oauth["ClientId"].toStr();
        string authority = oauth["Authority"].toStr();
        string nonce_in_session = fw.Session("oauth_nonce").toStr();

        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        string confUrl = authority.TrimEnd('/') + "/.well-known/openid-configuration";
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(confUrl, new OpenIdConnectConfigurationRetriever());
        var t = Task.Run(() => configManager.GetConfigurationAsync());
        t.Wait();
        var config = t.Result;

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        TokenValidationParameters validationParameters = new()
        {
            ValidAudience = clientId,
            ValidateIssuer = false,
            IssuerSigningKeys = config.SigningKeys
        };
        SecurityToken validatedToken;
        var claims = handler.ValidateToken(id_token, validationParameters, out validatedToken);

        var nonceClaim = claims.FindFirst("nonce");
        if (nonceClaim == null || nonceClaim.Value != nonce_in_session)
            throw new ApplicationException("OAuth error - Bad Authorization");

        string email = claims.FindFirst("email")?.Value ?? claims.FindFirst("preferred_username")?.Value ?? "";
        string name = claims.FindFirst("name")?.Value ?? email;
        if (string.IsNullOrEmpty(email))
            throw new ApplicationException("Cannot find user - Empty email");

        var user = fw.model<Users>().oneByEmail(email);
        if (user.Count > 0 && Utils.f2int(user["status"]) == Users.STATUS_ACTIVE)
        {
            fw.model<Users>().doLogin(Utils.f2int(user["id"]));
            fw.redirect((string)fw.config("LOGGED_DEFAULT_URL"));
        }
        else
        {
            fw.flash("error", "It looks Like you do Not have an Impacts DB account. Please contact your site administrator.");
            fw.redirect("/Login");
        }
    }
}
