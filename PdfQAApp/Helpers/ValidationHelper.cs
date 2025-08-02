using System.ComponentModel.DataAnnotations;

namespace PdfQAApp.Helpers
{
    public static class ValidationHelper
    {
        public static bool IsValidPdfFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            // Check file extension
            var allowedExtensions = new[] { ".pdf" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
                return false;

            // Check file size (max 50MB)
            const int maxFileSize = 50 * 1024 * 1024;
            if (file.Length > maxFileSize)
                return false;

            // Check MIME type
            var allowedMimeTypes = new[] { "application/pdf" };
            if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                return false;

            return true;
        }

        public static bool IsValidQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            // Check minimum length
            if (question.Trim().Length < 3)
                return false;

            // Check maximum length
            if (question.Length > 1000)
                return false;

            return true;
        }

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unnamed_file";

            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

            // Ensure it's not empty after sanitization
            if (string.IsNullOrWhiteSpace(sanitized))
                return "unnamed_file";

            return sanitized;
        }

        public static string GenerateUniqueFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            var sanitizedName = SanitizeFileName(nameWithoutExtension);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            
            return $"{sanitizedName}_{timestamp}_{uniqueId}{extension}";
        }
    }

    public class FileUploadValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is IFormFile file)
            {
                return ValidationHelper.IsValidPdfFile(file);
            }
            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return "Please upload a valid PDF file (max 50MB).";
        }
    }

    public class QuestionValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            if (value is string question)
            {
                return ValidationHelper.IsValidQuestion(question);
            }
            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return "Question must be between 3 and 1000 characters.";
        }
    }
}