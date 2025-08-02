using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfQAApp.Services;
using PdfQAApp.Models;

namespace PdfQAApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfQAController : ControllerBase
    //no view only data
    {
        private readonly IPythonPdfService _pythonPdfService;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<PdfQAController> _logger;

        public PdfQAController(
            IPythonPdfService pythonPdfService, 
            IFileUploadService fileUploadService,
            ILogger<PdfQAController> logger)
        {
            _pythonPdfService = pythonPdfService;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }

        [HttpPost("initialize")]
        public async Task<IActionResult> Initialize()
        {
            try
            {
                _logger.LogInformation("Initializing PDF QA system...");
                var result = await _pythonPdfService.InitializeAsync();
                
                 if (result.Success)
                {
                    _logger.LogInformation("PDF QA system initialized successfully");
                    return Ok(new { message = "System initialized successfully", result = result.Data });
                }
                else
                {
                    _logger.LogError($"Failed to initialize system: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing PDF QA system");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPdfs([FromForm] IFormFileCollection files)
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return BadRequest(new { error = "No files uploaded" });
                }

                _logger.LogInformation($"Uploading {files.Count} files...");
                var uploadResult = await _fileUploadService.UploadFilesAsync(files);
                
                if (!uploadResult.Success)
                {
                    return BadRequest(new { error = uploadResult.Error });
                }

                return Ok(new { 
                    message = "Files uploaded successfully", 
                    files = uploadResult.Data,
                    count = uploadResult.Data.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading files");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessPdfs([FromBody] ProcessPdfsRequest request)
        {
            try
            {
                if (request?.PdfPaths == null || request.PdfPaths.Count == 0)
                {
                    return BadRequest(new { error = "No PDF paths provided" });
                }

                _logger.LogInformation($"Processing {request.PdfPaths.Count} PDFs...");
                var result = await _pythonPdfService.ProcessPdfsAsync(request.PdfPaths);
                
                if (result.Success)
                {
                    _logger.LogInformation("PDFs processed successfully");
                    return Ok(new { 
                        message = "PDFs processed successfully", 
                        result = result.Data,
                        documentsProcessed = request.PdfPaths.Count
                    });
                }
                else
                {
                    _logger.LogError($"Failed to process PDFs: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PDFs");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion([FromBody] AskQuestionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Question))
                {
                    return BadRequest(new { error = "Question is required" });
                }

                _logger.LogInformation($"Processing question: {request.Question}");
                var result = await _pythonPdfService.AskQuestionAsync(request.Question);
                
                if (result.Success)
                {
                    _logger.LogInformation("Question processed successfully");
                    return Ok(new { 
                        question = request.Question,
                        answer = result.Data?.Answer,
                        sources = result.Data?.Sources,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogError($"Failed to process question: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing question");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("documents")]
        public async Task<IActionResult> GetDocuments()
        {
            try
            {
                _logger.LogInformation("Retrieving processed documents...");
                var result = await _pythonPdfService.GetDocumentsAsync();
                
                if (result.Success)
                {
                    return Ok(new { 
                        documents = result.Data,
                        count = result.Data?.Count ?? 0
                    });
                }
                else
                {
                    _logger.LogError($"Failed to retrieve documents: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("export")]
        public async Task<IActionResult> ExportQA([FromBody] ExportRequest request)
        {
            try
            {
                if (request?.QAHistory == null || request.QAHistory.Count == 0)
                {
                    return BadRequest(new { error = "No Q&A history provided" });
                }

                _logger.LogInformation($"Exporting {request.QAHistory.Count} Q&A pairs...");
                var result = await _pythonPdfService.ExportQAAsync(request.QAHistory, request.OutputPath);
                
                if (result.Success)
                {
                    _logger.LogInformation("Q&A exported successfully");
                    return Ok(new { 
                        message = "Q&A exported successfully", 
                        filePath = result.Data,
                        itemsExported = request.QAHistory.Count
                    });
                }
                else
                {
                    _logger.LogError($"Failed to export Q&A: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Q&A");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromBody] CleanupRequest request) //frombody as json
{
    try
    {
       _logger.LogInformation($"Cleanup endpoint called. Request is null: {request == null}");
        
        // Clear Python backend state - this is the most important part
        var clearResult = await _pythonPdfService.ClearDocumentsAsync();
        
        if (!clearResult.Success)
        {
            
            _logger.LogError($"Failed to clear Python backend: {clearResult.Error}");
            return StatusCode(500, new { 
                success = false,
                error = "Failed to clear backend state", 
                details = clearResult.Error 
            });
        }
        
        // Delete uploaded files if requested
        if (request?.ClearAll == true)
        {
            // Delete all uploaded files
            var filesResult = await _fileUploadService.GetUploadedFilesAsync();
            if (filesResult.Success && filesResult.Data != null)
            {
                foreach (var file in filesResult.Data)
                {
                    await _fileUploadService.DeleteFileAsync(file.FilePath);
                }
                _logger.LogInformation($"Deleted {filesResult.Data.Count} files");
            }
        }
        else if (request?.FilePaths != null && request.FilePaths.Count > 0)
        {
            // Delete specific files
            foreach (var filePath in request.FilePaths)
            {
                await _fileUploadService.DeleteFileAsync(filePath);
            }
            _logger.LogInformation($"Deleted {request.FilePaths.Count} specific files");
        }
        
        _logger.LogInformation("Cleanup completed successfully");
        return Ok(new { 
            success = true, 
            message = "Cleanup completed successfully",
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during cleanup");
        return StatusCode(500, new { 
            success = false,
            error = "Internal server error", 
            details = ex.Message 
        });
    }
}

        [HttpPost("test")]
        public async Task<IActionResult> TestPythonIntegration()
        {
            try
            {
                _logger.LogInformation("Testing Python integration...");
                var result = await _pythonPdfService.TestConnectionAsync();
                
                if (result.Success)
                {
                    return Ok(new { 
                        message = "Python integration test successful", 
                        result = result.Data,
                        timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Python integration");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearDocuments()
        {
            try
            {
                _logger.LogInformation("Clearing processed documents...");
                var result = await _pythonPdfService.ClearDocumentsAsync();
                
                if (result.Success)
                {
                    _logger.LogInformation("Documents cleared successfully");
                    return Ok(new { message = "Documents cleared successfully" });
                }
                else
                {
                    _logger.LogError($"Failed to clear documents: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing documents");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpGet("files")]
        public async Task<IActionResult> GetUploadedFiles()
        {
            try
            {
                _logger.LogInformation("Retrieving uploaded files...");
                var result = await _fileUploadService.GetUploadedFilesAsync();
                
                if (result.Success)
                {
                    return Ok(new { 
                        files = result.Data,
                        count = result.Data?.Count ?? 0
                    });
                }
                else
                {
                    _logger.LogError($"Failed to retrieve uploaded files: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving uploaded files");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        [HttpDelete("files/{fileName}")]
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest(new { error = "File name is required" });
                }

                _logger.LogInformation($"Deleting file: {fileName}");
                var result = await _fileUploadService.DeleteFileAsync(fileName);
                
                if (result.Success)
                {
                    _logger.LogInformation($"File deleted successfully: {fileName}");
                    return Ok(new { message = "File deleted successfully" });
                }
                else
                {
                    _logger.LogError($"Failed to delete file: {result.Error}");
                    return BadRequest(new { error = result.Error });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file: {fileName}");
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }
    }
}