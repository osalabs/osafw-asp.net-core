using System;
using System.Web;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace osafw
{
    public class UploadParams
    {
        public FW fw;
        // input params:
        public bool is_required = false; // set to True and upload_simple will throw ApplicationException if file required, but not uploaded
        public bool is_mkdir = true; // create save_path if not exists
        public bool is_overwrite = true; // overwrite existing file
        public bool is_cleanup = false; // only if is_overwrite=true, apply remove_upload_img to destination path (cleans all old jpg/png/gif with thumbnails)
        public bool is_resize = false; // resize to max w/h if image
        public int max_w = 1200; //  default max image width
        public int max_h = 1200; //  default max iamge height

        public String field_name;
        public Hashtable allowed_ext; // if empty - all exts allowed, exts should be with dots
        public String save_path;
        public String save_filename; // without ext, ext will be same as upload file, if empty - use orig filename from upload field
        public ulong max_filesize = 0; // max allowed filesize, if 0 - allow all

        // output params:
        public String orig_filename;  // original filename with ext
        public String full_path; // full path to saved file
        public String filename; // saved filename with ext
        public String ext; // saved ext
        public ulong filesize; // saved file size

        // example: Dim up As New UploadParams("file1", ".doc .pdf")
        public UploadParams(FW fw, String field_name, String save_path, String save_filename_noext = "", String allowed_ext_str = "")
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
        // simple upload from posted field name to destination directory with different options
        public static bool uploadSimple(UploadParams up)
        {
            //bool result = false;
            bool result = false;

            IFormFile file = up.fw.req.Form.Files[up.field_name];
            if (file != null)
            {
                up.orig_filename = file.FileName;
                // check for allowed filesize 
                up.filesize = (ulong)file.Length;
                if (up.max_filesize > 0 && (ulong)file.Length > up.max_filesize)
                {
                    if (up.is_required)
                    {
                        throw new ApplicationException("Uploaded file too large in size");
                    }
                    return result;
                }

                up.ext = UploadUtils.getUploadFileExt(file.FileName);
                // check for allowed ext
                if (up.allowed_ext.Count > 0 && !up.allowed_ext.ContainsKey(up.ext))
                {
                    if (up.is_required)
                    {
                        throw new ApplicationException("Uploaded file extension is not allowed");
                    }
                    return result;
                }

                // create target directory if required
                if (up.is_mkdir && !Directory.Exists(up.save_path))
                {
                    Directory.CreateDirectory(up.save_path);
                }

                up.full_path = up.save_path;
                if (up.save_filename != String.Empty)
                {
                    up.filename = up.save_filename + up.ext;
                }
                else
                {
                    up.filename = System.IO.Path.GetFileNameWithoutExtension(up.orig_filename) + up.ext;
                }
                up.full_path = up.full_path + up.filename;

                if (up.is_overwrite && up.is_cleanup)
                {
                    removeUploadImgByPath(up.fw, up.full_path);
                }

                if (!up.is_overwrite && System.IO.File.Exists(up.full_path))
                {
                    if (up.is_required)
                    {
                        throw new ApplicationException("Uploaded file cannot overwrite existing file");
                    }
                    return result;
                }

                up.fw.logger(LogLevel.DEBUG, "saving to ", up.full_path);
                using (var fileStream = new FileStream(up.full_path, FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }

                if (up.is_resize && UploadUtils.isUploadImgExtAllowed(up.ext))
                {
                    // TODO port resize function
                    // Utils.resizeImage(up.full_path, up.full_path, up.max_w, up.max_h);
                }
                result = true;
            }
            else
            {
                if (up.is_required)
                {
                    throw new ApplicationException("No required file uploaded");
                }
            }
            return result;
        }

        // perform file upload for module_name/id and set filepath where it// s stored, return true - if upload successful
        public static bool uploadFile(FW fw, String module_name, int id, ref String filepath, String input_name = "file1", bool is_skip_check = false)
        {
            bool result = false;
            IFormFile file = fw.req.Form.Files[input_name];

            if (file == null && file.Length > 0)
            {
                String ext = getUploadFileExt(file.FileName);
                if (is_skip_check || UploadUtils.isUploadImgExtAllowed(ext))
                {
                    // remove any old files if necessary
                    removeUploadImg(fw, module_name, id);

                    // save original file
                    String part = getUploadDir(fw, module_name, id) + "\\" + id;
                    filepath = part + ext;

                    using (var fileStream = new FileStream(filepath, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }

                    result = true;
                }
                else
                {
                    // throw new ApplicationException("Image type is not supported");
                }
            }

            return result;
        }

        public static String uploadFileSave(FW fw, String module_name, int id, IFormFile file, bool is_skip_check = false)
        {
            String result = "";

            if (file != null && file.Length > 0)
            {
                String ext = getUploadFileExt(file.FileName);
                if (is_skip_check || isUploadImgExtAllowed(ext))
                {
                    // remove any old files if necessary
                    removeUploadImg(fw, module_name, id);

                    // save original file
                    String part = getUploadDir(fw, module_name, id) + "\\" + id;
                    result = part + ext;
                    using (Stream fileStream = new FileStream(result, FileMode.Create))
                    {
                        file.CopyTo(fileStream);
                    }
                }
            }
            else
            {
                throw new ApplicationException("Image type is not supported");
            }

            return result;
        }

        // return extension, lowercased, .jpeg=>.jpg
        // Usage: String ext = Utils.get_upload_file_ext(file.FileName); // file As IFile
        public static String getUploadFileExt(String filename)
        {
            String ext = System.IO.Path.GetExtension(filename).ToLower();
            if (ext == ".jpeg") ext = ".jpg"; // small correction
            return ext;
        }

        // test if upload image extension is allowed
        public static bool isUploadImgExtAllowed(String ext)
        {
            if (ext == ".jpg" || ext == ".gif" || ext == ".png" )
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // return upload dir for the module name and id related to FW.config("site_root")/upload
        // id splitted to 1000
        public static String getUploadDir(FW fw, String module_name, long id)
        {
            String dir = (String)FW.config()["site_root"] + (String)FW.config()["UPLOAD_DIR"] + "\\" + module_name + "\\" + (id % 1000);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return dir;
        }

        // similar to get_upload_dir, but return - DOESN// T check for file existance
        public static String getUploadUrl(FW fw, String module_name, long id, String ext, String size = "")
        {
            String url = (String)FW.config()["ROOT_URL"] + "/upload/" + module_name + "/" + (id % 1000) + "/" + id;
            if (!String.IsNullOrEmpty(size)) url += "_" + size;
            url += ext;

            return url;
        }

        // removes all type of image files uploaded with thumbnails
        public static bool removeUploadImg(FW fw, String module_name, long id)
        {
            String dir = UploadUtils.getUploadDir(fw, module_name, id);
            return removeUploadImgByPath(fw, dir + "\\" + id);
        }

        public static bool removeUploadImgByPath(FW fw, String path)
        {
            String dir = System.IO.Path.GetDirectoryName(path);
            path = dir + "\\" + System.IO.Path.GetFileNameWithoutExtension(path); // cut extension if any

            if (!Directory.Exists(dir)) return false;

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
        //  size is one of: ""(original), "l", "m", "s"
        public static String getUploadImgPath(FW fw, String module_name, long id, String size, String ext = "")
        {
            if (size != "l" && size != "m" && size != "s" ) size = ""; // armor +1

            String part = UploadUtils.getUploadDir(fw, module_name, id) + "\\" + id;
            String orig_file = part;

            if (!String.IsNullOrEmpty(size)) orig_file = orig_file + "_" + size;

            if (String.IsNullOrEmpty(ext))
            {
                if (File.Exists(orig_file + ".gif") ) ext = ".gif";
                if (File.Exists(orig_file + ".png") ) ext = ".png";
                if (File.Exists(orig_file + ".jpg") ) ext = ".jpg";
            }

            if (!String.IsNullOrEmpty(ext))
            {
                return orig_file + ext;
            }

            return "";
        }


        // get correct image URL for uploaded image
        public static String getUploadImgUrl(FW fw, String module_name, long id, String size)
        {
            if (size != "l" && size != "m" && size != "s" ) size = ""; // armor +1

            String part = UploadUtils.getUploadDir(fw, module_name, id) + "/" + id;
            String orig_file = part;

            if (!String.IsNullOrEmpty(size)) orig_file = orig_file + "_" + size;

            String ext = "";
            if (File.Exists(orig_file + ".gif")) ext = ".gif";
            if (File.Exists(orig_file + ".png")) ext = ".png";
            if (File.Exists(orig_file + ".jpg")) ext = ".jpg";

            if (!String.IsNullOrEmpty(ext))
            {
                return UploadUtils.getUploadUrl(fw, module_name, id, ext, size);
            }
            return "";
        }
    }
}
