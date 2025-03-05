// AttLinks model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

using System.Collections;

namespace osafw;

public class AttLinks : FwModel
{
    public const string field_entity = "fwentities_id";

    public AttLinks() : base()
    {
        db_config = "";
        table_name = "att_links";
    }

    public override void init(FW fw)
    {
        base.init(fw);
        field_upd_time = "";
        field_upd_users_id = "";

        junction_model_main = fw.model<Att>();
        junction_field_main_id = "att_id";
        junction_model_linked = fw.model<FwEntities>();// Update to the model that you want to link to
        junction_field_linked_id = "item_id";
    }

    public virtual Hashtable oneByUK(int att_id, int fwentities_id, int item_id)
    {
        var where = new Hashtable()
        {
            {junction_field_main_id, att_id},
            {junction_field_linked_id, item_id},
            {field_entity, fwentities_id},
        };
        return db.row(table_name, where);
    }

    public virtual void deleteByAtt(int att_id)
    {
        var where = new Hashtable()
        {
            {junction_field_main_id, att_id},
        };
        db.del(table_name, where);
    }

    public virtual void setUnderUpdate(int fwentities_id, int item_id)
    {
        is_under_bulk_update = true;
        db.update(table_name, DB.h(field_status, STATUS_UNDER_UPDATE), DB.h(junction_field_linked_id, item_id, field_entity, fwentities_id));
    }

    public virtual void deleteUnderUpdate(int fwentities_id, int item_id)
    {
        var where = new Hashtable()
        {
            {junction_field_linked_id, item_id},
            {field_status, STATUS_UNDER_UPDATE},
        };
        db.del(table_name, where);
        is_under_bulk_update = false;
    }

    public virtual void updateJunction(string entity_icode, int item_id, Hashtable att_keys)
    {
        if (att_keys == null)
            return;

        Hashtable fields = [];
        Hashtable where = [];

        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        // set all rows as under update
        setUnderUpdate(fwentities_id, item_id);

        foreach (string key in att_keys.Keys)
        {
            var att_id = key.toInt();
            if (att_id == 0)
                continue; // skip non-id, ex prio_ID

            var item = oneByUK(att_id, fwentities_id, item_id);
            if (item.Count > 0)
            {
                // existing link
                fields = [];
                fields[field_status] = STATUS_ACTIVE;
                where = [];
                where["att_id"] = att_id;
                where["item_id"] = item_id;
                where["fwentities_id"] = fwentities_id;
                db.update(table_name, fields, where);
            }
            else
            {
                // new link
                fields = [];
                fields[junction_field_main_id] = att_id;
                fields[junction_field_linked_id] = item_id;
                fields[field_entity] = fwentities_id;
                fields[field_status] = STATUS_ACTIVE;
                fields[field_add_users_id] = fw.userId;
                db.insert(table_name, fields);
            }
        }

        // remove those who still not updated (so removed)
        deleteUnderUpdate(fwentities_id, item_id);
    }

}