// Users model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

//if you use Roles - uncomment define isRoles here
//#define isRoles

using OtpNet;
using QRCoder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static BCrypt.Net.BCrypt;

namespace osafw;

public class Users : FwModel<Users.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string email { get; set; } = string.Empty;
        public string pwd { get; set; } = string.Empty;
        public int access_level { get; set; }
        public int is_readonly { get; set; }
        public string iname { get; set; } = string.Empty;
        public string fname { get; set; } = string.Empty;
        public string lname { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string address1 { get; set; } = string.Empty;
        public string address2 { get; set; } = string.Empty;
        public string city { get; set; } = string.Empty;
        public string state { get; set; } = string.Empty;
        public string zip { get; set; } = string.Empty;
        public string phone { get; set; } = string.Empty;
        public string lang { get; set; } = string.Empty;
        public int ui_theme { get; set; }
        public int ui_mode { get; set; }
        public int date_format { get; set; }
        public int time_format { get; set; }
        public string timezone { get; set; } = string.Empty;
        public string idesc { get; set; } = string.Empty;
        public int? att_id { get; set; }
        public DateTime? login_time { get; set; }
        public string pwd_reset { get; set; } = string.Empty;
        public DateTime? pwd_reset_time { get; set; }
        public string mfa_secret { get; set; } = string.Empty;
        public string mfa_recovery { get; set; } = string.Empty;
        public DateTime? mfa_added { get; set; }
        public string login { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    // ACL constants
    public const int ACL_VISITOR = 0; //non-logged visitor
    public const int ACL_MEMBER = 1; //min access level for users
    public const int ACL_EMPLOYEE = 50;
    public const int ACL_MANAGER = 80;
    public const int ACL_ADMIN = 90;
    public const int ACL_SITEADMIN = 100;

    public const string PERM_COOKIE_NAME = "osafw_perm";
    public const int PERM_COOKIE_DAYS = 356;

    public const int PWD_RESET_TOKEN_LEN = 50;

    private readonly string table_menu_items = "menu_items";
    private readonly string table_users_cookies = "users_cookies";

    public Users() : base()
    {
        table_name = "users";
        csv_export_fields = "id fname lname email add_time";
        csv_export_headers = "id,First Name,Last Name,Email,Registered";
    }

    #region standard one/add/update overrides
    public DBRow oneByEmail(string email)
    {
        FwRow where = [];
        where["email"] = email;
        return db.row(table_name, where);
    }

    public DBRow oneByLogin(string login)
    {
        return db.row(table_name, DB.h("login", login));
    }

    /// <summary>
    /// return full user name - First Name Last Name
    /// </summary>
    /// <param name="id">Object type because if upd_users_id could be null</param>
    /// <returns></returns>
    public override string iname(object? id)
    {
        string result = "";

        int iid = id.toInt();
        if (iid > 0)
        {
            var item = one(iid);
            result = item["fname"] + "  " + item["lname"];
        }

        return result;
    }

    // check if user exists for a given email
    public override bool isExists(object uniq_key, int not_id)
    {
        return isExistsByField(uniq_key, not_id, "email");
    }

    public override int add(FwRow item)
    {
        if (!item.ContainsKey("access_level"))
            item["access_level"] = Users.ACL_MEMBER;

        if (!item.ContainsKey("pwd"))
            item["pwd"] = Utils.getRandStr(8); // generate password
        item["pwd"] = this.hashPwd(item["pwd"].toStr());

        // set ui_theme/ui_mode form the config if not set
        if (!item.ContainsKey("ui_theme"))
            item["ui_theme"] = fw.config("ui_theme").toInt();
        if (!item.ContainsKey("ui_mode"))
            item["ui_mode"] = fw.config("ui_mode").toInt();

        // set default date/time format and timezone from the config if not set
        if (!item.ContainsKey("date_format"))
            item["date_format"] = fw.config("date_format").toInt();
        if (!item.ContainsKey("time_format"))
            item["time_format"] = fw.config("time_format").toInt();
        if (!item.ContainsKey("timezone"))
            item["timezone"] = fw.config("timezone");

        return base.add(item);
    }

    public override bool update(int id, FwRow item)
    {
        if (id == 0) return false;//no anonymous updates

        if (item.ContainsKey("pwd"))
            item["pwd"] = this.hashPwd(item["pwd"].toStr());
        return base.update(id, item);
    }

    protected override string getOrderBy()
    {
        return "fname, lname";
    }

    // return standard list of id,iname where status=0 order by iname
    public override DBList list(IList? statuses = null)
    {
        statuses ??= new FwList() { STATUS_ACTIVE };
        return base.list(statuses);
    }

    public override FwList listSelectOptions(FwRow? def = null)
    {
        string sql = "select id, fname+' '+lname as iname from " + db.qid(table_name) + " where status=@status order by " + getOrderBy();
        return db.arrayp(sql, DB.h("status", STATUS_ACTIVE));
    }
    #endregion

    #region Work with Passwords/MFA
    /// <summary>
    /// performs any required password cleaning (for now - just limit pwd length at 32 and trim)
    /// </summary>
    /// <param name="plain_pwd">non-encrypted plain pwd</param>
    /// <param name="trim_at">max length</param>
    /// <returns>clean plain pwd</returns>
    public string cleanPwd(string plain_pwd, int trim_at = 32)
    {
        return plain_pwd[..Math.Min(trim_at, plain_pwd.Length)].Trim();
    }

    /// <summary>
    /// generate password hash from plain password
    /// </summary>
    /// <param name="plain_pwd">plain pwd</param>
    /// <param name="trim_at">max length to trim plain pwd before hashing</param>
    /// <returns>hash using https://github.com/BcryptNet/bcrypt.net </returns>
    public string hashPwd(string plain_pwd, int trim_at = 32)
    {
        try
        {
            return EnhancedHashPassword(cleanPwd(plain_pwd, trim_at));
        }
        catch (Exception)
        {
        }
        return "";
    }

    /// <summary>
    /// return true if plain password has the same hash as provided
    /// </summary>
    /// <param name="plain_pwd">plain pwd from user input</param>
    /// <param name="pwd_hash">password hash previously generated by hashPwd</param>
    /// <returns></returns>
    public bool checkPwd(string plain_pwd, string pwd_hash, int trim_at = 32)
    {
        try
        {
            return EnhancedVerify(cleanPwd(plain_pwd, trim_at), pwd_hash);
        }
        catch (Exception)
        {
        }
        return false;
    }

    /// <summary>
    /// generate reset token, save to users and send pwd reset link to the user
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool sendPwdReset(int id)
    {
        var pwd_reset_token = Utils.getRandStr(PWD_RESET_TOKEN_LEN);

        FwRow item = new()
        {
            {"pwd_reset", this.hashPwd(pwd_reset_token, PWD_RESET_TOKEN_LEN)},
            {"pwd_reset_time", DB.NOW}
        };
        this.update(id, item);

        var user = this.one(id);
        user["pwd_reset_token"] = pwd_reset_token;

        return fw.sendEmailTpl(user["email"], "email_pwd.txt", user);
    }

    /// <summary>
    /// evaluate password's stength and return a score (>60 good, >80 strong)
    /// </summary>
    /// <param name="pwd"></param>
    /// <returns></returns>
    public double scorePwd(string pwd)
    {
        var result = 0;
        if (string.IsNullOrEmpty(pwd))
            return result;

        // award every unique letter until 5 repetitions
        FwRow chars = [];
        for (var i = 0; i <= pwd.Length - 1; i++)
        {
            var count = chars.ContainsKey(pwd[i]) ? chars[pwd[i]].toInt() : 0;
            count++;
            chars[pwd[i]] = count;
            result += (int)(5.0 / (double)count);
        }

        // bonus points for mixing it up
        FwRow vars = new()
        {
            {"digits",Regex.IsMatch(pwd, @"\d")},
            {"lower",Regex.IsMatch(pwd, "[a-z]")},
            {"upper",Regex.IsMatch(pwd, "[A-Z]")},
            {"other",Regex.IsMatch(pwd, @"\W")}
        };
        var ctr = 0;
        foreach (bool value in vars.Values)
        {
            if (value) ctr += 1;
        }
        result += (ctr - 1) * 10;

        // adjust for length
        result = (int)(Math.Log(pwd.Length) / Math.Log(8)) * result;

        return result;
    }

    /// <summary>
    /// generate a new MFA secret
    /// </summary>
    /// <returns></returns>
    internal string generateMFASecret()
    {
        return Base32Encoding.ToString(KeyGeneration.GenerateRandomKey());
    }

    public string generateMFAQRCode(string mfa_secret, string user = "user@company", string issuer = "osafw")
    {
        var uriString = new OtpUri(OtpType.Totp, mfa_secret, user, issuer).ToString();

        var IMG_SIZE = 5;
        return $"data:image/png;base64,{Convert.ToBase64String(PngByteQRCodeHelper.GetQRCode(uriString, QRCodeGenerator.ECCLevel.Q, IMG_SIZE))}";
    }

    /// <summary>
    /// check if code is valid against provided MFA secret
    /// </summary>
    /// <param name="mfa_secret"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    public bool isValidMFACode(string mfa_secret, string code)
    {
        if (string.IsNullOrEmpty(mfa_secret))
            return false;

        var totp = new Totp(Base32Encoding.ToBytes(mfa_secret));
        // Generate the current TOTP value from the secret and compare it to the user's value.
        return totp.VerifyTotp(code, out _, new VerificationWindow(2, 2)); // use 1,1 for stricter time check
    }

    /// <summary>
    /// check if code is valid against user's MFA secret
    /// </summary>
    /// <param name="id">users.id</param>
    /// <param name="code"></param>
    /// <returns></returns>
    public bool isValidMFA(int id, string code)
    {
        var user = this.one(id);
        return isValidMFACode(user["mfa_secret"] ?? "", code);
    }

    /// <summary>
    /// check if code is a MFA recovery code, if yes - remove that code from user's recovery codes
    /// </summary>
    /// <param name="id"></param>
    /// <param name="code"></param>
    /// <returns>true if code is a recovery code</returns>
    public bool checkMFARecovery(int id, string code)
    {
        var result = false;
        var user = this.one(id);
        var recovery_codes = user["mfa_recovery"].toStr().Split(' '); // space-separated hashed codes
        var new_recovery_codes = "";
        //split by space and check each code
        foreach (var recovery_code in recovery_codes)
        {
            if (checkPwd(code, recovery_code))
                result = true;
            else
                new_recovery_codes += recovery_code + " "; // not found codes - add to new list
        }

        if (result)
        {
            //if found - update user's recovery codes (as we removed matched one)
            var item = new FwRow();
            item["mfa_recovery"] = new_recovery_codes.Trim();
            this.update(id, item);
        }

        return result;
    }
    #endregion

    #region Login/Session
    /// <summary>
    /// reset session and fill with user info from id, log login activity, update login time and timezone if provided
    /// </summary>
    /// <param name="id"></param>
    /// <param name="timezone"></param>
    public void doLogin(int id, string timezone = "")
    {
        var context = fw.context;
        context?.Session.Clear();
        fw.Session("XSS", Utils.getRandStr(16));

        reloadSession(id);

        var ip = Utils.getIP(fw.context);
        fw.logActivity(FwLogTypes.ICODE_USERS_LOGIN, FwEntities.ICODE_USERS, id, "IP:" + ip);
        // update login and timezone
        FwRow fields = [];
        fields["login_time"] = DB.NOW;

        if (!string.IsNullOrEmpty(timezone))
        {
            var user = one(id);
            if (string.IsNullOrEmpty(user["timezone"]) || user["timezone"] == DateUtils.TZ_UTC)
            {
                fields["timezone"] = timezone;
                fw.Session("timezone", timezone);
            }
        }

        this.update(id, fields);

        //if Site Admin - check for pending db updates
        if (isAccessLevel(ACL_SITEADMIN))
        {
            fw.model<FwUpdates>().loadUpdates();

            fw.Session("FW_UPDATES_CTR", fw.model<FwUpdates>().getCountPending().toStr());
        }

    }

    public bool reloadSession(int id = 0)
    {
        if (id == 0)
            id = fw.userId;
        var user = one(id);

        fw.Session("user_id", id.toStr());
        fw.Session("login", user["email"]);
        fw.Session("access_level", user["access_level"]); //note, set as string
        fw.Session("lang", user["lang"]);
        fw.Session("ui_theme", user["ui_theme"]);
        fw.Session("ui_mode", user["ui_mode"]);
        fw.Session("date_format", user["date_format"]);
        fw.Session("time_format", user["time_format"]);
        fw.Session("timezone", user["timezone"]);
        // fw.SESSION("user", hU)

        var fname = user["fname"].Trim();
        var lname = user["lname"].Trim();
        if (!string.IsNullOrEmpty(fname) || !string.IsNullOrEmpty(lname))
            fw.Session("user_name", string.Join(" ", fname, lname).Trim());
        else
            fw.Session("user_name", user["email"]);

        var avatar_link = "";
        if (user["att_id"].toInt() > 0)
            avatar_link = fw.model<Att>().getUrl(user["att_id"].toInt(), "s");
        fw.Session("user_avatar_link", avatar_link);

        return true;
    }
    #endregion

    #region Access Control
    /// <summary>
    /// return true if currently logged user has at least minimum requested access level
    /// </summary>
    /// <param name="min_acl">minimum required access level</param>
    /// <returns></returns>
    public bool isAccessLevel(int min_acl)
    {
        return fw.userAccessLevel >= min_acl;
    }

    /// <summary>
    /// if currently logged user has at least minimum requested access level. Throw AuthException if user's acl is not enough
    /// </summary>
    /// <param name="min_acl">minimum required access level</param>
    public void checkAccessLevel(int min_acl)
    {
        if (!isAccessLevel(min_acl))
        {
            throw new AuthException();
        }
    }

    /// <summary>
    /// return true if user is ReadOnly user
    /// </summary>
    /// <param name="id">optional, if not passed - currently logged user checked</param>
    /// <returns></returns>
    public bool isReadOnly(int id = -1)
    {
        if (id == -1)
            id = fw.userId;

        if (id <= 0)
            return true; //if no user logged - readonly

        var user = one(id);
        return user["is_readonly"].toBool();
    }

    /// <summary>
    /// check if logged user is readonly, if yes - throws AuthEception
    /// </summary>
    /// <param name="id">optional, if not passed - currently logged user checked</param>
    /// <exception cref="AuthException"></exception>
    public void checkReadOnly(int id = -1)
    {
        if (isReadOnly(id))
            throw new AuthException();
    }

    /// <summary>
    /// return true if roles support enabled
    /// </summary>
    /// <returns></returns>
    public bool isRoles()
    {
#if isRoles
        return true;
#else
        return false;
#endif
    }

    //
    /// <summary>
    /// get all RBAC info for the user/recource
    /// </summary>
    /// <param name="users_id"></param>
    /// <param name="resource_icode"></param>
    /// <returns>hashtable with permissions keys:
    ///     list => true if user has list permission
    ///     view => true if user has view permission
    ///     add => true if user has add permission
    ///     edit => true if user has edit permission
    ///     del => true if user has delete permission
    /// </returns>
    public FwRow getRBAC(int? users_id = null, string? resource_icode = null)
    {
#if isRoles
        var result = new FwRow();

        int user_access_level;

        if (users_id == null)
        {
            users_id = fw.userId;
            user_access_level = fw.userAccessLevel;
        }
        else
        {
            var user = one(users_id);
            user_access_level = user["access_level"].toInt();
        }

        if (user_access_level == ACL_SITEADMIN)
        {
            //siteadmin doesn't have roles - has access to everything - just set all permissions to true
            //logger(LogLevel.TRACE, "RBAC info (SITEADMIN):");
            return allPermissions();
        }


        if (string.IsNullOrEmpty(resource_icode))
            resource_icode = fw.route.controller;

        // read resource id
        var resource = fw.model<Resources>().oneByIcode(resource_icode);
        if (resource.Count == 0)
            return result; //if no resource defined - return empty result - basically access denied
        var resources_id = resource["id"].toInt();

        //list all permissions for the resource and all user roles
        List<string> roles_ids;
        if (users_id == 0)
            //visitor
            roles_ids = [fw.model<Roles>().idVisitor().ToString()]; // visitor role for non-logged
        else
            roles_ids = fw.model<UsersRoles>().colLinkedIdsByMainId((int)users_id);

        // read all permissions for the resource and user's roles
        var rows = fw.model<RolesResourcesPermissions>().listByRolesResources(roles_ids, new int[] { resources_id });
        var permissions_ids = new List<string>();
        foreach (FwRow row in rows)
        {
            permissions_ids.Add(row["permissions_id"].toStr());
        }

        // now read all permissions by ids and set icodes to result
        var permissions_rows = fw.model<Permissions>().multi(permissions_ids);
        foreach (FwRow row in permissions_rows)
        {
            result[row["icode"]] = true;
        }
#else
        var result = allPermissions(); //if no Roles support - always allow
#endif

        logger(LogLevel.TRACE, "RBAC info:", result);
        return result;
    }

    /// <summary>
    /// return all allowed permissions as { permissions.icode => true }
    /// </summary>
    /// <returns></returns>
    public FwRow allPermissions()
    {
        var result = new FwRow();
#if isRoles
        var permissions = fw.model<Permissions>().list();
        foreach (FwRow permission in permissions)
        {
            result[permission["icode"]] = true;
        }
#else
        //if no Roles support - always allow all
        var icodes = Utils.qw("list view add edit del");
        foreach (var icode in icodes)
        {
            result[icode] = true;
        }
#endif
        return result;
    }

    /// <summary>
    /// shortcut for isAccessByRolesResourceAction with current user/controller
    /// </summary>
    /// <param name="resource_action"></param>
    /// <param name="resource_action_more"></param>
    /// <returns></returns>
    public bool isAccessByRolesAction(string resource_action, string resource_action_more = "")
    {
        return isAccessByRolesResourceAction(fw.userId, fw.route.controller, resource_action, resource_action_more);
    }

    /// <summary>
    /// check if currently logged user roles has access to controller/action
    /// </summary>
    /// <param name="users_id">usually currently logged user - fw.userId</param>
    /// <param name="resource_icode">resource code like controller name 'AdminUsers'</param>
    /// <param name="resource_action">resource action like controller's action 'Index' or '' </param>
    /// <param name="resource_action_more">optional additional action string, usually route.action_more to help distinguish sub-actions</param>
    /// <returns></returns>
    public bool isAccessByRolesResourceAction(int users_id, string resource_icode, string resource_action, string resource_action_more = "", FwRow? access_actions_to_permissions = null)
    {
        logger("isAccessByRolesResourceAction", DB.h("users_id", users_id, "resource_icode", resource_icode, "resource_action", resource_action, "resource_action_more", resource_action_more));
#if isRoles
        // determine permission by resource action
        var permission_icode = fw.model<Permissions>().mapActionToPermission(resource_action, resource_action_more);
        logger("permission_icode:", permission_icode);

        if (access_actions_to_permissions != null)
        {
            //check if we have controller's permission's override for the action
            if (access_actions_to_permissions.ContainsKey(permission_icode))
                permission_icode = access_actions_to_permissions[permission_icode].toStr();
        }

        var result = isAccessByRolesResourcePermission(users_id, resource_icode, permission_icode);
        if (!result)
            logger(LogLevel.DEBUG, "Access by Roles denied", new FwRow {
                {"resource_icode", resource_icode },
                {"resource_action", resource_action },
                {"resource_action_more", resource_action_more },
                {"permission_icode", permission_icode },
                {"access_actions_to_permissions", access_actions_to_permissions },
            });
#else
        var result = true; //if no Roles support - always allow
#endif

        return result;
    }

    /// <summary>
    /// check if currently logged user roles has access to resource with specific permission
    /// </summary>
    /// <param name="users_id"></param>
    /// <param name="resource_icode"></param>
    /// <param name="permission_icode"></param>
    /// <returns></returns>
    public bool isAccessByRolesResourcePermission(int users_id, string resource_icode, string permission_icode)
    {
#if isRoles
        // read resource id
        var resource = fw.model<Resources>().oneByIcode(resource_icode);
        if (resource.Count == 0)
            return false; //if no resource defined - access denied
        var resources_id = resource["id"].toInt();

        var permission = fw.model<Permissions>().oneByIcode(permission_icode);
        if (permission.Count == 0)
            return false; //if no permission defined - access denied
        var permissions_id = permission["id"].toInt();

        // read all roles for user
        List<string> roles_ids;
        if (users_id == 0)
            roles_ids = [fw.model<Roles>().idVisitor().ToString()]; // visitor role for non-logged
        else
        {
            var user = one(users_id);
            if (user["access_level"].toInt() == ACL_SITEADMIN)
            {
                //siteadmin doesn't have roles - has access to everything
                return true;
            }
            else
            {
                roles_ids = fw.model<UsersRoles>().colLinkedIdsByMainId(users_id); // logged user roles
            }
        }


        // check if any of user's roles has access to resource/permission
        var result = fw.model<RolesResourcesPermissions>().isExistsByResourcePermissionRoles(resources_id, permissions_id, roles_ids);
        if (!result)
            logger(LogLevel.DEBUG, "Access by Roles denied", DB.h("resource_icode", resource_icode, "permission_icode", permission_icode));
#else
        var result = true; //if no Roles support - always allow
#endif
        return result;
    }

    // load list of resources user can see (have list permission) per RBAC and save into fw.G[rbac_menu]
    // use in sidebar menu as <~imenu if="GLOBAL[rbac_menu][AdminUsers]" inline>...</~imenu>
    public void loadRBACMenu()
    {
#if isRoles
        //check cache
        var cache_key = "rbac_menu#" + fw.userId;
        var cache_key_time = "rbac_menu_time#" + fw.userId;
        var rbac_menu = (DBRow)FwCache.getValue(cache_key);
        if (rbac_menu != null)
        {
            //check if time is earlier than roles_resources_permissions_updated
            var cache_time = (DateTime?)FwCache.getValue(cache_key_time);
            var roles_resources_permissions_updated = (DateTime?)FwCache.getValue(RolesResourcesPermissions.CACHE_KEY_UPDATED); // if null - then roles not changed recently
            if (roles_resources_permissions_updated == null || cache_time != null && cache_time >= roles_resources_permissions_updated)
            {
                fw.G["rbac_menu"] = rbac_menu; // CACHE HIT
                return;
            }
        }

        //not in cache - read from db
        List<string> res_icodes;
        if (isSiteAdmin())
        {
            //siteadmin doesn't have roles - has access to everything
            res_icodes = fw.model<Resources>().colIcodes();
        }
        else
        {
            // read all roles for user
            List<string> roles_ids = fw.model<UsersRoles>().colLinkedIdsByMainId(fw.userId);
            // read all resources user has list permission
            var list_permission = fw.model<Permissions>().oneByIcode(Permissions.PERMISSION_LIST);
            var rrps = fw.model<RolesResourcesPermissions>().listByRolesPermissions(roles_ids, new int[] { list_permission["id"].toInt() });

            // read all resources user has list permission
            var resources_ids = new List<int>();
            foreach (FwRow rrp in rrps)
            {
                resources_ids.Add(rrp["resources_id"].toInt());
            }
            res_icodes = fw.model<Resources>().colIcodes(resources_ids);
        }

        //convert res_icodes into key => 1
        rbac_menu = [];
        foreach (var icode in res_icodes)
            rbac_menu[icode] = "1";

        FwCache.setValue(cache_key, rbac_menu, 1800); // cache for 30 minutes
        FwCache.setValue(cache_key_time, DateTime.Now);

        fw.G["rbac_menu"] = rbac_menu;
#endif
    }

    //shortcut to avoid calling UsersRoles directly
    public FwList listLinkedRoles(int users_id)
    {
#if isRoles
        return fw.model<UsersRoles>().listLinkedByMainId(users_id);
#else
        return [];
#endif
    }

    //shortcut to avoid calling UsersRoles directly
    public void updateLinkedRoles(int users_id, FwRow linked_keys)
    {
#if isRoles
        fw.model<UsersRoles>().updateJunctionByMainId(users_id, linked_keys);
#endif
    }

    // return list of icodes for resources user has access to with a "list" permission
    // i.e. if user has list permission to a resource - it should be accessible via menu
    public List<string> icodesAccessibleResources(int users_id)
    {
        var result = new List<string>();

#if isRoles
        var p = new FwRow
        {
            { "icode", Permissions.PERMISSION_LIST },
            { "users_id", users_id }
        };
        var roles_sql = "";

        if (users_id == 0)
        {
            //this is visitor - use just one specific role
            var role_visitor_id = fw.model<Roles>().idVisitor();
            p["visitor_role_id"] = role_visitor_id;
            roles_sql = $"@visitor_role_id";
        }
        else
        {
            //if Site Admin - has access to all resources
            if (one(users_id)["access_level"].toInt() == ACL_SITEADMIN)
                return db.colp($"select icode from {fw.model<Resources>().table_name}");

            //get all roles for the known user
            roles_sql = $"select roles_id from {fw.model<UsersRoles>().table_name} where users_id=@users_id";
        }

        result = db.colp($@"with rids as (
                        select resources_id 
                        from {fw.model<RolesResourcesPermissions>().table_name}
                        where permissions_id in (select id from {fw.model<Permissions>().table_name} where icode=@icode)
                          and roles_id in ({roles_sql})
                        )

                        select icode from {fw.model<Resources>().table_name} r, rids 
                         where r.id=rids.resources_id", p);
#endif

        return result;
    }

    /// <summary>
    /// shortcut to check if currently logged user is a Site Admin
    /// </summary>
    /// <returns></returns>
    public bool isSiteAdmin()
    {
        return isAccessLevel(ACL_SITEADMIN);
    }
    #endregion

    #region Permanent Login Cookies
    public string createPermCookie(int id)
    {
        string cookieId = Utils.getRandStr(64);
        string hashed = Utils.sha256(cookieId);
        var fields = DB.h("cookie_id", hashed, "users_id", id);
        db.updateOrInsert(table_users_cookies, fields, DB.h("users_id", id));

        Utils.createCookie(fw, PERM_COOKIE_NAME, cookieId, 60 * 60 * 24 * PERM_COOKIE_DAYS);

        return cookieId;
    }

    public bool checkPermanentLogin()
    {
        var cookieId = Utils.getCookie(fw, PERM_COOKIE_NAME);
        if (!string.IsNullOrEmpty(cookieId))
        {
            var hashed = Utils.sha256(cookieId);
            DBRow row = db.row(table_users_cookies, DB.h("cookie_id", hashed));
            if (row.Count > 0)
            {
                doLogin(row["users_id"].toInt());
                return true;
            }
            else
            {
                Utils.deleteCookie(fw, PERM_COOKIE_NAME);
            }
        }
        return false;
    }

    public void removePermCookie(int id)
    {
        Utils.deleteCookie(fw, PERM_COOKIE_NAME);
        db.del(table_users_cookies, DB.h("users_id", id));
        db.del(table_users_cookies, DB.h("add_time", db.opLE(DateTime.Now.AddYears(-1)))); // also cleanup old records (i.e. force re-login after a year)
    }
    #endregion

    public void loadMenuItems()
    {
        if (FwCache.getValue("menu_items") is not FwList menu_items)
        {
            // read main menu items for sidebar
            menu_items = db.array(table_menu_items, DB.h("status", STATUS_ACTIVE), "iname");
            FwCache.setValue("menu_items", menu_items);
        }

        // only Menu items user can see per ACL
        var users_acl = fw.userAccessLevel;
        FwList result = [];
        foreach (FwRow item in menu_items)
        {
            if (item["access_level"].toInt() <= users_acl)
                result.Add(item);
        }

        fw.G["menu_items"] = result;
    }
}