#pragma warning disable CA1416 // some methods are Windows only

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace osafw;

public class ImageUtils
{

    // Detect orientation and auto-rotate correctly
    public static bool rotate(System.Drawing.Image image)
    {
        bool result = false;
        var rot = RotateFlipType.RotateNoneFlipNone;

        PropertyItem[] props = image.PropertyItems;

        foreach (PropertyItem p in props)
        {
            if (p.Id == 274)
            {
                switch (BitConverter.ToInt16(p.Value, 0))
                {
                    case 1:
                        rot = RotateFlipType.RotateNoneFlipNone;
                        break;
                    case 3:
                        rot = RotateFlipType.Rotate180FlipNone;
                        break;
                    case 6:
                        rot = RotateFlipType.Rotate90FlipNone;
                        break;
                    case 8:
                        rot = RotateFlipType.Rotate270FlipNone;
                        break;
                }
            }
        }

        if (rot != RotateFlipType.RotateNoneFlipNone)
        {
            image.RotateFlip(rot);
            result = true;
        }
        return result;
    }

    // resize image in from_file to w/h and save to to_file
    // (optional)w and h - mean max weight and max height (i.e. image will not be upsized if it's smaller than max w/h)
    // if no w/h passed - then no resizing occurs, just conversion (based on destination extension)
    // return false if no resize performed (if image already smaller than necessary). Note if to_file is not same as from_file - to_file will have a copy of the from_file
    public static bool resize(string from_file, string to_file, int w = -1, int h = -1)
    {
        FileStream stream = new(from_file, FileMode.Open, FileAccess.Read);

        // Create new image.
        System.Drawing.Image image = System.Drawing.Image.FromStream(stream);

        // Detect orientation and auto-rotate correctly
        rotate(image);

        // Calculate proportional max width and height.
        int oldWidth = image.Width;
        int oldHeight = image.Height;

        if (w == -1)
            w = oldWidth;
        if (h == -1)
            h = oldHeight;

        if (oldWidth / (double)w >= 1 | oldHeight / (double)h >= 1)
        {
        }
        else
        {
            // image already smaller no resize required - keep sizes same
            image.Dispose();
            stream.Close();
            if (to_file != from_file)
                // but if destination file is different - make a copy
                File.Copy(from_file, to_file);
            return false;
        }

        if (((double)oldWidth / (double)oldHeight) > ((double)w / (double)h))
        {
            double ratio = (double)w / (double)oldWidth;
            h = (int)(oldHeight * ratio);
        }
        else
        {
            double ratio = (double)h / (double)oldHeight;
            w = (int)(oldWidth * ratio);
        }

        // Create a new bitmap with the same resolution as the original image.
        Bitmap bitmap = new(w, h, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        // Create a new graphic.
        Graphics gr = Graphics.FromImage(bitmap);
        gr.Clear(Color.White);
        gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
        gr.SmoothingMode = SmoothingMode.HighQuality;
        gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
        gr.CompositingQuality = CompositingQuality.HighQuality;

        // Create a scaled image based on the original.
        gr.DrawImage(image, new Rectangle(0, 0, w, h), new Rectangle(0, 0, oldWidth, oldHeight), GraphicsUnit.Pixel);
        gr.Dispose();

        // Save the scaled image.
        string ext = UploadUtils.getUploadFileExt(to_file);
        ImageFormat out_format = image.RawFormat;
        EncoderParameters EncoderParameters = null;
        ImageCodecInfo ImageCodecInfo = null;

        if (ext == ".gif")
        {
            out_format = ImageFormat.Gif;
        }
        else if (ext == ".jpg")
        {
            out_format = ImageFormat.Jpeg;
            // set jpeg quality to 80
            ImageCodecInfo = GetEncoderInfo(out_format);
            System.Drawing.Imaging.Encoder encoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameters = new EncoderParameters(1);
            EncoderParameters.Param[0] = new EncoderParameter(encoder, System.Convert.ToInt32(80L));
        }
        else if (ext == ".png")
        {
            out_format = ImageFormat.Png;
        }

        // close read stream before writing as to_file might be same as from_file
        image.Dispose();
        stream.Close();

        if (EncoderParameters == null)
        {
            bitmap.Save(to_file, out_format); // image.RawFormat
        }
        else
        {
            bitmap.Save(to_file, ImageCodecInfo, EncoderParameters);
        }
        bitmap.Dispose();

        // if( contentType == "image/gif" )
        // {
        // Using (thumbnail)
        // {
        // OctreeQuantizer quantizer = new OctreeQuantizer ( 255 , 8 ) ;
        // using ( Bitmap quantized = quantizer.Quantize ( bitmap ) )
        // {
        // Response.ContentType = "image/gif";
        // quantized.Save ( Response.OutputStream , ImageFormat.Gif ) ;
        // }
        // }
        // }

        return true;
    }

    private static ImageCodecInfo GetEncoderInfo(ImageFormat format)
    {
        ImageCodecInfo[] encoders;
        encoders = ImageCodecInfo.GetImageEncoders();

        int j = 0;
        while (j < encoders.Length)
        {
            if (encoders[j].FormatID == format.Guid) return encoders[j];
            j += 1;
        }
        return null;

    } // GetEncoderInfo

}
