
using System.Threading.Tasks;
using PdfQAApp.Models;


namespace PdfQAApp.Services
{
    public interface IPythonPdfService
    {
        Task<ServiceResult<object>> InitializeAsync();
        Task<ServiceResult<object>> ProcessPdfsAsync(List<string> pdfPaths);
        Task<ServiceResult<QuestionAnswerResult>> AskQuestionAsync(string question);
        Task<ServiceResult<List<ProcessedDocument>>> GetDocumentsAsync();
        Task<ServiceResult<string>> ExportQAAsync(List<QAHistory> qaHistory, string outputPath = null);
        Task<ServiceResult<object>> TestConnectionAsync();
        Task<ServiceResult<object>> ClearDocumentsAsync();
    }

}