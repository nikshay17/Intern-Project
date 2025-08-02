using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PdfQAApp.Models;

namespace PdfQAApp.Services
{
    public interface IFileUploadService
    {
        Task<ServiceResult<List<UploadedFile>>> UploadFilesAsync(IFormFileCollection files);
        Task<ServiceResult<UploadedFile>> UploadFileAsync(IFormFile file);
        Task<ServiceResult<bool>> DeleteFileAsync(string fileName);
        Task<ServiceResult<List<UploadedFile>>> GetUploadedFilesAsync();
        Task<ServiceResult<List<string>>> GetUploadedFileNamesAsync();
        Task<ServiceResult<UploadedFile>> GetFileInfoAsync(string fileName);
        Task<ServiceResult<bool>> ValidateFileAsync(IFormFile file);
    }
}