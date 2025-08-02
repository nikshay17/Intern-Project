    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using PdfQAApp.Models;
    using PdfQAApp.Helpers;

    namespace PdfQAApp.Services
    {
        public class FileUploadService : IFileUploadService
        {
            private readonly ILogger<FileUploadService> _logger;
            private readonly IConfiguration _configuration;
            private readonly string _uploadPath;

            public FileUploadService(ILogger<FileUploadService> logger, IConfiguration configuration)
            {
                _logger = logger;
                _configuration = configuration;
                _uploadPath = _configuration["FileUpload:UploadPath"] ?? "uploads";

                // Ensure upload directory exists
                if (!Directory.Exists(_uploadPath))
                {
                    Directory.CreateDirectory(_uploadPath);
                }
            }

            public async Task<ServiceResult<List<UploadedFile>>> UploadFilesAsync(IFormFileCollection files)
            {//collection sent with html request from multipart
                try
                {
                    var uploadedFiles = new List<UploadedFile>();
                    var errors = new List<string>();

                    foreach (var file in files)
                    {
                        var result = await UploadFileAsync(file);
                        if (result.Success)
                        {
                            uploadedFiles.Add(result.Data);
                        }
                        else
                        {
                            errors.Add($"Failed to upload {file.FileName}: {result.Error}");
                        }
                    }

                    if (errors.Any())
                    {
                        _logger.LogWarning($"Some files failed to upload: {string.Join(", ", errors)}");

                        if (uploadedFiles.Count == 0)
                        {
                            return ServiceResult<List<UploadedFile>>.CreateFailure(
                                $"All files failed to upload: {string.Join(", ", errors)}");
                        }
                    }

                    _logger.LogInformation($"Successfully uploaded {uploadedFiles.Count} files");
                    return ServiceResult<List<UploadedFile>>.CreateSuccess(uploadedFiles);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading files");
                    return ServiceResult<List<UploadedFile>>.CreateFailure($"Upload failed: {ex.Message}");
                }
            }

            public async Task<ServiceResult<UploadedFile>> UploadFileAsync(IFormFile file)
            {
                try
                {
                    // Validate file
                    var validationResult = await ValidateFileAsync(file);
                    if (!validationResult.Success)
                    {
                        return ServiceResult<UploadedFile>.CreateFailure(validationResult.Error);
                    }

                    // Generate unique filename
                    var originalFileName = file.FileName;
                    var uniqueFileName = ValidationHelper.GenerateUniqueFileName(originalFileName);
                    var filePath = Path.Combine(_uploadPath, uniqueFileName);
                    
                    // Get absolute path for Python backend
                    var absolutePath = Path.GetFullPath(filePath);

                    // Save file
                    using (var stream = new FileStream(absolutePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var uploadedFile = new UploadedFile
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = originalFileName,
                        FilePath = absolutePath,  // Use absolute path
                        FileSize = file.Length,
                        UploadedAt = DateTime.UtcNow,
                        ContentType = file.ContentType
                    };

                    _logger.LogInformation($"Successfully uploaded file: {originalFileName} -> {uniqueFileName} at {absolutePath}");
                    return ServiceResult<UploadedFile>.CreateSuccess(uploadedFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error uploading file: {file?.FileName}");
                    return ServiceResult<UploadedFile>.CreateFailure($"Upload failed: {ex.Message}");
                }
            }

            public async Task<ServiceResult<bool>> DeleteFileAsync(string fileName)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        return ServiceResult<bool>.CreateBoolFailure("File name is required");
                    }

                    // Handle both filename and full path scenarios
                    string filePath;
                    if (Path.IsPathRooted(fileName))
                    {
                        // It's already a full path
                        filePath = fileName;
                    }
                    else
                    {
                        // It's just a filename, construct the full path
                        filePath = Path.Combine(_uploadPath, fileName);
                    }

                    // Security check: ensure the file is within the upload directory
                    var fullUploadPath = Path.GetFullPath(_uploadPath);
                    var fullFilePath = Path.GetFullPath(filePath);

                    if (!fullFilePath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"Security violation: Attempted to delete file outside upload directory: {fileName}");
                        return ServiceResult<bool>.CreateBoolFailure("Invalid file path");
                    }

                    if (!File.Exists(filePath))
                    {
                        _logger.LogWarning($"File not found for deletion: {fileName}");
                        return ServiceResult<bool>.CreateBoolFailure("File not found");
                    }

                    await Task.Run(() => File.Delete(filePath));

                    _logger.LogInformation($"Successfully deleted file: {fileName}");
                    return ServiceResult<bool>.CreateBoolSuccess(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error deleting file: {fileName}");
                    return ServiceResult<bool>.CreateBoolFailure($"Delete failed: {ex.Message}");
                }
            }

            public async Task<ServiceResult<List<UploadedFile>>> GetUploadedFilesAsync()
            {
                try
                {
                    var uploadedFiles = new List<UploadedFile>();
                    
                    if (Directory.Exists(_uploadPath))
                    {
                        var directoryInfo = new DirectoryInfo(_uploadPath);
                        var fileInfos = directoryInfo.GetFiles("*.pdf");
                        
                        foreach (var fileInfo in fileInfos)
                        {
                            uploadedFiles.Add(new UploadedFile
                            {
                                Id = Guid.NewGuid().ToString(),
                                FileName = fileInfo.Name,
                                FilePath = fileInfo.FullName,  // Use absolute path
                                FileSize = fileInfo.Length,
                                UploadedAt = fileInfo.CreationTimeUtc,
                                ContentType = "application/pdf"
                            });
                        }
                    }

                    _logger.LogInformation($"Found {uploadedFiles.Count} uploaded files");
                    return ServiceResult<List<UploadedFile>>.CreateSuccess(uploadedFiles);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving uploaded files");
                    return ServiceResult<List<UploadedFile>>.CreateFailure($"Retrieval failed: {ex.Message}");
                }
            }

            public async Task<ServiceResult<bool>> ValidateFileAsync(IFormFile file)
            {
                try
                {
                    if (file == null || file.Length == 0)
                    {
                        return ServiceResult<bool>.CreateBoolFailure("File is required");
                    }

                    // Use validation helper
                    var isValid = await Task.Run(() => ValidationHelper.IsValidPdfFile(file));

                    if (!isValid)
                    {
                        return ServiceResult<bool>.CreateBoolFailure("Invalid PDF file. Please upload a valid PDF file (max 50MB)");
                    }

                    _logger.LogDebug($"File validation successful: {file.FileName}");
                    return ServiceResult<bool>.CreateBoolSuccess(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error validating file: {file?.FileName}");
                    return ServiceResult<bool>.CreateBoolFailure($"Validation failed: {ex.Message}");
                }
            }

            // Additional helper method to get file info by filename
            public async Task<ServiceResult<UploadedFile>> GetFileInfoAsync(string fileName)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        return ServiceResult<UploadedFile>.CreateFailure("File name is required");
                    }

                    var filePath = Path.Combine(_uploadPath, fileName);
                    var absolutePath = Path.GetFullPath(filePath);

                    if (!File.Exists(absolutePath))
                    {
                        return ServiceResult<UploadedFile>.CreateFailure("File not found");
                    }

                    var fileInfo = new FileInfo(absolutePath);
                    var uploadedFile = new UploadedFile
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = fileName,
                        FilePath = absolutePath,  // Use absolute path
                        FileSize = fileInfo.Length,
                        UploadedAt = fileInfo.CreationTimeUtc,
                        ContentType = "application/pdf"
                    };

                    return ServiceResult<UploadedFile>.CreateSuccess(uploadedFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error getting file info: {fileName}");
                    return ServiceResult<UploadedFile>.CreateFailure($"Failed to get file info: {ex.Message}");
                }
            }

            // Method to get just the filenames without full paths
           public async Task<ServiceResult<List<string>>> GetUploadedFileNamesAsync()
{
    try
    {
        var files = await Task.Run(() =>
            Directory.GetFiles(_uploadPath, "*.pdf", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(fileName => fileName != null)  // Filter out any null values
                    .Select(fileName => fileName!)        // Use null-forgiving operator since we filtered nulls
                    .ToList());

        _logger.LogInformation($"Found {files.Count} uploaded files");
        return ServiceResult<List<string>>.CreateSuccess(files);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving uploaded file names");
        return ServiceResult<List<string>>.CreateFailure($"Retrieval failed: {ex.Message}");
    }
}
        }
    }