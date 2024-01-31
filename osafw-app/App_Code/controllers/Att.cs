// Att public downloads controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

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
        fw.redirect(fw.config("ASSETS_URL") + Att.IMGURL_0);
    }

    public void DownloadAction(int id = 0)
    {
        if (id == 0)
            throw new NotFoundException();
        string size = reqs("size");

        Hashtable item = model.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

        if ((string)item["is_s3"] == "1")
            model.redirectS3(item, size);

        model.transmitFile(id, size);
    }

    public void ShowAction(int id = 0)
    {
        if (id == 0)
            throw new NotFoundException();
        string size = reqs("size");
        bool is_preview = reqs("preview") == "1";

        Hashtable item = model.one(id);
        if (item.Count == 0)
            throw new NotFoundException();

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
                // if it's not an image and requested preview - return(redirect) std file image
                fw.redirect(fw.config("ASSETS_URL") + Att.IMGURL_FILE);
            }
        }
        else
        {
            model.transmitFile(id, size, "inline");
        }
    }
}