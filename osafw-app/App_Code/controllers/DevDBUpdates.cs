// Demo Dynamic Admin controller
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class DevDBUpdatesController : FwDynamicController
{
    public static new int access_level = Users.ACL_SITEADMIN;

    protected FwUpdates model;

    public override void init(FW fw)
    {
        base.init(fw);

        base_url = "/Dev/DBUpdates";
        this.loadControllerConfig();
        model = model0 as FwUpdates;
        db = model.getDB(); // model-based controller works with model's db

        // Update list of updates 
        fw.model<FwUpdates>().parseUpdates();
        fw.G["IS_HAVE_DB_UPDATES"] = fw.model<FwUpdates>().getNotAppliedCount();
    }

    public override Hashtable IndexAction()
    {
        return base.IndexAction();
    }

    public override Hashtable SaveAction(int id = 0)
    {
        Hashtable ps = new Hashtable();
        string queries = "";
        if (id > 0)
        {
            var row = model.one(id);
            if (row.Count == 0)
            {
                throw new UserException("Update is not foud");
            }

            if (Utils.f2int(row["status"]) == FwUpdates.STATUS_APPLIED)
            {
                throw new UserException("Update was already applied");
            }

            queries = row["idesc"];
        }
        else
        {
            var views_file = fw.config("site_root") + @"\App_Data\sql\views.sql";
            queries = FW.getFileContent(views_file);
        }
        string[] asql = DB.splitMultiSQL(queries);
        var processedQueries = new ArrayList();

        var command = db.getConnection().CreateCommand();
        var transaction = db.getConnection().BeginTransaction();

        command.Connection = db.getConnection();
        command.Transaction = transaction;

        string error = "";
        try
        {
            foreach (string sqlone1 in asql)
            {
                var sqlone = sqlone1.Trim();
                if (sqlone.Length > 0)
                {
                    processedQueries.Add(DB.h("query", sqlone));
                    command.CommandText = sqlone;
                    command.ExecuteNonQuery();
                }
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollback_ex)
            {
                throw new UserException(rollback_ex.Message);
            }
        }
        
        ps["processedQueries"] = processedQueries;
        ps["error"] = error;
        ps["no_errors"] = string.IsNullOrEmpty(error);

        if (id > 0)
        {
            if (string.IsNullOrEmpty(error))
                model.markAsApplied(id);
            else
                model.markAsFailed(id, error);
        }

        return ps;
    }

    public override Hashtable AutocompleteAction()
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable DeleteAction(int id)
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable RestoreDeletedAction(int id)
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable SaveMultiAction()
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable SaveUserViewsAction()
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable ShowAction(int id = 0)
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override void ShowDeleteAction(int id)
    {
        throw new ApplicationException("Not Imlemented");
    }

    public override Hashtable ShowFormAction(int id = 0)
    {
        throw new ApplicationException("Not Imlemented");
    }


    public override void UserViewsAction(int id = 0)
    {
        throw new ApplicationException("Not Imlemented");
    }
}