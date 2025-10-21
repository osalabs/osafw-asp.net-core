// S3 Storage model class
//
// Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
// (c) 2009-2021 Oleg Savchuk www.osalabs.com

//https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html
//https://docs.aws.amazon.com/AmazonS3/latest/dev/UploadObjSingleOpNET.html
//https://docs.aws.amazon.com/AmazonS3/latest/dev/HLuploadFileDotNet.html

//this module will work if user for defined AWSAccessKey will have permissions like:
// YOURBUCKETNAME is same as defined in S3Bucket
// you could optionally add /S3Root/* after YOURBUCKETNAME to limit access only to specific root prefix
//
//{
//    "Version": "2012-10-17",
//    "Statement": [
//        {
//            "Sid": "XXXXXXXXXXXXXXXXXX",
//            "Effect": "Allow",
//            "Action": [
//                "s3:*"
//            ],
//            "Resource": [
//                "arn:aws:s3:::YOURBUCKETNAME",   <-- bucket name only to allow Bucket List queries
//                "arn:aws:s3:::YOURBUCKETNAME/*"  <-- note /* here
//            ]
//        }
//    ]
//}

// uncomment line below to enable S3 storage
//#define is_S3

#if is_S3
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Text.RegularExpressions;
#endif

namespace osafw;

public class S3 : FwModel<S3.Row>
{
    public class Row
    {
        public int id { get; set; }
    }

    public string region = "";
    public string bucket = "";
    public string root = "";

    // const for simple check from other code if S3 is enabled
#if is_S3
    public const bool IS_ENABLED = true;
    public AmazonS3Client client;
    // params defined in web.config:
    // fw.config("AWSAccessKey") - access key
    // fw.config("AWSSecretKey") - secret key
    // fw.config("AWSRegion") - region "us-west-2"
    // fw.config("S3Bucket") - bucket name "xyz"
    // fw.config("S3Root") - root folder under bucket, default ""
#else
    public const bool IS_ENABLED = false;
#endif

    public override void init(FW fw)
    {
        base.init(fw);
        table_name = "xxx";

        initClient(); // automatically init client on start
    }

#if !is_S3
    //S3 disabled - just use a stub methods
    public object initClient(string access_key = "", string secret_key = "", string region = "", string bucket = "", string root = "")
    {
        return null;
    }

    public object createFolder(string foldername)
    {
        fw.logger(LogLevel.WARN, "S3 storage is not enabled");
        return null;
    }

    public string getSignedUrl(string key, int expires_minutes = 10, int max_age = 31536000)
    {
        fw.logger(LogLevel.WARN, "S3 storage is not enabled");
        return "";
    }

    public bool uploadLocalFile(string key, string filepath, string disposition = "", string filename = "", object storage_class = null)
    {
        fw.logger(LogLevel.WARN, "S3 storage is not enabled");
        return false;
    }

    public object deleteObject(string key)
    {
        fw.logger(LogLevel.WARN, "S3 storage is not enabled");
        return null;
    }

    public string download(string key, string filepath)
    {
        fw.logger(LogLevel.WARN, "S3 storage is not enabled");
        return "";
    }

    public object uploadPostedFile(string key, object file, string disposition = "", string filename = "")
    {
        throw new System.ApplicationException("S3 storage is not enabled");
    }

#else
    // initialize client
    // root should end with "/" if non-empty
    public AmazonS3Client initClient(string access_key = "", string secret_key = "", string region = "", string bucket = "", string root = "")
    {
        string akey = (string)(!string.IsNullOrEmpty(access_key) ? access_key : fw.config("AWSAccessKey"));
        string skey = (string)(!string.IsNullOrEmpty(secret_key) ? secret_key : fw.config("AWSSecretKey"));
        // region is defined in web.config "AWSRegion"

        this.region = (string)(!string.IsNullOrEmpty(region) ? region : fw.config("AWSRegion"));
        this.bucket = (string)(!string.IsNullOrEmpty(bucket) ? bucket : fw.config("S3Bucket"));
        this.root = (string)(!string.IsNullOrEmpty(root) ? root : fw.config("S3Root"));

        //throw exception if region/bucket/akey/skey is not defined
        if (string.IsNullOrEmpty(this.region) || string.IsNullOrEmpty(this.bucket) || string.IsNullOrEmpty(akey) || string.IsNullOrEmpty(skey))
            throw new System.ApplicationException("S3 storage is not configured");

        client = new AmazonS3Client(akey, skey, Amazon.RegionEndpoint.GetBySystemName(this.region)); // , Amazon.RegionEndpoint.USWest2

        return client;
    }

