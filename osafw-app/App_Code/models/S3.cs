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
//                "arn:aws:s3:::YOURBUCKETNAME/*"  <-- note /* here
//            ]
//        }
//    ]
//}

//#define is_S3

#if is_S3

using System;
using Microsoft.VisualBasic;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace osafw
{
    public class S3 : FwModel
    {
        public string region = "";
        public string bucket = "";
        public string root = "";

        public AmazonS3Client client;
        // params defined in web.config:
        // fw.config("AWSAccessKey") - access key
        // fw.config("AWSSecretKey") - secret key
        // fw.config("AWSRegion") - region "us-west-2"
        // fw.config("S3Bucket") - bucket name "xyz"
        // fw.config("S3Root") - root folder under bucket, default ""

        public override void init(FW fw)
        {
            base.init(fw);
            table_name = "xxx";

            initClient(); // automatically init client on start
        }

        // initialize client
        // root should end with "/" if non-empty
        public AmazonS3Client initClient(string access_key = "", string secret_key = "", string region = "", string bucket = "", string root = "")
        {
            string akey = (string)(!string.IsNullOrEmpty(access_key)? access_key: fw.config("AWSAccessKey"));
            string skey = (string)(!string.IsNullOrEmpty(secret_key)? secret_key: fw.config("AWSSecretKey"));
            // region is defined in web.config "AWSRegion"

            this.region = (string)(!string.IsNullOrEmpty(region)? region: fw.config("AWSRegion"));
            this.bucket = (string)(!string.IsNullOrEmpty(bucket)? bucket: fw.config("S3Bucket"));
            this.root = (string)(!string.IsNullOrEmpty(root)? root: fw.config("S3Root"));

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

        public PutObjectResponse uploadFilepath(string key, string filepath, string disposition = "")
        {
            logger("uploading to S3: key=[" + key + "], filepath=[" + filepath + "]");
            var request = new PutObjectRequest()
            {
                BucketName = this.bucket,
                Key = this.root + key,
                FilePath = filepath
            };
            request.Headers["Content-Type"] = fw.model<Att>().getMimeForExt(UploadUtils.getUploadFileExt(filepath));

            if (!string.IsNullOrEmpty(disposition))
            {
                var filename = Strings.Replace(System.IO.Path.GetFileName(filepath), "\"", "'"); // replace quotes
                request.Headers["Content-Disposition"] = "inline; filename=\"" + filename + "\"";
            }

            var task = client.PutObjectAsync(request);
            task.Wait();
            // logger("uploaded to: " & request.Key)
            // logger("HttpStatusCode=" & response.HttpStatusCode)
            return task.Result;
        }

        /// <summary>
        ///  upload HttpPostedFile to the S3
        /// </summary>
        /// <param name="key">relative to the S3Root</param>
        /// <param name="file">file from http upload</param>
        /// <param name="disposition">if defined - Content-Disposition with file.FileName added</param>
        /// <returns></returns>
        /// alternative way for disposition - in get https://docs.aws.amazon.com/AmazonS3/latest/dev/RetrievingObjectUsingNetSDK.html
        public PutObjectResponse uploadPostedFile(string key, IFormFile file, string disposition = "")
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
                var filename = file.FileName.Replace("\"", "'"); // replace quotes
                request.Headers["Content-Disposition"] = "inline; filename=\"" + filename + "\"";
            }

            var task = client.PutObjectAsync(request);
            task.Wait();
            // logger("uploaded to: " & request.Key)
            // logger("HttpStatusCode=" & response.HttpStatusCode)
            return task.Result;
        }

        // alternative hi-level way to upload - use TransferUtility
        // Dim fileTransferUtility = New Amazon.S3.Transfer.TransferUtility(client)
        // fileTransferUtility.Upload()


        /// <summary>
        /// return signed url for the key with standard params: 10 min expiration
        /// </summary>
        /// <param name="key">relative to the S3Root</param>
        /// <returns>url to download the </returns>
        /// see for all the details and ability to override response headers https://docs.aws.amazon.com/sdkfornet/v3/apidocs/items/S3/MS3GetPreSignedURLGetPreSignedUrlRequest.html
        public string getSignedUrl(string key)
        {
            var request = new GetPreSignedUrlRequest()
            {
                BucketName = this.bucket,
                Key = this.root + key,
                Expires = DateTime.Now.AddMinutes(10)
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
        /// <param name="key">relative to the S3Root</param>
        /// <returns>response of one object deletion or response of top folder delete</returns>
        /// <remarks>RECURSIVE! for folders</remarks>
        public DeleteObjectResponse deleteObject(string key)
        {
            if (Strings.Right(key, 1) == "/")
            {
                // it's subfolder - delete all content first

                ListObjectsRequest listrequest = new()
                {
                    BucketName = this.bucket,
                    Prefix = key
                };
                var task = client.ListObjectsAsync(listrequest);
                task.Wait();
                ListObjectsResponse list = task.Result;
                foreach (S3Object entry in list.S3Objects)
                    deleteObject(entry.Key);
            }

            DeleteObjectRequest request = new()
            {
                BucketName = this.bucket,
                Key = this.root + key
            };

            var task2 = client.DeleteObjectAsync(request);
            task2.Wait();
            return task2.Result;
        }
    }

}

#endif