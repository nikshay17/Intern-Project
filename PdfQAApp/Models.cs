using System.ComponentModel.DataAnnotations;

namespace PdfQAApp.Models
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }

        // Generic factory methods
        public static ServiceResult<T> CreateSuccess(T data)
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data,
                Error = null
            };
        }

        public static ServiceResult<T> CreateFailure(string error)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Data = default(T),
                Error = error
            };
        }

        // Specific factory method for bool type
        public static ServiceResult<bool> CreateBoolFailure(string error)
        {
            return new ServiceResult<bool>
            {
                Success = false,
                Data = false,
                Error = error
            };
        }

        public static ServiceResult<bool> CreateBoolSuccess(bool data = true)
        {
            return new ServiceResult<bool>
            {
                Success = true,
                Data = data,
                Error = null
            };
        }
    }

    public class PythonCommand
    {
        public string Action { get; set; } //like process pdfs
        public object Data { get; set; }
    }

    public class UploadedFile
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public string ContentType { get; set; }
    }

    public class ProcessedDocument
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int PageCount { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string Status { get; set; }
    }

    public class QuestionAnswerResult
    {
        public string Answer { get; set; }
        public List<string> Sources { get; set; }
        public double Confidence { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class QAHistory
    {
        public string Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public List<string> Sources { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Request DTOs
    public class ProcessPdfsRequest
    {
        [Required] //must be there
        public List<string> PdfPaths { get; set; }
    }

    public class AskQuestionRequest
    {
        [Required]
        [StringLength(1000, MinimumLength = 3)]
        public string Question { get; set; }
    }

    public class ExportRequest
    {
        [Required]
        public List<QAHistory> QAHistory { get; set; }
        public string OutputPath { get; set; }
    }

    // Response DTOs
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class FileUploadResponse
    {
        public string Message { get; set; }
        public List<UploadedFile> Files { get; set; }
        public int Count { get; set; }
    }

    public class CleanupRequest
{
    public List<string> FilePaths { get; set; }
    public bool ClearAll { get; set; }
    public bool ForceNewSession { get; set; }
}

    public class ProcessPdfsResponse
    {
        public string Message { get; set; }
        public object Result { get; set; }
        public int DocumentsProcessed { get; set; }
    }

    public class AskQuestionResponse
    {
        public string Question { get; set; }
        public string Answer { get; set; }
        public List<string> Sources { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DocumentsResponse
    {
        public List<ProcessedDocument> Documents { get; set; }
        public int Count { get; set; }
    }

    public class ExportResponse
    {
        public string Message { get; set; }
        public string FilePath { get; set; }
        public int ItemsExported { get; set; }
    }
}