    // foldername - relative to the S3Root
    // foldername should not contain / at the begin or end
    public PutObjectResponse createFolder(string foldername)
    {
        foldername = Regex.Replace(foldername, @"^\/|\/$", ""); // remove / from begin and end

        // create /att folder
        PutObjectRequest request = new()
        {
            BucketName = this.bucket,
            Key = this.root + foldername + "/",
            ContentBody = string.Empty
        };
        // logger(client.Config.RegionEndpointServiceName)
        // logger(request.BucketName)
        // logger(request.Key)
        var task = client.PutObjectAsync(request);
        task.Wait();
        // logger("folder created: " & request.Key)
        // logger("response:" & response.HttpStatusCode)
        // logger("response:" & response.ToString())
        return task.Result;
    }

    /// <summary>
    /// upload local file by filepath to the S3
    /// </summary>
    /// <remarks>
    /// S3 Storage Classes: https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/S3/TS3StorageClass.html
    /// </remarks>
    /// <param name="key">relative to the S3Root</param>
    /// <param name="file">file from http upload</param>
    /// <param name="disposition">if defined (ex: inline) - Content-Disposition with file.FileName added</param>
    /// <param name="filename">optional filename to include in disposition header</param>
    /// <param name="storage_class">S3 Storage Class, default is Amazon.S3.S3StorageClass.Standard, use 5 times cheaper Amazon.S3.S3StorageClass.GlacierInstantRetrieval for warm archive files.</param>
    /// <returns></returns>
    public bool uploadLocalFile(string key, string filepath, string disposition = "", string filename = "", S3StorageClass storage_class = null)
    {
        logger("uploading to S3: key=[" + key + "], filepath=[" + filepath + "]");

        var request = new PutObjectRequest()
        {
            BucketName = this.bucket,
            Key = this.root + key,
            FilePath = filepath,
            StorageClass = storage_class ?? S3StorageClass.Standard
        };
        request.Headers["Content-Type"] = UploadUtils.mimeMapping(filepath);

        if (!string.IsNullOrEmpty(disposition))
        {
            if (filename == "") filename = System.IO.Path.GetFileName(filepath);
            filename = filename.Replace("\"", "'"); // replace quotes
            request.Headers["Content-Disposition"] = disposition + "; filename=\"" + filename + "\"";
        }

        var task = client.PutObjectAsync(request);
        task.Wait();
        // logger("uploaded to: " & request.Key)
        if (task.Result.HttpStatusCode != System.Net.HttpStatusCode.OK) logger(LogLevel.WARN, "HttpStatusCode=" + task.Result.HttpStatusCode);

        return (task.Result.HttpStatusCode == System.Net.HttpStatusCode.OK);
    }

    /// <summary>
    ///  upload HttpPostedFile to the S3
    /// </summary>
    /// <param name="key">relative to the S3Root</param>
    /// <param name="file">file from http upload</param>
    /// <param name="disposition">if defined (ex: inline) - Content-Disposition with file.FileName added</param>
    /// <param name="filename">optional filename to include in disposition header</param>
    /// <returns></returns>
    /// alternative way for disposition - in get https://docs.aws.amazon.com/AmazonS3/latest/dev/RetrievingObjectUsingNetSDK.html
    public PutObjectResponse uploadPostedFile(string key, Microsoft.AspNetCore.Http.IFormFile file, string disposition = "", string filename = "")
    {
        var request = new PutObjectRequest()
        {
            BucketName = this.bucket,
            Key = this.root + key,
            InputStream = file.OpenReadStream()
        };
        request.Headers["Content-Type"] = file.ContentType;

        if (!string.IsNullOrEmpty(disposition))
        {
            if (filename == "") filename = file.FileName;
            filename = filename.Replace("\"", "'"); // replace quotes
            request.Headers["Content-Disposition"] = disposition + "; filename=\"" + filename + "\"";
        }

        var task = client.PutObjectAsync(request);
        task.Wait();
        // logger("uploaded to: " & request.Key)
        if (task.Result.HttpStatusCode != System.Net.HttpStatusCode.OK) logger(LogLevel.WARN, "HttpStatusCode=" + task.Result.HttpStatusCode);
        return task.Result;
    }

    // alternative hi-level way to upload - use TransferUtility
    // Dim fileTransferUtility = New Amazon.S3.Transfer.TransferUtility(client)
    // fileTransferUtility.Upload()

