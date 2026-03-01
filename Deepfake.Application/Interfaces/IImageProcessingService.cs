namespace Deepfake.Application.Interfaces
{
    public interface IImageProcessingService
    {
        Task<bool> IsValidImageSignatureAsync(Stream imageStream);
        Task<byte[]> CreateThumbnailAsync(Stream imageStream,int thumbnailWidth,int thumbnailHeight);
    }
}