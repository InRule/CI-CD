using System.IO;
using System.Threading.Tasks;

namespace InRule.DevOps.Helpers.Models
{
    public interface IFileRepository
    {
        Task DownloadFilesFromRepo(string fileExtension);
        Task DownloadFilesFromRepo(string fileExtension, string moniker);
        Task<string> UploadFileToRepo(string fileContent, string fileName);
        Task<string> UploadFileToRepo(string fileContent, string fileName, string moniker);
        Task<string> UploadFileToRepo(Stream fileContentStream, string fileName);
        Task<string> UploadFileToRepo(Stream fileContentStream, string fileName, string moniker);
    }
}
