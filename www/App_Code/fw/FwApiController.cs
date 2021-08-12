// Base API controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System;

namespace osafw
{
    public class FwApiController : FwController
    {
        // Public Shared Shadows route_default_action As String = "index" 'empty|index|show - calls IndexAction or ShowAction accordingly if no requested controller action found. If empty (default) - show template from /cur_controller/cur_action dir

        public override void init(FW fw)
        {
            base.init(fw);
        }

        protected virtual bool auth()
        {
            var result = false;

            if (fw.SessionBool("is_logged"))
                result = true;
            if (!result)
                throw new ApplicationException("API auth error");

            return result;
        }

        // send output seaders
        // and if auth requested - check authorization
        protected virtual void prepare(bool isAuth = true)
        {
            // logger(fw.req.Headers)

            var origin = "";
            if (!string.IsNullOrEmpty(fw.req.Headers["Origin"].ToString()))
                origin = fw.req.Headers["Origin"];
            else
            {
                // try referrer
                if (!string.IsNullOrEmpty(fw.req.Headers["Referer"].ToString()))
                {
                    var uri = new Uri(fw.req.Headers["Referer"]);
                    origin = uri.GetLeftPart(UriPartial.Authority);
                }
            }

            // logger(fw.config("hostname"))
            // logger("referer:")
            // logger(referrer)

            // validate referrer is same as our hostname
            if (string.IsNullOrEmpty(origin) || (origin != "http://" + fw.config("hostname") && origin != "https://" + fw.config("hostname") && origin != (string)fw.config("API_ALLOW_ORIGIN")))
                throw new ApplicationException("Invalid origin " + origin);

            // create headers
            fw.resp.Headers.Remove("Access-Control-Allow-Origin");
            fw.resp.Headers.Add("Access-Control-Allow-Origin", origin);

            fw.resp.Headers.Remove("Access-Control-Allow-Credentials");
            fw.resp.Headers.Add("Access-Control-Allow-Credentials", "true");

            fw.resp.Headers.Remove("Access-Control-Allow-Methods");
            fw.resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

            // check auth
            if (isAuth)
                this.auth();
        }
    }
}