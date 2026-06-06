// Upload and Image manipulation framework utils

// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;

namespace osafw;

public class UploadParams
{
    public FW fw = null!;
    // input params:
    public bool is_required = false; // set to True and upload_simple will throw ApplicationException if file required, but not uploaded
    public bool is_mkdir = true; // create save_path if not exists
    public bool is_overwrite = true; // overwrite existing file
    public bool is_cleanup = false; // only if is_overwrite=true, apply remove_upload_img to destination path (cleans all old jpg/png/gif with thumbnails)
    public bool is_resize = false; // resize to max w/h if image
    public int max_w = 10000; // default max image width
    public int max_h = 10000; // default max iamge height

    public string field_name = string.Empty;
    public FwDict allowed_ext = []; // if empty - all exts allowed, exts should be with dots
    public string save_path = string.Empty;
    public string save_filename = string.Empty; // without ext, ext will be same as upload file, if empty - use orig filename from upload field
    public ulong max_filesize = 0; // max allowed filesize, if 0 - allow all

    // output params:
    public string orig_filename = string.Empty; // original filename with ext
    public string full_path = string.Empty; // full path to saved file
    public string filename = string.Empty; // saved filename with ext
    public string ext = string.Empty; // saved ext
    public ulong filesize;

    // example: Dim up As New UploadParams("file1", ".doc .pdf")
    public UploadParams(FW fw, string field_name, string save_path, string save_filename_noext = "", string allowed_ext_str = "")
    {
        this.fw = fw;
        this.field_name = field_name;
        this.save_path = save_path;
        this.save_filename = save_filename_noext;
        this.allowed_ext = Utils.qh(allowed_ext_str);
    }
}


