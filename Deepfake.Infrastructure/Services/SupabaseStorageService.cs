using Deepfake.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Supabase;

namespace Deepfake.Infrastructure.Services;

public class SupabaseStorageService : IStorageService
{
    private readonly Client _supabaseClient;

    public SupabaseStorageService(Client supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string bucketName, string fileName)
    {
        // 1. Dosyayı hafızaya (byte dizisine) oku
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        // 2. Supabase Storage'a yükle (Aynı isimde varsa üzerine yazar: Upsert = true)
        await _supabaseClient.Storage
            .From(bucketName)
            .Upload(fileBytes, fileName, new Supabase.Storage.FileOptions { CacheControl = "3600", Upsert = true });

        // 3. Yüklenen dosyanın herkes tarafından erişilebilir (Public) URL'ini al ve döndür
        var publicUrl = _supabaseClient.Storage.From(bucketName).GetPublicUrl(fileName);
        
        return publicUrl;
    }
}