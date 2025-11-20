using System.Drawing;
using System.Drawing.Imaging;

namespace BiketaBai.Helpers;

public static class ImageCompressionHelper
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private const int MaxQuality = 90;
    private const int MinQuality = 50;

    /// <summary>
    /// Compresses an image to be under 5MB while maintaining quality
    /// </summary>
    public static async Task<byte[]> CompressImageAsync(Stream imageStream, string contentType)
    {
        using var originalImage = Image.FromStream(imageStream);
        
        // Determine output format
        ImageFormat outputFormat = contentType.ToLower() switch
        {
            "image/png" => ImageFormat.Png,
            "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
            _ => ImageFormat.Jpeg
        };

        // Try different quality levels until we get under 5MB
        for (int quality = MaxQuality; quality >= MinQuality; quality -= 10)
        {
            using var memoryStream = new MemoryStream();
            
            if (outputFormat == ImageFormat.Jpeg)
            {
                var encoder = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                
                if (encoder != null)
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    
                    originalImage.Save(memoryStream, encoder, encoderParams);
                }
                else
                {
                    originalImage.Save(memoryStream, ImageFormat.Jpeg);
                }
            }
            else
            {
                // For PNG, try resizing if needed
                var resizedImage = ResizeImageIfNeeded(originalImage, MaxFileSize);
                resizedImage.Save(memoryStream, outputFormat);
                resizedImage.Dispose();
            }

            var compressedBytes = memoryStream.ToArray();
            
            if (compressedBytes.Length <= MaxFileSize)
            {
                return compressedBytes;
            }
        }

        // If still too large, resize the image
        using var resized = ResizeImageIfNeeded(originalImage, MaxFileSize);
        using var finalStream = new MemoryStream();
        resized.Save(finalStream, outputFormat);
        return finalStream.ToArray();
    }

    /// <summary>
    /// Resizes image if needed to fit within max size
    /// </summary>
    private static Image ResizeImageIfNeeded(Image originalImage, long maxSize)
    {
        // Calculate target dimensions (reduce by 10% each iteration)
        double scale = 1.0;
        Image? resized = null;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            int newWidth = (int)(originalImage.Width * scale);
            int newHeight = (int)(originalImage.Height * scale);

            resized?.Dispose();
            resized = new Bitmap(originalImage, newWidth, newHeight);

            using var testStream = new MemoryStream();
            resized.Save(testStream, originalImage.RawFormat);
            
            if (testStream.Length <= maxSize)
            {
                return resized;
            }

            scale *= 0.9; // Reduce by 10%
        }

        return resized ?? originalImage;
    }

    /// <summary>
    /// Compresses an IFormFile image
    /// </summary>
    public static async Task<byte[]> CompressFormFileAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        return await CompressImageAsync(stream, file.ContentType);
    }
}

