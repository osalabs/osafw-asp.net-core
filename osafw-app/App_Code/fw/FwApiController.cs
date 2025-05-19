// Base API controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw;

public class FwApiController : FwController
{
    public override void init(FW fw)
    {
        base.init(fw);
    }

    protected virtual bool auth()
    {
        var result = false;
        string x_api_key = fw.request.Headers["X-API-Key"];
        var api_key = fw.config()["API_KEY"] as string;

        //authorize if user logged OR API_KEY configured and matches
        if (fw.isLogged || !string.IsNullOrEmpty(api_key) && x_api_key == api_key)
            result = true;

        if (!result)
        {
            fw.response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
            throw new AuthException("API auth error");
        }

        return result;
    }

    // send output seaders
    // and if auth requested - check authorization
    protected virtual void prepare(bool isAuth = true)
    {
        // logger(fw.req.Headers)

        var origin = "";
        if (!string.IsNullOrEmpty(fw.request.Headers.Origin.ToString()))
            origin = fw.request.Headers.Origin;
        else
        {
            // try referrer
            if (!string.IsNullOrEmpty(fw.request.Headers.Referer.ToString()))
            {
                var uri = new Uri(fw.request.Headers.Referer);
                origin = uri.GetLeftPart(UriPartial.Authority);
            }
        }

        // logger(fw.config("hostname"))
        // logger("referer:")
        // logger(referrer)

        // validate referrer is same as our hostname
        if (string.IsNullOrEmpty(origin) || (origin != "http://" + fw.config("hostname") && origin != "https://" + fw.config("hostname") && origin != (string)fw.config("API_ALLOW_ORIGIN")))
            throw new AuthException("Invalid origin " + origin);

        // create headers
        fw.response.Headers.AccessControlAllowOrigin = origin;
        fw.response.Headers.AccessControlAllowCredentials = "true";
        fw.response.Headers.AccessControlAllowMethods = "GET, POST, PUT, DELETE, OPTIONS";

        // check auth
        if (isAuth)
            this.auth();
    }
}