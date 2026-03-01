using Deepfake.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
namespace Deepfake.Infrastructure.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        public async Task<bool>IsValidImageSignatureAsync(Stream imageStream)
        {
            if (imageStream == null || imageStream.Length < 8) return false;
        
            byte[] buffer = new byte[8];
            imageStream.Position = 0;
            await imageStream.ReadExactlyAsync(buffer, 0, 8);
            imageStream.Position = 0; // Kaseti başa sar

            // JPEG Magic Numbers: FF D8 FF
            if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF) return true;

            // PNG Magic Numbers: 89 50 4E 47 0D 0A 1A 0A
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
                buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A) return true;

            return false;
        }

        public async Task<byte[]> CreateThumbnailAsync(Stream imageStream, int thumbnailWidth, int thumbnailHeight)
        {
            imageStream.Position = 0;
            using var img = await Image.LoadAsync(imageStream);
            
            img.Mutate(x=>x.Resize( new ResizeOptions{
                Size= new Size(thumbnailWidth, thumbnailHeight),
                Mode= ResizeMode.Crop}
            ));
            using var thumbStream = new MemoryStream();
            await img.SaveAsync(thumbStream, new JpegEncoder());
            return thumbStream.ToArray();
        }
    }
}