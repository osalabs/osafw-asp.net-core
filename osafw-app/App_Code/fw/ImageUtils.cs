#pragma warning disable CA1416 // some methods are Windows only

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace osafw;

public class ImageUtils
{
    /// <summary>
    /// Validates uploaded image size and header dimensions before thumbnail resize work starts.
    /// </summary>
    /// <param name="filepath">Full path to the uploaded image file saved on disk.</param>
    /// <param name="maxFileBytes">Maximum allowed compressed file size in bytes.</param>
    /// <param name="maxWidth">Maximum allowed image width in pixels.</param>
    /// <param name="maxHeight">Maximum allowed image height in pixels.</param>
    /// <param name="maxPixels">Maximum allowed width times height pixel count.</param>
    /// <exception cref="UserException">Thrown when the image is too large, malformed, or unsupported.</exception>
    public static void validateImageUpload(string filepath, long maxFileBytes, int maxWidth, int maxHeight, long maxPixels)
    {
        var info = new FileInfo(filepath);
        if (!info.Exists)
            throw new UserException("Uploaded image was not saved");

        if (maxFileBytes > 0 && info.Length > maxFileBytes)
            throw new UserException("Uploaded image is too large");

        var dimensions = imageDimensions(filepath);
        if (dimensions.Width <= 0 || dimensions.Height <= 0)
            throw new UserException("Uploaded image type is not supported");

        var pixels = (long)dimensions.Width * dimensions.Height;
        if (dimensions.Width > maxWidth || dimensions.Height > maxHeight || pixels > maxPixels)
            throw new UserException("Uploaded image dimensions are too large");
    }

    /// <summary>
    /// Reads image dimensions from PNG, GIF, or JPEG headers without performing a full resize decode.
    /// </summary>
    /// <param name="filepath">Full path to an uploaded image file.</param>
    /// <returns>Header dimensions as width and height, or zeros when the file is not a supported image header.</returns>
    public static (int Width, int Height) imageDimensions(string filepath)
    {
        using FileStream stream = new(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
        Span<byte> header = stackalloc byte[24];
        var read = stream.Read(header);
        if (read < 10)
            return (0, 0);

        if (read >= 24
            && header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && header[4] == 0x0D
            && header[5] == 0x0A
            && header[6] == 0x1A
            && header[7] == 0x0A)
        {
            return (
                BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)),
                BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4))
            );
        }

        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
        {
            return (
                BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(6, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(8, 2))
            );
        }

        if (header[0] == 0xFF && header[1] == 0xD8)
            return jpegDimensions(stream);

        return (0, 0);
    }

    private static (int Width, int Height) jpegDimensions(Stream stream)
    {
        stream.Position = 2;
        while (stream.Position < stream.Length)
        {
            var markerStart = stream.ReadByte();
            if (markerStart < 0)
                break;
            if (markerStart != 0xFF)
                continue;

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0 || marker == 0xD9 || marker == 0xDA)
                break;

            var length = readUInt16BigEndian(stream);
            if (length < 2)
                break;

            if (isJpegStartOfFrame(marker))
            {
                if (stream.ReadByte() < 0)
                    break;

                var height = readUInt16BigEndian(stream);
                var width = readUInt16BigEndian(stream);
                return (width, height);
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }

        return (0, 0);
    }

    private static bool isJpegStartOfFrame(int marker)
    {
        return marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
    }

    private static int readUInt16BigEndian(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        if (high < 0 || low < 0)
            return 0;

        return (high << 8) + low;
    }

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
                if (p.Value == null || p.Value.Length < 2)
                    continue;

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
        EncoderParameters? EncoderParameters = null;
        ImageCodecInfo? ImageCodecInfo = null;

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

        if (EncoderParameters == null || ImageCodecInfo == null)
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

    private static ImageCodecInfo? GetEncoderInfo(ImageFormat format)
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
