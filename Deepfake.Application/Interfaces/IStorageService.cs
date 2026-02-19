using Microsoft.AspNetCore.Http;

namespace Deepfake.Application.Interfaces;

public interface IStorageService
{
    // Görseli Supabase'e yükler ve public/erişilebilir URL'ini döndürür
    Task<string> UploadImageAsync(IFormFile file, string bucketName, string fileName);
}