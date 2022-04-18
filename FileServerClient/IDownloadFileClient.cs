using Fusion1.FileServerHost.FileService;

namespace Fusion1.FileServerClient
{
    public interface IDownloadFileClient
    {
        public IAsyncEnumerable<DownloadFileResponse> DownloadFile(string fileName);
    }
}
