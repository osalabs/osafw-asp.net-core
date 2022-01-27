// Att public downloads controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;

namespace osafw
{
    public class AttController : FwController
    {
        protected Att model = new();

        public override void init(FW fw)
        {
            base.init(fw);
            model.init(fw);
        }

        public void IndexAction()
        {
            fw.redirect(fw.config("ASSETS_URL") + "/img/0.gif");
        }

        public void DownloadAction(string form_id = "")
        {
            int id = Utils.f2int(form_id);
            if (id == 0)
                throw new ApplicationException("404 File Not Found");
            string size = reqs("size");

            Hashtable item = model.one(id);
            if ((string)item["is_s3"] == "1")
                model.redirectS3(item, size);

            model.transmitFile(Utils.f2int(form_id), size);
        }

        public void ShowAction(string form_id = "")
        {
            int id = Utils.f2int(form_id);
            if (id == 0)
                throw new ApplicationException("404 File Not Found");
            string size = reqs("size");
            bool is_preview = reqs("preview") == "1";

            Hashtable item = model.one(id);
            if ((string)item["is_s3"] == "1")
            {
                model.redirectS3(item, size);
                return;
            }

            if (is_preview)
            {
                if ((string)item["is_image"] == "1")
                {
                    model.transmitFile(id, size, "inline");
                }
                else
                {
                    // if it's not an image and requested preview - return std image
                    string filepath = fw.config("site_root") + "/img/att_file.png"; // TODO move to web.config or to model? and no need for transfer file - just redirect TODO
                    string ext = UploadUtils.getUploadFileExt(filepath);
                    fw.response.Headers.Add("Content-type", model.getMimeForExt(ext));
                    fw.response.SendFileAsync(filepath).Wait();

                }
            }
            else
            {
                model.transmitFile(id, size, "inline");
            }
        }
    }
}