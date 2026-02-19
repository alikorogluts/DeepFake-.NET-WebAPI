using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Deepfake.Application.Interfaces;

namespace Deepfake.Infrastructure.Services;

public class PythonFastApiClient : IPythonService
{
    private readonly HttpClient _httpClient;

    public PythonFastApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TriggerAnalysisAsync(Guid analysisId, string imageUrl)
    {
        try
        {
            var payload = new 
            { 
                id = analysisId, 
                image_url = imageUrl 
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/api/analyze",
                payload
            );

            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }}