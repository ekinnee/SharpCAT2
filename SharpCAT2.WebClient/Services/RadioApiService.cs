using System.Text.Json;
using SharpCAT2.WebClient.Models;

namespace SharpCAT2.WebClient.Services;

/// <summary>
/// Implementation of radio API service that communicates with SharpCAT2.WebApi
/// </summary>
public class RadioApiService : IRadioApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RadioApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RadioApiService(HttpClient httpClient, ILogger<RadioApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<RadioStatusResponse?> GetStatusAsync()
    {
        try
        {
            _logger.LogDebug("Getting radio status");
            var response = await _httpClient.GetAsync("api/radio/status");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<RadioStatusResponse>(content, _jsonOptions);
            }
            
            _logger.LogWarning("Failed to get radio status: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            return null;
        }
    }

    public async Task<ApiResponse?> ConnectAsync(ConnectRequest request)
    {
        try
        {
            _logger.LogDebug("Connecting to radio: {SerialPort}, {Radio}, {BaudRate}", 
                request.SerialPort, request.Radio, request.BaudRate);
            
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("api/radio/connect", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<ApiResponse>(responseContent, _jsonOptions);
                return result;
            }
            
            var errorResult = JsonSerializer.Deserialize<ApiResponse>(responseContent, _jsonOptions);
            return errorResult ?? new ApiResponse { Error = "Unknown error occurred" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to radio");
            return new ApiResponse { Error = ex.Message };
        }
    }

    public async Task<ApiResponse?> DisconnectAsync()
    {
        try
        {
            _logger.LogDebug("Disconnecting from radio");
            var response = await _httpClient.PostAsync("api/radio/disconnect", null);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<ApiResponse>(content, _jsonOptions);
            }
            
            var errorResult = JsonSerializer.Deserialize<ApiResponse>(content, _jsonOptions);
            return errorResult ?? new ApiResponse { Error = "Unknown error occurred" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from radio");
            return new ApiResponse { Error = ex.Message };
        }
    }

    public async Task<CommandResponse?> SendCommandAsync(string command)
    {
        try
        {
            _logger.LogDebug("Sending command to radio: {Command}", command);
            
            var request = new { Command = command };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("api/radio/command", content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<CommandResponse>(responseContent, _jsonOptions);
            }
            
            var errorResult = JsonSerializer.Deserialize<CommandResponse>(responseContent, _jsonOptions);
            return errorResult ?? new CommandResponse { Error = "Unknown error occurred" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to radio");
            return new CommandResponse { Error = ex.Message };
        }
    }

    public async Task<List<RadioModel>?> GetAvailableRadiosAsync()
    {
        try
        {
            _logger.LogDebug("Getting available radios");
            var response = await _httpClient.GetAsync("api/radio/available");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<RadioModel>>(content, _jsonOptions);
            }
            
            _logger.LogWarning("Failed to get available radios: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available radios");
            return null;
        }
    }
}