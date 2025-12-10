// Permissions model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2023 Oleg Savchuk www.osalabs.com

using System;
using System.Collections;

namespace osafw;

public class Permissions : FwModel<Permissions.Row>
{
    public class Row
    {
        public int id { get; set; }
        public int? resources_id { get; set; }
        public string icode { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int prio { get; set; }
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public const string PERMISSION_LIST = "list";
    public const string PERMISSION_VIEW = "view";
    public const string PERMISSION_ADD = "add";
    public const string PERMISSION_EDIT = "edit";
    public const string PERMISSION_DELETE = "del";
    public const string PERMISSION_DELETE_PERMANENT = "del_perm";

    protected FwRow MAP_ACTIONS_PERMISSIONS = new()
    {
        { FW.ACTION_INDEX, PERMISSION_LIST },
        { FW.ACTION_SHOW, PERMISSION_VIEW },
        { FW.ACTION_SHOW_FORM + '/' + FW.ACTION_MORE_NEW, PERMISSION_ADD }, //to distinguish between add and edit
        { FW.ACTION_SHOW_FORM + '/' + FW.ACTION_MORE_EDIT, PERMISSION_EDIT }, // necessary for show edit form
        { FW.ACTION_SHOW_FORM, PERMISSION_EDIT },
        { FW.ACTION_SAVE + '/' + FW.ACTION_MORE_NEW, PERMISSION_ADD },// to distinguish save new and save existing
        { FW.ACTION_SAVE, PERMISSION_EDIT },
        { FW.ACTION_SAVE_MULTI, PERMISSION_EDIT },
        { FW.ACTION_SHOW_DELETE, PERMISSION_DELETE },
        { FW.ACTION_SHOW_DELETE + '/' + FW.ACTION_MORE_EDIT, PERMISSION_DELETE }, // necessary for show delete form
        { FW.ACTION_DELETE, PERMISSION_DELETE },
        //{ FW.ACTION_DELETE, PERMISSION_DELETE_PERMANENT } //TODO distinguish permanent delete
        { FW.ACTION_DELETE_RESTORE, PERMISSION_DELETE}, //if can delete permanently - can restore too
        { FW.ACTION_NEXT, PERMISSION_VIEW}, // next/prev links for view
        { FW.ACTION_AUTOCOMPLETE, PERMISSION_LIST}, // autocomplete - same as list
        { FW.ACTION_USER_VIEWS, PERMISSION_VIEW}, // if can view - can work with views too
        { FW.ACTION_SAVE_USER_VIEWS, PERMISSION_VIEW}, // if can view - can save views too
        { FW.ACTION_SAVE_SORT, PERMISSION_EDIT} //can edit - can sort too
    };

    public Permissions() : base()
    {
        db_config = "";
        table_name = "permissions";
        field_prio = "prio";
    }

    // map fw actions to permissions
    public string mapActionToPermission(string action, string action_more = "")
    {
        if (!Utils.isEmpty(action_more))
        {
            //if action_more is set - use it to find more granular permission
            //new, edit, delete
            action = action + "/" + action_more;
        }

        //find standard mapping
        var permission = MAP_ACTIONS_PERMISSIONS[action].toStr();
        if (!string.IsNullOrEmpty(permission))
            return permission;

        //if no standard permission found - return action as permission (custom permission)
        return action;
    }
}