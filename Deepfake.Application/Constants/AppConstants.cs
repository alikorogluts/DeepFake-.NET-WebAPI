namespace Deepfake.Application.Constants;

public static class AppConstants
{
    // Supabase Storage Kova Adı
    public const string StorageBucket = "analysis-images";
    
    // RabbitMQ Kuyruk Adları
    public const string AnalysisQueue = "analysis_queue";
    public const string ResultQueue = "result_queue";
}