public class UploadUtils
{
    private static readonly HashSet<string> ActiveContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css",
        ".htm",
        ".html",
        ".js",
        ".mjs",
        ".svg",
        ".svgz",
        ".swf",
        ".wasm",
        ".xht",
        ".xhtml",
        ".xml",
        ".xslt",
    };

    /// <summary>
    /// Normalizes a filename or extension to the framework upload extension format.
    /// </summary>
    /// <param name="filenameOrExt">Original filename, path, or dot-prefixed extension to normalize.</param>
    /// <returns>Lowercase extension including the leading dot, or an empty string when no extension is present.</returns>
    public static string normalizeUploadExt(string? filenameOrExt)
    {
        var value = filenameOrExt?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string ext;
        if (value.StartsWith('.') && value.IndexOfAny(['\\', '/']) < 0 && value.LastIndexOf('.') == 0)
            ext = value.ToLowerInvariant();
        else
            ext = getUploadFileExt(value);

        if (ext == ".jpeg")
            ext = ".jpg";

        return ext;
    }

    /// <summary>
    /// Checks whether an extension represents browser-active content that should never be rendered inline from user uploads.
    /// </summary>
    /// <param name="filenameOrExt">Original filename, path, or dot-prefixed extension to inspect.</param>
    /// <returns><c>true</c> when the extension is commonly executable or script-capable in browsers.</returns>
    public static bool isActiveContentExt(string? filenameOrExt)
    {
        return ActiveContentExtensions.Contains(normalizeUploadExt(filenameOrExt));
    }

    /// <summary>
    /// Checks whether a client-supplied content type is browser-active and should be treated as unsafe metadata.
    /// </summary>
    /// <param name="contentType">HTTP content type supplied by the browser or storage metadata.</param>
    /// <returns><c>true</c> when the type can execute or activate browser parsing beyond inert download handling.</returns>
    public static bool isActiveContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var value = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return value == "image/svg+xml"
            || value == "text/html"
            || value == "text/css"
            || value == "application/xhtml+xml"
            || value == "application/xml"
            || value == "text/xml"
            || value.Contains("javascript")
            || value.Contains("ecmascript")
            || value.EndsWith("+xml");
    }

    /// <summary>
    /// Returns a safe content type for user attachment responses or storage metadata.
    /// </summary>
    /// <param name="filenameOrExt">Original filename, path, or dot-prefixed extension used for server-side MIME lookup.</param>
    /// <param name="submittedContentType">Optional client-supplied content type used only to detect active-content mismatches.</param>
    /// <returns>A server-selected content type, downgraded to <c>application/octet-stream</c> for active or unknown content.</returns>
    public static string contentTypeForAttachment(string? filenameOrExt, string? submittedContentType = "")
    {
        if (isActiveContentExt(filenameOrExt) || isActiveContentType(submittedContentType))
            return "application/octet-stream";

        var contentType = mimeMapping(filenameOrExt ?? string.Empty);
        return isActiveContentType(contentType) ? "application/octet-stream" : contentType;
    }

    /// <summary>
    /// Resolves a safe Content-Disposition mode for user attachments.
    /// </summary>
    /// <param name="filenameOrExt">Original filename, path, or dot-prefixed extension to inspect.</param>
    /// <param name="requestedDisposition">Requested disposition, usually <c>inline</c> or <c>attachment</c>.</param>
    /// <param name="submittedContentType">Optional client-supplied content type used to detect active-content mismatches.</param>
    /// <returns><c>inline</c> only for safe raster image types requested inline; otherwise <c>attachment</c>.</returns>
    public static string dispositionForAttachment(string? filenameOrExt, string requestedDisposition = "attachment", string? submittedContentType = "")
    {
        if (!string.Equals(requestedDisposition, "inline", StringComparison.OrdinalIgnoreCase))
            return "attachment";

        if (isActiveContentExt(filenameOrExt) || isActiveContentType(submittedContentType))
            return "attachment";

        return isUploadImgExtAllowed(normalizeUploadExt(filenameOrExt)) ? "inline" : "attachment";
    }

    // simple upload from posted field name to destination directory with different options
    public static bool uploadSimple(UploadParams up)
    {
        bool result = false;

        var files = up.fw.request?.Form?.Files;
        var file = files?.GetFile(up.field_name);
        if (file != null)
        {
            up.orig_filename = file.FileName;

            // check for allowed filesize
            up.filesize = (ulong)file.Length;
            if (up.max_filesize > 0 && (ulong)file.Length > up.max_filesize)
            {
                if (up.is_required)
                    throw new UserException("Uploaded file too large in size");
                return result;
            }

            up.ext = UploadUtils.getUploadFileExt(file.FileName);
            // check for allowed ext
            if (up.allowed_ext.Count > 0 && !up.allowed_ext.ContainsKey(up.ext))
            {
                if (up.is_required)
                    throw new UserException("Uploaded file extension is not allowed");
                return result;
            }

            // create target directory if required
            if (up.is_mkdir && !Directory.Exists(up.save_path))
                Directory.CreateDirectory(up.save_path);

            up.full_path = up.save_path;
            if (!string.IsNullOrEmpty(up.save_filename))
                up.filename = up.save_filename + up.ext;
            else
                up.filename = System.IO.Path.GetFileNameWithoutExtension(up.orig_filename) + up.ext;
            up.full_path = up.full_path + @"\" + up.filename;

            if (up.is_overwrite && up.is_cleanup)
                removeUploadImgByPath(up.fw, up.full_path);

            if (!up.is_overwrite & System.IO.File.Exists(up.full_path))
            {
                if (up.is_required)
                    throw new UserException("Uploaded file cannot overwrite existing file");
                return result;
            }

            up.fw.logger(LogLevel.DEBUG, "saving to ", up.full_path);
            using (var fileStream = new FileStream(up.full_path, FileMode.Create))
            {
                file.CopyTo(fileStream);
            }

            if (up.is_resize && isUploadImgExtAllowed(up.ext))
                ImageUtils.resize(up.full_path, up.full_path, up.max_w, up.max_h);
            result = true;
        }
        else if (up.is_required)
            throw new UserException("No required file uploaded");

        return result;
    }

    // perform file upload for module_name/id and set filepath where it's stored, return true - if upload successful
    public static bool uploadFile(FW fw, string module_name, int id, out string filepath, string input_name = "file1", bool is_skip_check = false)
    {
        var files = fw.request?.Form?.Files;
        var file = files?.GetFile(input_name);

        return uploadFile(fw, module_name, id, out filepath, file, is_skip_check);
    }

    // this one based on file index, not input name
    public static bool uploadFile(FW fw, string module_name, int id, out string filepath, int file_index = 0, bool is_skip_check = false)
    {
        filepath = "";
        var files = fw.request?.Form?.Files;
        if (files == null || file_index > files.Count - 1)
            return false;
        var file = files[file_index];

        return uploadFile(fw, module_name, id, out filepath, file, is_skip_check);
    }

    public static bool uploadFile(FW fw, string module_name, int id, out string filepath, IFormFile? file, bool is_skip_check = false)
    {
        filepath = uploadFileSave(fw, module_name, id, file, is_skip_check);
        return !string.IsNullOrEmpty(filepath);
    }

    public static string uploadFileSave(FW fw, string module_name, int id, IFormFile? file, bool is_skip_check = false)
    {
        string result = "";
        if (file != null && file.Length > 0)
        {
            string ext = getUploadFileExt(file.FileName);
            if (is_skip_check || isUploadImgExtAllowed(ext))
            {
                // remove any old files if necessary
                removeUploadImg(fw, module_name, id);

                // save original file
                string part = getUploadDir(fw, module_name, id) + @"\" + id;
                result = part + ext;
                using (Stream fileStream = new FileStream(result, FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }
            }
            else
            {
            }
        }

        return result;
    }

    // return extension, lowercased, .jpeg=>.jpg
    // Usage: Dim ext As String = Utils.get_upload_file_ext(file.FileName) 'file As IFormFile
    public static string getUploadFileExt(string filename)
    {
        string ext = System.IO.Path.GetExtension(filename).ToLower();
        if (ext == ".jpeg")
            ext = ".jpg"; // small correction
        return ext;
    }

    // test if upload image extension is allowed
    public static bool isUploadImgExtAllowed(string ext)
    {
        if (ext == ".jpg" | ext == ".gif" | ext == ".png")
            return true;
        else
            return false;
    }

    // return upload dir for the module name and id related to FW.config("site_root")/upload
    // id splitted to 1000
    public static string getUploadDir(FW fw, string module_name, long id)
    {
        string dir = fw.config("site_root").toStr() + fw.config("UPLOAD_DIR") + @"\" + module_name + @"\" + (id % 1000);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return dir;
    }

    // similar to get_upload_dir, but return - DOESN'T check for file existance
    public static string getUploadUrl(FW fw, string module_name, long id, string ext, string size = "")
    {
        string url = fw.config("ROOT_URL").toStr() + "/upload/" + module_name + "/" + (id % 1000) + "/" + id;
        if (!string.IsNullOrEmpty(size))
            url += "_" + size;
        url += ext;

        return url;
    }

    // removes all type of image files uploaded with thumbnails
    public static bool removeUploadImg(FW fw, string module_name, long id)
    {
        string dir = UploadUtils.getUploadDir(fw, module_name, id);
        return removeUploadImgByPath(fw, dir + @"\" + id);
    }

    public static bool removeUploadImgByPath(FW fw, string path)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
            return false;

        path = dir + @"\" + System.IO.Path.GetFileNameWithoutExtension(path); // cut extension if any

        if (!Directory.Exists(dir))
            return false;

        File.Delete(path + "_l.png");
        File.Delete(path + "_l.gif");
        File.Delete(path + "_l.jpg");

        File.Delete(path + "_m.png");
        File.Delete(path + "_m.gif");
        File.Delete(path + "_m.jpg");

        File.Delete(path + "_s.png");
        File.Delete(path + "_s.gif");
        File.Delete(path + "_s.jpg");

        File.Delete(path + ".png");
        File.Delete(path + ".gif");
        File.Delete(path + ".jpg");
        return true;
    }

    // get correct image path for uploaded image
    // size is one of: ""(original), "l", "m", "s"
    public static string getUploadImgPath(FW fw, string module_name, long id, string size, string ext = "")
    {
        if (size != "l" & size != "m" & size != "s")
            size = ""; // armor +1

        string part = UploadUtils.getUploadDir(fw, module_name, id) + @"\" + id;
        string orig_file = part;

        if (!string.IsNullOrEmpty(size))
            orig_file = orig_file + "_" + size;

        if (ext == "")
        {
            if (File.Exists(orig_file + ".gif"))
                ext = ".gif";
            if (File.Exists(orig_file + ".png"))
                ext = ".png";
            if (File.Exists(orig_file + ".jpg"))
                ext = ".jpg";
        }

        if (!string.IsNullOrEmpty(ext))
            return orig_file + ext;
        else
            return "";
    }


    // get correct image URL for uploaded image
    public static string getUploadImgUrl(FW fw, string module_name, long id, string size)
    {
        if (size != "l" & size != "m" & size != "s")
            size = ""; // armor +1

        string part = UploadUtils.getUploadDir(fw, module_name, id) + "/" + id;
        string orig_file = part;

        if (!string.IsNullOrEmpty(size))
            orig_file = orig_file + "_" + size;

        string ext = "";
        if (File.Exists(orig_file + ".gif"))
            ext = ".gif";
        if (File.Exists(orig_file + ".png"))
            ext = ".png";
        if (File.Exists(orig_file + ".jpg"))
            ext = ".jpg";

        if (!string.IsNullOrEmpty(ext))
            return UploadUtils.getUploadUrl(fw, module_name, id, ext, size);
        else
            return "";
    }

    // alternative for .NET 4 MimeMapping.GetMimeMapping(FileName)
    public static string mimeMapping(string filename)
    {
        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        if (provider.TryGetContentType(filename, out var contentType) && !string.IsNullOrEmpty(contentType))
            return contentType;

        return "application/octet-stream";
    }
}
