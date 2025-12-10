// Att model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.IO;

namespace osafw;

public class Att : FwModel<Att.Row>
{
    public class Row
    {
        public int id { get; set; }
        public string icode { get; set; } = string.Empty;
        public int? att_categories_id { get; set; }
        public int? fwentities_id { get; set; }
        public int? item_id { get; set; }
        public int is_s3 { get; set; }
        public int is_inline { get; set; }
        public int is_image { get; set; }
        public string fname { get; set; } = string.Empty;
        public long fsize { get; set; }
        public string ext { get; set; } = string.Empty;
        public string iname { get; set; } = string.Empty;
        public int status { get; set; }
        public DateTime add_time { get; set; }
        public int add_users_id { get; set; }
        public DateTime? upd_time { get; set; }
        public int upd_users_id { get; set; }
    }

    public const string IMGURL_0 = "/img/0.gif";
    public const string IMGURL_FILE = "/img/att_file.png";

    const string URL_PREFIX = "/Att";

    const int MAX_THUMB_W_S = 180;
    const int MAX_THUMB_H_S = 180;
    const int MAX_THUMB_W_M = 512;
    const int MAX_THUMB_H_M = 512;
    const int MAX_THUMB_W_L = 1200;
    const int MAX_THUMB_H_L = 1200;

    const int CACHE_DAYS = 30; // cache requests for 30 days

    public Att() : base()
    {
        table_name = "att";
    }

    // overload by file index
    public FwRow? uploadOne(int id, int file_index, bool is_new = false)
    {
        var files = fw.request?.Form?.Files;
        if (files == null || file_index >= files.Count)
            throw new UserException("No file(s) selected");

        return uploadOne(id, files[file_index], is_new);
    }

    // overload by file name
    public FwRow? uploadOne(int id, string input_name, bool is_new = false)
    {
        var files = fw.request?.Form?.Files;
        var fileByName = files?.GetFile(input_name);
        if (fileByName == null)
            throw new UserException("No file(s) selected");

        return uploadOne(id, fileByName, is_new);
    }

    /// <summary>
    /// upload file to the server and update att table with file information
    /// </summary>
    /// <param name="id"></param>
    /// <param name="file"></param>
    /// <param name="is_new"></param>
    /// <returns> return hashtable with added files information id, fname, fsize, ext and filepath or null if upload failed or no files</returns>
    /// </returns>
    public FwRow? uploadOne(int id, IFormFile file, bool is_new = false)
    {
        FwRow? result = null;
        var requestFiles = fw.request?.Form?.Files;
        if (requestFiles == null || requestFiles.Count == 0 || file == null)
            return result;

        if (uploadFile(id, out string filepath, file, true))
        {
            logger("uploaded to [" + filepath + "]");
            string ext = UploadUtils.getUploadFileExt(filepath);

            // update db with file information
            FwRow fields = [];
            if (is_new)
                fields["iname"] = file.FileName;

            fields["iname"] = file.FileName;
            fields["fname"] = file.FileName;
            fields["fsize"] = Utils.fileSize(filepath);
            fields["ext"] = ext;
            fields["is_s3"] = 0; //reset S3 flag to overwrite the existing S3 file
            fields["status"] = STATUS_ACTIVE; // finished upload - change status to active
                                              // turn on image flag if it's an image
            if (UploadUtils.isUploadImgExtAllowed(ext))
            {
                // if it's an image - turn on flag and resize for thumbs
                fields["is_image"] = "1";

                ImageUtils.resize(filepath, getUploadImgPath(id, "s", ext), MAX_THUMB_W_S, MAX_THUMB_H_S);
                ImageUtils.resize(filepath, getUploadImgPath(id, "m", ext), MAX_THUMB_W_M, MAX_THUMB_H_M);
                ImageUtils.resize(filepath, getUploadImgPath(id, "l", ext), MAX_THUMB_W_L, MAX_THUMB_H_L);
            }

            this.update(id, fields);
            fields["filepath"] = filepath;
            result = fields;

            moveToS3(id);
        }
        return result;
    }

