// Log Types model class
// store types of log for activity_logs table
// there are 2 types of logs:
// - system logs - auto-generated by fw
//   - example: added/updated/deleted (i.e. changes to entity records)
// - user logs - user-generated
//   - example: user comments, user events
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2024 Oleg Savchuk www.osalabs.com

namespace osafw;

public class FwLogTypes : FwModel
{
    public const int ITYPE_SYSTEM = 0;
    public const int ITYPE_USER = 10;

    public const string ICODE_ADDED = "added";
    public const string ICODE_UPDATED = "updated";
    public const string ICODE_DELETED = "deleted";
    public const string ICODE_COMMENT = "comment";
    //users login audit realted
    public const string ICODE_USERS_SIMULATE = "simulate";
    public const string ICODE_USERS_LOGIN = "login";
    public const string ICODE_USERS_LOGIN_FAIL = "login_fail";
    public const string ICODE_USERS_LOGOFF = "logoff";
    public const string ICODE_USERS_CHPWD = "chpwd";

    public FwLogTypes() : base()
    {
        table_name = "log_types";
    }
}