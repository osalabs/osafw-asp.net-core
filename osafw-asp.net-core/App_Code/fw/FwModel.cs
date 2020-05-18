using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace osafw_asp.net_core.fw
{
    public class FwModel : IDisposable
    {
        const int STATUS_ACTIVE = 0;
        const int STATUS_DELETED = 127;

        protected FW fw;
        protected DB db;
        protected string db_config = ""; // if empty(default) - fw.db used, otherwise - new db connection created based on this config name

        public string table_name = ""; // must be assigned in child class
        public string csv_export_fields = ""; // all or Utils.qw format
        public string csv_export_headers = ""; // comma-separated format

        public string field_id = "id"; // default primary key name
        public string field_iname = "iname";

        // default field names. If you override it and make empty - automatic processing disabled
        public string field_status = "status";
        public string field_add_users_id = "add_users_id";
        public string field_upd_users_id = "upd_users_id";
        public string field_upd_time = "upd_time";
        public bool is_normalize_names = false; // if true - Utils.name2fw() will be called for all fetched rows to normalize names (no spaces or special chars)

        protected FwModel(FW fw = null) {
            if (fw == null) {
                this.fw = fw;
                this.db = fw.db;
            }
        }

        public virtual void init(FW fw) {
            this.fw = fw;
            if (db_config != "") {
                //db = new DB(fw, fw.config("db")(db_config), db_config);
            } else {
                db = fw.db;
            }
        }

        public virtual DB getDB()
        {
            return db;
        }

        public virtual Hashtable one(int id)
        {
            Hashtable item = (Hashtable)fw.cache.getRequestValue("fwmodel_one_" + table_name + "#" + id);
            if (item == null)
            {
                Hashtable where = new Hashtable();
                where[field_id] = id;
                item = db.row(table_name, where);
                normalizeNames(item); // TODO uncomment after port method
                fw.cache.setRequestValue("fwmodel_one_" + table_name + "#" + id, item);
            }
            return item;
        }

        // add renamed fields For template engine - spaces and special chars replaced With "_" and other normalizations
        public void normalizeNames(Hashtable row)
        {
            if (!is_normalize_names) return;
            foreach (String key in new ArrayList(row.Keys)) // static copy of row keys to avoid loop issues
            {
                row[Utils.name2fw(key)] = row[key];
            }
            if (field_id != "" && !row.ContainsKey("id")) row["id"] = row[field_id];
        }

        public void normalizeNames(ArrayList rows)
        {
            if (!is_normalize_names) return;
            foreach (Hashtable row in rows)
            {
                normalizeNames(row);
            }
        }

        public virtual String iname(int id)
        {
            Hashtable row = one(id);
            return (String)row[field_iname];
        }
        public virtual String iname(Object id)
        {
            String result = "";
            if (Utils.f2int(id) > 0)
            {
                result = iname(Utils.f2int(id));
            }
            return result;
        }

        // return standard list of id,iname where status=0 order by iname
        public virtual ArrayList list()
        {
            Hashtable where = new Hashtable();
            if (field_status != "")
            {
                where[field_status] = STATUS_ACTIVE;
            }
            return db.array(table_name, where, field_iname);
        }
        /*
'override if id/iname differs in table
'params - to use - override in your model
Public Overridable Function listSelectOptions(Optional params As Object = Nothing) As ArrayList
Dim where As New Hashtable
If field_status > "" Then where(field_status) = STATUS_ACTIVE

Dim select_fields As New ArrayList From {
        New Hashtable From {{"field", field_id}, {"alias", "id"}},
        New Hashtable From {{"field", field_iname}, {"alias", "iname"}}
    }
Return db.array(table_name, where, db.q_ident(field_iname), select_fields)
End Function

'return count of all non-deleted
Public Function getCount() As Integer
Dim where As New Hashtable
If field_status > "" Then where(field_status) = db.opNOT(STATUS_DELETED)
Return db.value(table_name, where, "count(*)")
End Function

'just return first row by iname field (you may want to make it unique)
Public Overridable Function oneByIname(iname As String) As Hashtable
Dim where As Hashtable = New Hashtable
where(field_iname) = iname
Return db.row(table_name, where)
End Function

'check if item exists for a given field
Public Overridable Function isExistsByField(uniq_key As Object, not_id As Integer, field As String) As Boolean
Dim where As New Hashtable
where(field) = uniq_key
where(field_id) = db.opNOT(not_id)
Dim val As String = db.value(table_name, where, "1")
If val = "1" Then
    Return True
Else
    Return False
End If
End Function

'check if item exists for a given iname
Public Overridable Function isExists(uniq_key As Object, not_id As Integer) As Boolean
Return isExistsByField(uniq_key, not_id, field_iname)
End Function

'add new record and return new record id
Public Overridable Function add(item As Hashtable) As Integer
'item("add_time") = Now() 'not necessary because add_time field in db should have default value now() or getdate()
If field_add_users_id > "" AndAlso Not item.ContainsKey(field_add_users_id) AndAlso fw.SESSION("is_logged") Then item(field_add_users_id) = fw.SESSION("user_id")
Dim id As Integer = db.insert(table_name, item)
fw.logEvent(table_name & "_add", id)
Return id
End Function

'update exising record
Public Overridable Function update(id As Integer, item As Hashtable) As Boolean
If field_upd_time > "" Then item(field_upd_time) = Now()
If field_upd_users_id > "" AndAlso Not item.ContainsKey(field_upd_users_id) AndAlso fw.SESSION("is_logged") Then item(field_upd_users_id) = fw.SESSION("user_id")

Dim where As New Hashtable
where(Me.field_id) = id
db.update(table_name, item, where)

fw.logEvent(table_name & "_upd", id)

fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
Return True
End Function

'mark record as deleted (status=127) OR actually delete from db (if is_perm or status field not defined for this model table)
Public Overridable Sub delete(id As Integer, Optional is_perm As Boolean = False)
Dim where As New Hashtable
where(Me.field_id) = id

If is_perm OrElse String.IsNullOrEmpty(field_status) Then
    'place here code that remove related data
    db.del(table_name, where)
    fw.cache.requestRemove("fwmodel_one_" & table_name & "#" & id) 'cleanup cache, so next one read will read new value
Else
    Dim vars As New Hashtable
    vars(field_status) = STATUS_DELETED
    If field_upd_time > "" Then vars(field_upd_time) = Now()
    If field_add_users_id > "" AndAlso fw.SESSION("is_logged") Then vars(field_add_users_id) = fw.SESSION("user_id")

    db.update(table_name, vars, where)
End If
fw.logEvent(table_name & "_del", id)
End Sub

'upload utils
Public Overridable Function uploadFile(id As Integer, ByRef filepath As String, Optional input_name As String = "file1", Optional is_skip_check As Boolean = False) As Boolean
Return UploadUtils.uploadFile(fw, table_name, id, filepath, input_name, is_skip_check)
End Function

'return upload dir for the module name and id related to FW.config("site_root")/upload
' id splitted to 1000
Public Overridable Function getUploadDir(ByVal id As Long) As String
Return UploadUtils.getUploadDir(fw, table_name, id)
End Function

Public Overridable Function getUploadUrl(ByVal id As Long, ByVal ext As String, Optional size As String = "") As String
Return UploadUtils.getUploadUrl(fw, table_name, id, ext, size)
End Function

'removes all type of image files uploaded with thumbnails
Public Overridable Function removeUpload(ByVal id As Long, ByVal ext As String) As Boolean
Dim dir As String = getUploadDir(id)

If UploadUtils.isUploadImgExtAllowed(ext) Then
    'if this is image - remove possibly created thumbs
    File.Delete(dir & "/" & id & "_l" & ext)
    File.Delete(dir & "/" & id & "_m" & ext)
    File.Delete(dir & "/" & id & "_s" & ext)
End If

'delete main file
File.Delete(dir & "/" & id & ext)
Return True
End Function

Public Overridable Function getUploadImgPath(ByVal id As Long, ByVal size As String, Optional ext As String = "") As String
Return UploadUtils.getUploadImgPath(fw, table_name, id, size, ext)
End Function

'methods from fw - just for a covenience, so no need to use "fw.", as they are used quite frequently
Public Overloads Sub logger(ByVal ParamArray args() As Object)
If args.Length = 0 Then Return
fw._logger(LogLevel.DEBUG, args)
End Sub
Public Overloads Sub logger(level As LogLevel, ByVal ParamArray args() As Object)
If args.Length = 0 Then Return
fw._logger(level, args)
End Sub


Public Overridable Function getSelectOptions(sel_id As String) As String
Return FormUtils.selectOptions(Me.listSelectOptions(), sel_id)
End Function

Public Overridable Function getAutocompleteList(q As String) As ArrayList
Dim where As New Hashtable
where(field_iname) = db.opLIKE("%" & q & "%")
If field_status > "" Then where(field_status) = db.opNOT(STATUS_DELETED)
Return db.col(table_name, where, field_iname)
End Function

'sel_ids - selected ids in the list()
'params - to use - override in your model
Public Overridable Function getMultiListAL(ids As ArrayList, Optional params As Object = Nothing) As ArrayList
Dim rows As ArrayList = Me.list()
For Each row As Hashtable In rows
    row("is_checked") = ids.Contains(row(Me.field_id))
Next
Return rows
End Function

'overloaded version for string comma-separated ids
'sel_ids - comma-separated ids
'params - to use - override in your model
Public Overridable Function getMultiList(sel_ids As String, Optional params As Object = Nothing) As ArrayList
Dim ids As New ArrayList(Split(sel_ids, ","))
Return Me.getMultiListAL(ids, params)
End Function

''' <summary>
'''     return comma-separated ids of linked elements - TODO refactor to use arrays, not comma-separated string
''' </summary>
''' <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
''' <param name="id">main id</param>
''' <param name="id_name">field name for main id</param>
''' <param name="link_id_name">field name for linked id</param>
''' <returns></returns>
Public Overridable Function getLinkedIds(link_table_name As String, id As Integer, id_name As String, link_id_name As String) As ArrayList
Dim where As New Hashtable
where(id_name) = id
Dim rows As ArrayList = db.array(link_table_name, where)
Dim result As New ArrayList
For Each row As Hashtable In rows
    result.Add(row(link_id_name))
Next

Return result
End Function

''' <summary>
'''  update (and add/del) linked table
''' </summary>
''' <param name="link_table_name">link table name that contains id_name and link_id_name fields</param>
''' <param name="id">main id</param>
''' <param name="id_name">field name for main id</param>
''' <param name="link_id_name">field name for linked id</param>
''' <param name="linked_keys">hashtable with keys as link id (as passed from web)</param>
Public Overridable Sub updateLinked(link_table_name As String, id As Integer, id_name As String, link_id_name As String, linked_keys As Hashtable)
Dim fields As New Hashtable
Dim where As New Hashtable
Dim link_table_field_status = "status"

'set all fields as under update
fields(link_table_field_status) = 1
where(id_name) = id
db.update(link_table_name, fields, where)

If linked_keys IsNot Nothing Then
    For Each link_id As String In linked_keys.Keys
        fields = New Hashtable
        fields(id_name) = id
        fields(link_id_name) = link_id
        fields(link_table_field_status) = 0

        where = New Hashtable
        where(id_name) = id
        where(link_id_name) = link_id
        db.update_or_insert(link_table_name, fields, where)
    Next
End If

'remove those who still not updated (so removed)
where = New Hashtable
where(id_name) = id
where(link_table_field_status) = 1
db.del(link_table_name, where)
End Sub


Public Overridable Function findOrAddByIname(iname As String, ByRef Optional is_added As Boolean = False) As Integer
iname = Trim(iname)
If iname.Length = 0 Then Return 0
Dim result As Integer
Dim item As Hashtable = Me.oneByIname(iname)
If item.ContainsKey(Me.field_id) Then
    'exists
    result = item(Me.field_id)
Else
    'not exists - add new
    item = New Hashtable
    item(field_iname) = iname
    result = Me.add(item)
    is_added = True
End If
Return result
End Function

Public Overridable Function getCSVExport() As StringBuilder
Dim where As New Hashtable
If field_status > "" Then where(field_status) = STATUS_ACTIVE

Dim aselect_fields As String() = Array.Empty(Of String)()
If csv_export_fields > "" Then
    aselect_fields = Utils.qw(csv_export_fields)
End If

Dim rows = db.array(table_name, where, "", aselect_fields)
Return Utils.getCSVExport(csv_export_headers, csv_export_fields, rows)

End Function
*/
        public void Dispose()
        {
            fw.Dispose();
        }
    }
}
