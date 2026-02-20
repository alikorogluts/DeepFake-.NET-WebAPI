using System.IO;
using System.Threading.Tasks;
using Deepfake.Application.Interfaces;
using Supabase;

namespace Deepfake.Infrastructure.Services;

public class SupabaseStorageService : IStorageService
{
    private readonly Client _supabaseClient;

    public SupabaseStorageService(Client supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    // YENİ EVRENSEL METODUMUZ
    public async Task<string> UploadFileAsync(Stream fileStream, string bucketName, string fileName, string contentType = "image/jpeg")
    {
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var options = new Supabase.Storage.FileOptions { CacheControl = "3600", Upsert = true, ContentType = contentType };
        
        await _supabaseClient.Storage
            .From(bucketName)
            .Upload(fileBytes, fileName, options);

        return _supabaseClient.Storage.From(bucketName).GetPublicUrl(fileName);
    }

    public async Task<string> UploadFileBytesAsync(byte[] fileBytes, string bucketName, string fileName)
    {
        var options = new Supabase.Storage.FileOptions { CacheControl = "3600", Upsert = true };
        
        await _supabaseClient.Storage
            .From(bucketName)
            .Upload(fileBytes, fileName, options);

        return _supabaseClient.Storage.From(bucketName).GetPublicUrl(fileName);
    }
}