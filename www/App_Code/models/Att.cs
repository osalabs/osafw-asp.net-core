// Att model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace osafw
{

//TODO MIGRATE
//# Const is_S3 = False 'if you use Amazon.S3 set to True here and in S3 model

//# If is_S3 Then
//    Imports Amazon
//#End If

    public class Att : FwModel
    {
        const int MAX_THUMB_W_S = 180;
        const int MAX_THUMB_H_S = 180;
        const int MAX_THUMB_W_M = 512;
        const int MAX_THUMB_H_M = 512;
        const int MAX_THUMB_W_L = 1200;
        const int MAX_THUMB_H_L = 1200;

        public string MIME_MAP = "doc|application/msword docx|application/msword xls|application/vnd.ms-excel xlsx|application/vnd.ms-excel ppt|application/vnd.ms-powerpoint pptx|application/vnd.ms-powerpoint pdf|application/pdf html|text/html zip|application/x-zip-compressed jpg|image/jpeg jpeg|image/jpeg gif|image/gif png|image/png wmv|video/x-ms-wmv avi|video/x-msvideo mp4|video/mp4";
        public string att_table_link = "att_table_link";

        public Att() : base()
        {
            table_name = "att";
        }

        public Hashtable uploadOne(int id, int file_index, bool is_new = false)
        {
            Hashtable result = null/* TODO Change to default(_) if this is not a reference type */;
            if (uploadFile(id, out string filepath, file_index, true))
            {
                logger("uploaded to [" + filepath + "]");
                string ext = UploadUtils.getUploadFileExt(filepath);

                // TODO refactor in better way
                IFormFile file = fw.req.Form.Files[file_index];

                // update db with file information
                Hashtable fields = new();
                if (is_new)
                    fields["iname"] = file.FileName;
                fields["iname"] = file.FileName;
                fields["fname"] = file.FileName;
                fields["fsize"] = Utils.fileSize(filepath);
                fields["ext"] = ext;
                fields["status"] = STATUS_ACTIVE; // finished upload - change status to active
                                                  // turn on image flag if it's an image
                if (UploadUtils.isUploadImgExtAllowed(ext))
                {
                    // if it's an image - turn on flag and resize for thumbs
                    fields["is_image"] = 1;

                    Utils.resizeImage(filepath, getUploadImgPath(id, "s", ext), MAX_THUMB_W_S, MAX_THUMB_H_S);
                    Utils.resizeImage(filepath, getUploadImgPath(id, "m", ext), MAX_THUMB_W_M, MAX_THUMB_H_M);
                    Utils.resizeImage(filepath, getUploadImgPath(id, "l", ext), MAX_THUMB_W_L, MAX_THUMB_H_L);
                }

                this.update(id, fields);
                fields["filepath"] = filepath;
                result = fields;
            }
            return result;
        }

        // return id of the first successful upload
        /// <summary>
        /// mulitple files upload from Request.Files
        /// </summary>
        /// <param name="item">files to add to att table, can contain: table_name, item_id, att_categories_id</param>
        /// <returns>db array list of added files information id, fname, fsize, ext, filepath</returns>
        public ArrayList uploadMulti(Hashtable item)
        {
            ArrayList result = new();

            for (var i = 0; i <= fw.req.Form.Files.Count - 1; i++)
            {
                var file = fw.req.Form.Files[i];
                if (file.Length > 0)
                {
                    // add att db record
                    Hashtable itemdb = (Hashtable)item.Clone();
                    itemdb["status"] = 1; // under upload
                    var id = this.add(itemdb);

                    var resone = this.uploadOne(id, i, true);
                    if (resone != null)
                    {
                        resone["id"] = id;
                        result.Add(resone);
                    }
                }
            }

            return result;
        }

        public bool updateTmpUploads(string files_code, string att_table_name, int item_id)
        {
            Hashtable where = new();
            where["table_name"] = "tmp_" + att_table_name + "_" + files_code;
            where["item_id"] = 0;
            db.update(table_name, new Hashtable() { { "table_name", att_table_name }, { "item_id", item_id } }, where);
            return true;
        }

        /// <summary>
        /// permanently removes any temporary uploads older than 48h
        /// </summary>
        /// <returns>number of uploads deleted</returns>
        public int cleanupTmpUploads()
        {
            var rows = db.array("select * from " + db.q_ident(table_name) + " where add_time<DATEADD(hour, -48, getdate()) and (status=1 or table_name like 'tmp[_]%')");
            foreach (Hashtable row in rows)
                this.delete((int)row["id"], true);
            return rows.Count;
        }

        // add/update att_table_links
        public void updateAttLinks(string table_name, int id, Hashtable form_att)
        {
            if (form_att == null)
                return;

            int me_id = fw.model<Users>().meId();

            // 1. set status=1 (under update)
            Hashtable fields = new();
            fields["status"] = 1;
            Hashtable where = new();
            where["table_name"] = table_name;
            where["item_id"] = id;
            db.update(att_table_link, fields, where);

            // 2. add new items or update old to status =0
            foreach (string form_att_id in form_att.Keys)
            {
                int att_id = Utils.f2int(form_att_id);
                if (att_id == 0)
                    continue;

                where = new();
                where["table_name"] = table_name;
                where["item_id"] = id;
                where["att_id"] = att_id;
                Hashtable row = db.row(att_table_link, where);

                if (Utils.f2int(row["id"])>0)
                {
                    // existing link
                    fields = new();
                    fields["status"] = 0;
                    where = new();
                    where["id"] = row["id"];
                    db.update(att_table_link, fields, where);
                }
                else
                {
                    // new link
                    fields = new();
                    fields["att_id"] = att_id;
                    fields["table_name"] = table_name;
                    fields["item_id"] = id;
                    fields["add_users_id"] = me_id;
                    db.insert(att_table_link, fields);
                }
            }

            // 3. remove not updated atts (i.e. user removed them)
            where = new();
            where["table_name"] = table_name;
            where["item_id"] = id;
            where["status"] = 1;
            db.del(att_table_link, where);
        }


        // return correct url
        public string getUrl(int id, string size = "")
        {
            // Dim item As Hashtable = one(id)
            // Return get_upload_url(id, item("ext"), size)
            if (id == 0)
                return "";

            // if /Att need to be on offline folder
            string result = fw.config("ROOT_URL") + "/Att/" + id;
            if (!string.IsNullOrEmpty(size))
                result += "?size=" + size;
            return result;
        }

        // return correct url - direct, i.e. not via /Att
        public string getUrlDirect(int id, string size = "")
        {
            Hashtable item = one(id);
            if (item.Count == 0)
                return "";

            return getUrlDirect(item, size);
        }

        // if you already have item, must contain: item("id"), item("ext")
        public string getUrlDirect(Hashtable item, string size = "")
        {
            return getUploadUrl((long)item["id"], (string)item["ext"], size);
        }

        // IN: extension - doc, jpg, ... (dot is optional)
        // OUT: mime type or application/octetstream if not found
        public string getMimeForExt(string ext)
        {
            Hashtable map = Utils.qh(MIME_MAP);
            ext = Regex.Replace(ext, @"^\.", ""); // remove dot if any

            string result;
            if (map.ContainsKey(ext))
                result = (string)map[ext];
            else
                result = "application/octetstream";

            return result;
        }

        // mark record as deleted (status=127) OR actually delete from db (if is_perm)
        public override void delete(int id, bool is_perm = false)
        {
            // also delete from related tables:
            // users.att_id -> null?
            // spages.head_att_id -> null?
            if (is_perm)
                // delete from att_table_link only if perm
                db.del(att_table_link, DB.h("att_id", id));

            // remove files first
            Hashtable item = one(id);
            if ((string)item["is_s3"] == "1")
                /* TODO ERROR: Skipped IfDirectiveTrivia *//* TODO ERROR: Skipped DisabledTextTrivia *//* TODO ERROR: Skipped ElseDirectiveTrivia */
                fw.logger(LogLevel.WARN, "Att record has S3 flag, but S3 storage is not enabled");
            else
                // local storage
                deleteLocalFiles(id);

            base.delete(id, is_perm);
        }

        public void deleteLocalFiles(int id)
        {
            Hashtable item = one(id);

            string filepath = getUploadImgPath(id, "", (string)item["ext"]);
            if (!string.IsNullOrEmpty(filepath))
                File.Delete(filepath);
            // for images - also delete s/m thumbnails
            if ((int)item["is_image"] == 1)
            {
                foreach (string size in Utils.qw("s m l"))
                {
                    filepath = getUploadImgPath(id, size, (string)item["ext"]);
                    if (!string.IsNullOrEmpty(filepath))
                        File.Delete(filepath);
                }
            }
        }

        // check access rights for current user for the file by id
        // generate exception
        public void checkAccessRights(int id)
        {
            bool result = true;
            Hashtable item = one(id);

            int user_access_level = Utils.f2int(fw.Session("access_level"));

            // If item("access_level") > user_access_level Then
            // result = False
            // End If

            // file must have Active status
            if ((int)item["status"] != 0)
                result = false;

            if (!result)
                throw new ApplicationException("Access Denied. You don't have enough rights to get this file");
        }

        // transimt file by id/size to user's browser, optional disposition - attachment(default)/inline
        // also check access rights - throws ApplicationException if file not accessible by cur user
        // if no file found - throws ApplicationException
        public void transmitFile(int id, string size = "", string disposition = "attachment")
        {
            Hashtable item = one(id);
            if (size != "s" && size != "m")
                size = "";

            if ((int)item["id"] > 0)
            {
                checkAccessRights((int)item["id"]);

                //TODO MIGRATE
                //fw.resp.Cache.SetCacheability(HttpCacheability.Private); // use public only if all uploads are public
                //fw.resp.Cache.SetExpires(DateTime.Now.AddDays(30)); // cache for 30 days, this allows browser not to send any requests to server during this period (unless F5)
                //fw.resp.Cache.SetMaxAge(new TimeSpan(30, 0, 0, 0));

                string filepath = getUploadImgPath(id, size, (string)item["ext"]);
                DateTime filetime = System.IO.File.GetLastWriteTime(filepath);
                filetime = new DateTime(filetime.Year, filetime.Month, filetime.Day, filetime.Hour, filetime.Minute, filetime.Second); // remove any milliseconds

                //TODO MIGRATE
                //fw.resp.Cache.SetLastModified(filetime); // this allows browser to send If-Modified-Since request headers (unless Ctrl+F5)

                string ifmodhead = fw.req.Headers["If-Modified-Since"];
                if (ifmodhead != null && DateTime.TryParse(ifmodhead, out DateTime ifmod) && ifmod.ToLocalTime() >= filetime)
                {
                    fw.resp.StatusCode = 304; // not modified
                    //TODO MIGRATE fw.resp.SuppressContent = true;
                }
                else
                {
                    fw.logger(LogLevel.INFO, "Transmit(", disposition, ") filepath [", filepath, "]");
                    string filename = ((string)item["fname"]).Replace("\"", "'");
                    string ext = UploadUtils.getUploadFileExt(filename);

                    fw.resp.Headers.Add("Content-type", getMimeForExt(ext));
                    fw.resp.Headers.Add("Content-Disposition", disposition + "; filename=\"" + filename + "\"");

                    HttpResponseWritingExtensions.WriteAsync(fw.resp, FW.getFileContent(filepath));
                    //TODO MIGRATE fw.resp.TransmitFile(filepath);
                }
            }
            else
                throw new ApplicationException("No file specified");
        }

        // return all att files linked via att_table_link
        // is_image = -1 (all - files and images), 0 (files only), 1 (images only)
        public ArrayList getAllLinked(string table_name, int id, int is_image = -1)
        {
            string where = "";
            if (is_image > -1)
                where += " and a.is_image=" + is_image;
            return db.array("select a.* " + " from " + att_table_link + " atl, att a " 
                + " where atl.table_name=" + db.q(table_name) 
                + " and atl.item_id=" + db.qi(id) 
                + " and a.id=atl.att_id" + where + " order by a.id ");
        }


        // return first att image linked via att_table_link
        public Hashtable getFirstLinkedImage(string table_name, int id)
        {
            return db.row("select top 1 a.* " + " from " + att_table_link + " atl, att a " + " where atl.table_name=" + db.q(table_name) + " and atl.item_id=" + db.qi(id) + " and a.id=atl.att_id" + " and a.is_image=1 " + " order by a.id ");
        }

        // return all att images linked via att_table_link
        public ArrayList getAllLinkedImages(string table_name, int id)
        {
            return getAllLinked(table_name, id, 1);
        }

        // return all att files linked via att.table_name and att.item_id
        // is_image = -1 (all - files and images), 0 (files only), 1 (images only)
        public ArrayList getAllByTableName(string table_name, int item_id, int is_image = -1)
        {
            Hashtable where = new();
            where["status"] = STATUS_ACTIVE;
            where["table_name"] = table_name;
            where["item_id"] = item_id;
            if (is_image > -1)
                where["is_image"] = is_image;
            return db.array(table_name, where, "id");
        }

        // like getAllByTableName, but also fills att_categories hash
        public ArrayList getAllByTableNameWithCategories(string table_name, int item_id, int is_image = -1)
        {
            var rows = getAllByTableName(table_name, item_id, is_image);
            foreach (Hashtable row in rows)
            {
                var att_categories_id = Utils.f2int(row["att_categories_id"]);
                if (att_categories_id > 0)
                    row["att_categories"] = fw.model<AttCategories>().one(att_categories_id);
            }
            return rows;
        }

        // return one att record with additional check by table_name
        public Hashtable oneWithTableName(int id, string item_table_name)
        {
            var row = one(id);
            if ((string)row["table_name"] != item_table_name)
                row.Clear();
            return row;
        }

        // return one att record by table_name and item_id
        public Hashtable oneByTableName(string item_table_name, int item_id)
        {
            return db.row(table_name, new Hashtable()
        {
            {
                "table_name",
                item_table_name
            },
            {
                "item_id",
                item_id
            }
        });
        }

        public string getS3KeyByID(string id, string size = "")
        {
            var sizestr = "";
            if (!string.IsNullOrEmpty(size))
                sizestr = "_" + size;

            return this.table_name + "/" + id + "/" + id + sizestr;
        }

        // generate signed url and redirect to it, so user download directly from S3
        public void redirectS3(Hashtable item, string size = "")
        {
            logger(LogLevel.WARN, "redirectS3 - S3 not enabled");
        }
 
//TODO MIGRATE    
//    'generate signed url and redirect to it, so user download directly from S3
//    Public Sub redirectS3(item As Hashtable, Optional size As String = "")
//#If is_S3 Then
//        If fw.model(Of Users).meId() = 0 Then Throw New ApplicationException("Access Denied") 'denied for non-logged

//        Dim url = fw.model(Of S3).getSignedUrl(getS3KeyByID(item("id"), size))

//        fw.redirect(url)
//#Else
//        logger(LogLevel.WARN, "redirectS3 - S3 not enabled")
//#End If
//    End Sub

//#If is_S3 Then

//    Public Function moveToS3(id As Integer) As Boolean
//        Dim result = True
//        Dim item = one(id)
//        If item("is_s3") = 1 Then Return True 'already in S3

//        Dim model_s3 = fw.model(Of S3)
//        'model_s3.createFolder(Me.table_name)
//        'upload all sizes if exists
//        'id=47 -> /47/47 /47/47_s /47/47_m /47/47_l
//        For Each size As String In Utils.qw("&nbsp; s m l")
//            size = Trim(size)
//            Dim filepath As String = getUploadImgPath(id, size, item("ext"))
//            If Not System.IO.File.Exists(filepath) Then Continue For

//            Dim res = model_s3.uploadFilepath(getS3KeyByID(id, size), filepath, "inline")
//            If res.HttpStatusCode<> Net.HttpStatusCode.OK Then
//                result = False
//                Exit For
//            End If
//        Next

//        If result Then
//            'mark as uploaded
//            Me.update(id, New Hashtable From { { "is_s3", 1} })
//            'remove local files
//            deleteLocalFiles(id)
//        End If

//        Return True
//    End Function

//    ''' <summary>
//    ''' upload all posted files (fw.req.Files) to S3 for the table
//    ''' </summary>
//    ''' <param name="item_table_name"></param>
//    ''' <param name="item_id"></param>
//    ''' <param name="att_categories_id"></param>
//    ''' <param name="fieldnames">qw string of ONLY field names to upload</param>
//    ''' <returns>number of successuflly uploaded files</returns>
//    ''' <remarks>also set FLASH error if some files not uploaded</remarks>
//    Public Function uploadPostedFilesS3(item_table_name As String, item_id As Integer, Optional att_categories_id As String = Nothing, Optional fieldnames As String = "") As Integer
//        Dim result = 0

//        Dim honlynames = Utils.qh(fieldnames)

//        'create list of eligible file uploads, check for the ContentLength as any 'input type = "file"' creates a System.Web.HttpPostedFile object even if the file was not attached to the input
//        Dim afiles As New ArrayList
//        If honlynames.Count > 0 Then
//            'if we only need some fields - skip if not requested field
//            For i = 0 To fw.req.Files.Count - 1
//                If Not honlynames.ContainsKey(fw.req.Files.GetKey(i)) Then Continue For
//                If fw.req.Files(i).ContentLength > 0 Then afiles.Add(fw.req.Files(i))
//            Next
//        Else
//            'just add all files
//            For i = 0 To fw.req.Files.Count - 1
//                If fw.req.Files(i).ContentLength > 0 Then afiles.Add(fw.req.Files(i))
//            Next
//        End If

//        'do nothing if empty file list
//        If afiles.Count = 0 Then Return 0

//        'upload files to the S3
//        Dim model_s3 = fw.model(Of S3)

//        'create /att folder
//        model_s3.createFolder(Me.table_name)

//        'upload files to S3
//        For Each file In afiles
//            'first - save to db so we can get att_id
//            Dim attitem As New Hashtable
//            attitem("att_categories_id") = att_categories_id
//            attitem("table_name") = item_table_name
//            attitem("item_id") = item_id
//            attitem("is_s3") = 1
//            attitem("status") = 1
//            attitem("fname") = file.FileName
//            attitem("fsize") = file.ContentLength
//            attitem("ext") = UploadUtils.getUploadFileExt(file.FileName)
//            Dim att_id = fw.model(Of Att).add(attitem)

//            Try
//                Dim response = model_s3.uploadPostedFile(getS3KeyByID(att_id), file, "inline")

//                'TODO check response for 200 and if not - error/delete?
//                'once uploaded - mark in db as uploaded
//                fw.model(Of Att).update(att_id, New Hashtable From { { "status", 0} })

//                result += 1

//            Catch ex As Amazon.S3.AmazonS3Exception
//                logger(ex.Message)
//                logger(ex)
//                fw.FLASH("error", "Some files were not uploaded due to error. Please re-try.")
//                'TODO if error - don't set status to 0 but remove att record?
//                fw.model(Of Att).delete(att_id, True)
//            End Try
//        Next

//        Return result
//    End Function
//#End If    
    }
}