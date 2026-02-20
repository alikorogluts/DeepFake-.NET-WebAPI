using System.IO;
using System.Threading.Tasks;

namespace Deepfake.Application.Interfaces;

public interface IStorageService
{
    // IFormFile GİTTİ, evrensel Stream GELDİ!
    Task<string> UploadFileAsync(Stream fileStream, string bucketName, string fileName, string contentType = "image/jpeg");
    Task<string> UploadFileBytesAsync(byte[] fileBytes, string bucketName, string fileName);
}