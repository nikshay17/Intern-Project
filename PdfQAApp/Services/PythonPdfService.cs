using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PdfQAApp.Models;

namespace PdfQAApp.Services
{
    public class PythonPdfService : IPythonPdfService
    {
        private readonly ILogger<PythonPdfService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiBaseUrl;

        public PythonPdfService(ILogger<PythonPdfService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _pythonApiBaseUrl = _configuration.GetValue<string>("PythonSettings:ApiBaseUrl") ?? "http://127.0.0.1:5000";
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(10); // Set timeout for long operations
        }

        public async Task<ServiceResult<object>> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing Python service...");
                
                var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/initialize", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<object>(content);
                    
                    _logger.LogInformation("Python service initialized successfully");
                    return new ServiceResult<object> { Success = true, Data = result };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to initialize Python service: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<object> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing Python service");
                return new ServiceResult<object> { Success = false, Error = ex.Message };
            }
        }

        public async Task<ServiceResult<object>> ProcessPdfsAsync(List<string> PdfPaths)
        {
            try
            {
                _logger.LogInformation($"Processing {PdfPaths.Count} PDFs via Python API...");
                
                var requestData = new { PdfPaths = PdfPaths };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/process", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<object>(responseContent);
                    
                    _logger.LogInformation("PDFs processed successfully by Python service");
                    return new ServiceResult<object> { Success = true, Data = result };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to process PDFs: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<object> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PDFs");
                return new ServiceResult<object> { Success = false, Error = ex.Message };
            }
        }

        public async Task<ServiceResult<QuestionAnswerResult>> AskQuestionAsync(string question)
{
    try
    {
        _logger.LogInformation($"Asking question via Python API: {question}");
        
        var requestData = new { question = question };
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/ask", content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Raw response from Python API: {responseContent}");
            
            try
            {
                // Parse the Python API response manually
                using (JsonDocument document = JsonDocument.Parse(responseContent))
                {
                    var root = document.RootElement;
                    
                    // Extract answer
                    string answer = root.GetProperty("answer").GetString();
                    
                    // Extract confidence
                    double confidence = 0.0;
                    if (root.TryGetProperty("confidence", out var confElement))
                    {
                        confidence = confElement.GetDouble();
                    }
                    
                    // Extract sources from relevant_chunks
                    var sources = new List<string>();
                    if (root.TryGetProperty("relevant_chunks", out var chunks) && chunks.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var chunk in chunks.EnumerateArray())
                        {
                            if (chunk.TryGetProperty("text", out var textElement))
                            {
                                string chunkText = textElement.GetString();
                                // Take first 100 characters of each chunk as source
                                string sourceText = chunkText.Length > 100 
                                    ? chunkText.Substring(0, 100) + "..." 
                                    : chunkText;
                                sources.Add(sourceText);
                            }
                        }
                    }
                    
                    // Create result object
                    var result = new QuestionAnswerResult
                    {
                        Answer =

                        
                        answer,
                        Sources = sources,
                        Confidence = confidence,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    _logger.LogInformation("Question processed successfully by Python service");
                    return new ServiceResult<QuestionAnswerResult> { Success = true, Data = result };
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError($"JSON parsing error: {jsonEx.Message}");
                _logger.LogError($"Response content: {responseContent}");
                return new ServiceResult<QuestionAnswerResult> 
                { 
                    Success = false, 
                    Error = $"Failed to parse response: {jsonEx.Message}" 
                };
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to ask question: {response.StatusCode} - {errorContent}");
            return new ServiceResult<QuestionAnswerResult> 
            { 
                Success = false, 
                Error = $"HTTP {response.StatusCode}: {errorContent}" 
            };
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error asking question");
        return new ServiceResult<QuestionAnswerResult> { Success = false, Error = ex.Message };
    }
}

        public async Task<ServiceResult<List<ProcessedDocument>>> GetDocumentsAsync()
        {
            try
            {
                _logger.LogInformation("Getting documents from Python API...");
                
                var response = await _httpClient.GetAsync($"{_pythonApiBaseUrl}/documents");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var documents = JsonSerializer.Deserialize<List<ProcessedDocument>>(responseContent);
                    
                    _logger.LogInformation("Documents retrieved successfully from Python service");
                    return new ServiceResult<List<ProcessedDocument>> { Success = true, Data = documents };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get documents: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<List<ProcessedDocument>> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents");
                return new ServiceResult<List<ProcessedDocument>> { Success = false, Error = ex.Message };
            }
        }

        public async Task<ServiceResult<string>> ExportQAAsync(List<QAHistory> qaHistory, string outputPath = null)
        {
            try
            {
                _logger.LogInformation($"Exporting {qaHistory.Count} Q&A pairs via Python API...");
                
                var requestData = new { 
                    qa_history = qaHistory,
                    output_path = outputPath
                };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/export", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<object>(responseContent);
                    
                    _logger.LogInformation("Q&A exported successfully by Python service");
                    return new ServiceResult<string> { Success = true, Data = result?.ToString() };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to export Q&A: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<string> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Q&A");
                return new ServiceResult<string> { Success = false, Error = ex.Message };
            }
        }

        public async Task<ServiceResult<object>> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing Python API connection...");
                
                var requestData = new { message = "Hello from C#" };
                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/test", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<object>(responseContent);
                    
                    _logger.LogInformation("Python API connection test successful");
                    return new ServiceResult<object> { Success = true, Data = result };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Python API connection test failed: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<object> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Python API connection");
                return new ServiceResult<object> { Success = false, Error = ex.Message };
            }
        }

        public async Task<ServiceResult<object>> ClearDocumentsAsync()
        {
            try
            {
                _logger.LogInformation("Clearing documents via Python API...");
                
                var response = await _httpClient.PostAsync($"{_pythonApiBaseUrl}/clear", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<object>(responseContent);
                    
                    _logger.LogInformation("Documents cleared successfully by Python service");
                    return new ServiceResult<object> { Success = true, Data = result };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to clear documents: {response.StatusCode} - {errorContent}");
                    return new ServiceResult<object> { Success = false, Error = $"HTTP {response.StatusCode}: {errorContent}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing documents");
                return new ServiceResult<object> { Success = false, Error = ex.Message };
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}