    /// <summary>
    /// download file from S3 to specific local filepath
    /// </summary>
    /// <param name="key"></param>
    /// <param name="filepath">filepath to download file to</param>
    /// <returns>downloaded file filepath or empty string if not success</returns>
    public string download(string key, string filepath)
    {
        logger($"downloading from S3: key=[{key}], filepath=[{filepath}]");

        GetObjectRequest request = new()
        {
            BucketName = this.bucket,
            Key = this.root + key
        };

        var task = client.GetObjectAsync(request);
        task.Wait();
        GetObjectResponse response = task.Result;

        // check response code and return empty string if not 200
        if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            logger($"S3 download failed: {response.HttpStatusCode}");
            return string.Empty;
        }

        response.WriteResponseStreamToFileAsync(filepath, false, default).Wait();

        return filepath;
    }


    /// <summary>
    /// return signed url for the key with standard params: 10 min expiration
    /// </summary>
    /// <param name="key">relative to the S3Root</param>
    /// <returns>url to download the </returns>
    /// see for all the details and ability to override response headers https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/S3/MS3GetPreSignedURLGetPreSignedUrlRequest.html
    /// TODO for cacheing use custom builder which will round current time to 10min (or 1h) and sign with "fixed" time instead current
    /// https://stackoverflow.com/questions/45213553/aws-s3-presigned-request-cache
    /// or cache signed urls on caller level (Att model)
    public string getSignedUrl(string key, int expires_minutes = 10, int max_age = 31536000)
    {
        if (max_age == 0)
            max_age = expires_minutes * 60;//special case to match max_age to expires

        var headers = new ResponseHeaderOverrides();
        headers.CacheControl = "private, max-age=" + max_age + ", immutable"; //max age=31536000 with immuatable avoids send revalidation request from browser to resource https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching#avoiding_revalidation

        var request = new GetPreSignedUrlRequest()
        {
            BucketName = this.bucket,
            Key = this.root + key,
            Expires = DateTime.Now.AddMinutes(10),
            ResponseHeaderOverrides = headers
        };
        // or DateTime.UtcNow
        // sample code
        // request.ResponseHeaderOverrides.ContentType = "text/xml+zip"
        // request.ResponseHeaderOverrides.ContentDisposition = "attachment; filename=dispName.pdf"
        // request.ResponseHeaderOverrides.CacheControl = "No-cache"
        // request.ResponseHeaderOverrides.ContentLanguage = "mi, en"
        // request.ResponseHeaderOverrides.Expires = "Thu, 01 Dec 1994 16:00:00 GMT"
        // request.ResponseHeaderOverrides.ContentEncoding = "x-gzip"
        return client.GetPreSignedURL(request);
    }

    /// <summary>
    /// delete one object or whole folder
    /// </summary>
    /// <param name="key">object key, relative to the S3Root by default</param>
    /// <param name="is_add_root">if set to False, the S3Root prefix is not added. Used in recursive folder delete where object key name is obtained as a full path from the S3 API</param>
    /// <param name="is_folder_check">if set to False, do not check for the folder "/" ending. Used to delete a folder where the folder itself is an actual object with a zero size</param>
    /// <returns>response of one object deletion or response of top folder delete</returns>
    /// <remarks>RECURSIVE! for folders</remarks>
    public DeleteObjectResponse deleteObject(string key, bool is_add_root = true, bool is_folder_check = true)
    {
        logger("S3 deleteObject: [" + key + "]" + " (" + is_add_root.ToString()[0] + "," + is_folder_check.ToString()[0] + ")");
        if (is_folder_check && key.EndsWith("/"))
        {
            // it's subfolder - delete all content first
            ListObjectsV2Request listrequest = new()
            {
                BucketName = this.bucket,
                Prefix = (is_add_root ? this.root : "") + key,
                Delimiter = "/"
            };
            var task = client.ListObjectsV2Async(listrequest);
            task.Wait();
            ListObjectsV2Response list = task.Result;

            //delete objects in folder first. Note: object can be a folder itself with a zero size if it was created separately with no body and key name ending with "/", so set "is_folder_check" to False here to delete an object and avoid an infinite loop
            var list_s3objects = list.S3Objects;
            if (list_s3objects != null)
            {
            foreach (S3Object entry in list_s3objects)
                deleteObject(entry.Key, false, false);
            }

            //delete subfolders if any
            var list_common_prefixes = list.CommonPrefixes;
            if (list_common_prefixes != null)
            {
            foreach (string subfolder in list_common_prefixes)
                deleteObject(subfolder, false);
            }
        }

        DeleteObjectRequest request = new()
        {
            BucketName = this.bucket,
            Key = (is_add_root ? this.root : "") + key
        };

        var task2 = client.DeleteObjectAsync(request);
        task2.Wait();
        return task2.Result;
    }
#endif
}