    // return id of the first successful upload
    /// <summary>
    /// mulitple files upload from Request.Files
    /// </summary>
    /// <param name="item">files to add to att table, can contain: table_name, item_id, att_categories_id</param>
    /// <returns>db array list of added files information id, fname, fsize, ext, filepath</returns>
    public FwList uploadMulti(FwRow item)
    {
        FwList result = [];

        var files = fw.request?.Form?.Files;
        if (files == null || files.Count == 0)
            return result;

        for (var i = 0; i <= files.Count - 1; i++)
        {
            var file = files[i];
            if (file.Length > 0)
            {
                // add att db record
                FwRow itemdb = new(item);
                itemdb["status"] = STATUS_UNDER_UPDATE; // under upload
                var id = this.add(itemdb);

                var resone = this.uploadOne(id, file, true);
                if (resone != null)
                {
                    resone["id"] = id;
                    result.Add(resone);
                }
            }
        }

        return result;
    }

    // when uploading tmp files - use this function to make them linked to specific entity id
    public bool updateTmpUploads(string entity_icode, int item_id)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        FwRow where = [];
        where["fwentities_id"] = fwentities_id;
        where["iname"] = db.opLIKE("TMP#%");
        where["status"] = STATUS_DELETED;
        where["item_id"] = db.opISNULL();
        db.update(table_name, new FwRow() {
            { "status", STATUS_ACTIVE },
            { "item_id", item_id }
        }, where);
        return true;
    }

    /// <summary>
    /// permanently removes any temporary uploads older than 48h
    /// </summary>
    /// <returns>number of uploads deleted</returns>
    public int cleanupTmpUploads()
    {
        var rows = db.arrayp("select * from " + db.qid(table_name) +
            @$" where add_time<DATEADD(hour, -48, getdate())
                 and (status={db.qi(STATUS_UNDER_UPDATE)} or status={db.qi(STATUS_DELETED)} and iname like 'TMP#%')", DB.h());
        foreach (var row in rows)
            this.delete(row["id"].toInt(), true);
        return rows.Count;
    }

    /// <summary>
    /// return url of the uploaded file (by item)
    ///   IMPORTANT! call checkAccess before this function to check if user has access to the file
    ///   because if case of S3 url - it's a direct url without additional checks
    /// </summary>
    /// <param name="item"></param>
    /// <param name="size">s,m,l or empty(original size)</param>
    /// <returns></returns>
    public string getUrl(FwRow item, string size = "")
    {
        string result;
        if (item["is_s3"].toBool())
        {
            result = fw.model<S3>().getSignedUrl(getS3KeyByID(item["icode"].toStr(), size));
        }
        else
        {
            result = fw.config("ROOT_URL") + URL_PREFIX + "/" + item["icode"];
            if (!string.IsNullOrEmpty(size))
                result += "?size=" + size;
        }
        return result;
    }

    /// <summary>
    /// return url of the uploaded file (by id)
    ///   IMPORTANT! call checkAccess before this function to check if user has access to the file
    ///   because if case of S3 url - it's a direct url without additional checks
    /// </summary>
    /// <param name="id"></param>
    /// <param name="size">s,m,l or empty(original size)</param>
    /// <returns></returns>
    public string getUrl(int id, string size = "")
    {
        var item = one(id);
        if (item.Count == 0)
            return "";

        return getUrl(item, size);
    }

    /// <summary>
    /// return absolute url (with https://domain) of the uploaded file (by id)
    /// </summary>
    /// <param name="id"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public string getUrlAbsolute(int id, string size = "")
    {
        var url = getUrl(id, size);
        //if start with "/" - this is relative, add domain
        if (url.StartsWith("/"))
            url = fw.config("ROOT_DOMAIN").toStr() + url;

        return url;
    }

    public string getUrlPreview(int id, string size = "s")
    {
        return getUrl(id, size) + "&preview=1";
    }
    public string getUrlPreview(FwRow item, string size = "s")
    {
        return getUrl(item, size) + "&preview=1";
    }

    // mark record as deleted (status=127) OR actually delete from db (if is_perm)
    public override void delete(int id, bool is_perm = false)
    {
        // also delete from related tables:
        // users.att_id -> null?
        // spages.head_att_id -> null?
        if (is_perm)
        {
            // delete from att_links only if perm
            fw.model<AttLinks>().deleteByAtt(id);

            // remove files first
            var item = one(id);
            if (item["is_s3"].toInt() == 1)
            {
                //delete the whole folder for att, it will delete all files recursively
                fw.model<S3>().deleteObject(table_name + "/" + item["icode"] + "/");
            }
            else
            {
                // local storage
                deleteLocalFiles(id);
            }
        }

        base.delete(id, is_perm);
    }

    public void deleteLocalFiles(int id)
    {
        var item = one(id);

        string filepath = getUploadImgPath(id, "", item["ext"].toStr());
        if (!string.IsNullOrEmpty(filepath))
            File.Delete(filepath);
        // for images - also delete s/m thumbnails
        if (item["is_image"].toInt() == 1)
        {
            foreach (string size in Utils.qw("s m l"))
            {
                filepath = getUploadImgPath(id, size, item["ext"].toStr());
                if (!string.IsNullOrEmpty(filepath))
                    File.Delete(filepath);
            }
        }
    }

    // check access rights for current user for the file by id
    // generate exception
    public override void checkAccess(int id = 0, string action = "")
    {
        bool result = true;
        var item = one(id);

        // int user_access_level = fw.userAccessLevel;
        // If item("access_level") > user_access_level Then
        // result = False
        // End If

        // file must have Active status
        if (item["status"].toInt() != STATUS_ACTIVE)
            result = false;

        if (!result)
            throw new AuthException("Access Denied. You don't have enough rights to get this file");
    }

    // transimt file by id/size to user's browser, optional disposition - attachment(default)/inline
    // also check access rights - throws ApplicationException if file not accessible by cur user
    // if no file found - throws ApplicationException
    public void transmitFile(int id, string size = "", string disposition = "attachment")
    {
        var item = one(id);
        if (item.Count == 0)
            throw new UserException("No file specified");

        checkAccess(item["id"].toInt());

        if (size != "s" && size != "m")
            size = "";

        var max_age = (int)(new TimeSpan(CACHE_DAYS, 0, 0, 0).TotalSeconds);
        fw.response.Headers.CacheControl = $"private, max-age={max_age}"; // use public only if all uploads are public
        fw.response.Headers.Pragma = "cache";
        fw.response.Headers.Expires = DateTime.Now.AddDays(CACHE_DAYS).ToString("R"); // cache for several days, this allows browser not to send any requests to server during this period (unless F5)

        string filepath = getUploadImgPath(id, size, item["ext"].toStr());
        if (!File.Exists(filepath))
        {
            fw.response.StatusCode = 404;
            return;
        }

        DateTime filetime = File.GetLastWriteTime(filepath).ToUniversalTime();

        fw.response.Headers.LastModified = filetime.ToString("R");// this allows browser to send If-Modified-Since request headers (unless Ctrl+F5)

        string ifmodhead = fw.request.Headers.IfModifiedSince.ToString();
        if (ifmodhead != null && DateTime.TryParse(ifmodhead, out DateTime ifmod) && ifmod >= filetime)
        {
            fw.response.StatusCode = 304; // not modified
            return;
        }

        fw.logger(LogLevel.INFO, "Transmit(", disposition, ") filepath [", filepath, "]");
        string filename = item["fname"].toStr().Replace('"', '\'');
        string ext = UploadUtils.getUploadFileExt(filename);

        fw.response.Headers.ContentType = Utils.ext2mime(ext);
        fw.response.Headers.ContentDisposition = disposition + $"; filename=\"{filename}\"";

        fw.response.SendFileAsync(filepath).Wait();
    }

    /// <summary>
    /// list all files linked to item_id via att_links, optionally filtered by is_image and category_icode
    /// </summary>
    /// <param name="entity_icode"></param>
    /// <param name="item_id"></param>
    /// <param name="is_image"></param>
    /// <param name="category_icode"></param>
    /// <returns></returns>
    public FwList listLinked(string entity_icode, int item_id, int is_image = -1, string category_icode = "")
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        string where = "";
        FwRow @params = [];
        @params["@fwentities_id"] = fwentities_id;
        @params["@item_id"] = item_id;

        if (is_image > -1)
        {
            where += " and a.is_image=@is_image";
            @params["@is_image"] = is_image;
        }
        if (category_icode != "")
        {
            var att_category = fw.model<AttCategories>().oneByIcode(category_icode);
            if (att_category.Count > 0)
            {
                where += " and a.att_categories_id=@att_categories_id";
                @params["@att_categories_id"] = att_category["id"];
            }
        }

        return db.arrayp("select a.* " + " from " + db.qid(fw.model<AttLinks>().table_name) + " al, " + db.qid(table_name) + " a " +
            $@" where al.fwentities_id=@fwentities_id
                  and al.item_id=@item_id
                  and a.id=al.att_id 
                  {where}
                order by a.id", @params);
    }

    /// <summary>
    /// return first linked file (or image) to item_id via att_links
    /// </summary>
    /// <param name="entity_icode"></param>
    /// <param name="item_id"></param>
    /// <param name="is_image"></param>
    /// <returns></returns>
    public FwRow oneFirstLinked(string entity_icode, int item_id, int is_image = -1)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        string where = "";
        FwRow @params = new()
        {
            {"@fwentities_id", fwentities_id},
            {"@item_id", item_id},
        };
        if (is_image > -1)
        {
            where += " and a.is_image=@is_image";
            @params["@is_image"] = is_image;
        }

        return db.rowp(db.limit("SELECT a.* from " + db.qid(fw.model<AttLinks>().table_name) + " al, " + db.qid(table_name) + " a" +
            @$" WHERE al.fwentities_id=@fwentities_id
                  and al.item_id=@item_id
                  and a.id=al.att_id
                  {where}
                order by a.id", 1), @params);
    }

    // return all att images linked via att_links
    public FwList listLinkedImages(string link_table_name, int id)
    {
        return listLinked(link_table_name, id, 1);
    }

    // return all att files linked via att.fwentities_id and att.item_id
    // is_image = -1 (all - files and images), 0 (files only), 1 (images only)
    public FwList listByEntity(string entity_icode, int item_id, int is_image = -1)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        FwRow where = [];
        where["status"] = STATUS_ACTIVE;
        where["fwentities_id"] = fwentities_id;
        where["item_id"] = item_id;
        if (is_image > -1)
            where["is_image"] = is_image;
        return db.array(table_name, where, "id");
    }

    // return all att files linked via att.fwentities_id and att.item_id and att.att_categories_id
    public FwList listByEntityCategory(string entity_icode, int item_id, string category_icode = "")
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        var att_categories_id = 0;
        if (!string.IsNullOrEmpty(category_icode))
        {
            var att_category = fw.model<AttCategories>().oneByIcode(category_icode);
            if (att_category.Count == 0)
                return [];
            att_categories_id = att_category["id"].toInt();
        }

        FwRow where = [];
        where["status"] = STATUS_ACTIVE;
        where["fwentities_id"] = fwentities_id;
        where["item_id"] = item_id;
        where["att_categories_id"] = att_categories_id;
        return db.array(table_name, where, "id");
    }

    // return one att record with additional check by entity
    public FwRow oneWithEntityCheck(int id, string entity_icode)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        var row = one(id).toHashtable();
        if (row["fwentities_id"].toInt() != fwentities_id)
            row.Clear();
        return row;
    }

    // return one att record by table_name and item_id
    public FwRow oneByEntity(string entity_icode, int item_id)
    {
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);

        return db.row(table_name, new FwRow()
        {
            {"fwentities_id",fwentities_id},
            {"item_id",item_id}
        });
    }

    public string getS3KeyByID(string id, string size = "")
    {
        var sizestr = "";
        if (!string.IsNullOrEmpty(size))
            sizestr = "_" + size;

        return this.table_name + "/" + id + "/" + id + sizestr;
    }

    //////////////////// S3 related functions - only works with S3 model if Amazon.S3 installed

    // generate signed url and redirect to it, so user download directly from S3
    public void redirectS3(FwRow item, string size = "")
    {
        if (fw.userId == 0)
            throw new AuthException(); // denied for non-logged

        var url = fw.model<S3>().getSignedUrl(getS3KeyByID(item["icode"].toStr(), size));
        fw.redirect(url);
    }

    /// <summary>
    /// move file from local file storage to S3
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool moveToS3(int id)
    {
        if (!S3.IS_ENABLED)
            return false;

#pragma warning disable CS0162 // Unreachable code detected
        var result = true;
#pragma warning restore CS0162 // Unreachable code detected
        var item = one(id);
        if (item["is_s3"].toInt() == 1)
            return true; // already in S3

        var model_s3 = fw.model<S3>();
        // model_s3.createFolder(Me.table_name)
        // upload all sizes if exists
        // id=47 -> /47/47 /47/47_s /47/47_m /47/47_l
        foreach (string size1 in Utils.qw("&nbsp; s m l"))
        {
            var size = size1.Trim();
            string filepath = getUploadImgPath(id, size, item["ext"]);
            if (!System.IO.File.Exists(filepath))
                continue;

            result = model_s3.uploadLocalFile(getS3KeyByID(item["icode"], size), filepath, "inline");
            if (!result)
                break;
        }

        if (result)
        {
            // mark as uploaded
            this.update(id, new FwRow() { { "is_s3", "1" } });
            // remove local files
            deleteLocalFiles(id);
        }

        return result;
    }

    /// <summary>
    /// download file from S3 to filepath, if filepath is empty - download to tmp file and return full path
    /// </summary>
    /// <param name="id">att.id</param>
    /// <param name="size">optional size for images</param>
    /// <param name="filepath">filepath, if filepath is empty - download to tmp file and return full path</param>
    /// <returns>downloaded file filepath or empty string if not success</returns>
    public string downloadFromS3(int id, string size = "", string filepath = "")
    {
        var item = one(id);
        if (item["is_s3"] != "1")
        {
            logger("att file not in S3");
            return string.Empty;
        }

        if (Utils.isEmpty(filepath))
        {
            filepath = Utils.getTmpFilename() + item["ext"];
        }

        return fw.model<S3>().download(getS3KeyByID(id.ToString(), size), filepath);
    }


    /// <summary>
    /// upload all posted files (fw.request.Form.Files) to S3 for the table
    /// </summary>
    /// <param name="entity_icode"></param>
    /// <param name="item_id"></param>
    /// <param name="att_categories_id"></param>
    /// <param name="fieldnames">qw string of ONLY field names to upload</param>
    /// <returns>number of successuflly uploaded files</returns>
    /// <remarks>also set FLASH error if some files not uploaded</remarks>
    public int uploadPostedFilesS3(string entity_icode, int item_id, string? att_categories_id = null, string fieldnames = "")
    {
        var result = 0;
        var fwentities_id = fw.model<FwEntities>().idByIcodeOrAdd(entity_icode);
        var honlynames = Utils.qh(fieldnames);

        // create list of eligible file uploads, check for the ContentLength as any 'input type = "file"' creates a System.Web.HttpPostedFile object even if the file was not attached to the input
        FwList afiles = [];
        if (honlynames.Count > 0)
        {
            // if we only need some fields - skip if not requested field
            for (var i = 0; i <= fw.request.Form.Files.Count - 1; i++)
            {
                if (!honlynames.ContainsKey(fw.request.Form.Files[i].FileName))
                    continue;
                if (fw.request.Form.Files[i].Length > 0)
                    afiles.Add(fw.request.Form.Files[i]);
            }
        }
        else
            // just add all files
            for (var i = 0; i <= fw.request.Form.Files.Count - 1; i++)
            {
                if (fw.request.Form.Files[i].Length > 0)
                    afiles.Add(fw.request.Form.Files[i]);
            }

        // do nothing if empty file list
        if (afiles.Count == 0)
            return 0;

        // upload files to the S3
        var model_s3 = fw.model<S3>();

        // create /att folder
        model_s3.createFolder(this.table_name);

        // upload files to S3
        foreach (IFormFile file in afiles)
        {
            // first - save to db so we can get att_id
            FwRow attitem = [];
            attitem["att_categories_id"] = att_categories_id;
            attitem["fwentities_id"] = fwentities_id;
            attitem["item_id"] = item_id;
            attitem["is_s3"] = "1";
            attitem["status"] = "1";
            attitem["fname"] = file.FileName;
            attitem["fsize"] = file.Length;
            attitem["ext"] = UploadUtils.getUploadFileExt(file.FileName);
            var att_id = fw.model<Att>().add(attitem);

            try
            {
                model_s3.uploadPostedFile(getS3KeyByID(att_id.ToString()), file, "inline");

                // TODO check response for 200 and if not - error/delete?
                // once uploaded - mark in db as uploaded
                fw.model<Att>().update(att_id, new FwRow() { { "status", "0" } });

                result += 1;
            }
            catch (Exception ex)
            {
                logger(ex.Message);
                logger(ex);
                fw.flash("error", "Some files were not uploaded due to error. Please re-try.");
                // TODO if error - don't set status to 0 but remove att record?
                fw.model<Att>().delete(att_id, true);
            }
        }

        return result;
    }

    public override void filterForJson(FwRow item)
    {
        //leave only specific keys
        var keys = Utils.qh("id icode att_categories_id iname is_image fsize ext url url_preview");
        foreach (var key in new FwList(item.Keys))
        {
            if (!keys.ContainsKey(key))
                item.Remove(key);
        }

        //also add url and url_preview if not exists
        if (!item.ContainsKey("url"))
            item["url"] = getUrl(item);
        if (!item.ContainsKey("url_preview"))
            item["url_preview"] = getUrlPreview(item);
    }
}
