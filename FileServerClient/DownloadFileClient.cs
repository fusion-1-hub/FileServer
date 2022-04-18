using Grpc.Core;
using Grpc.Net.Client;
using Fusion1.FileServerHost.FileService;

namespace Fusion1.FileServerClient
{
    public class DownloadFileClient : IDownloadFileClient
    {
        private readonly GrpcChannel _channel;
        private readonly DownloadFileService.DownloadFileServiceClient _client;

        public DownloadFileClient(string URL)
        {
            _channel = GrpcChannel.ForAddress(URL);
            _client = new DownloadFileService.DownloadFileServiceClient(_channel);
        }

        public IAsyncEnumerable<DownloadFileResponse> DownloadFile(string fileName)
        {
            var request = _client.DownloadFile(new DownloadFileRequest()
            {
                FileName = fileName
            });
            return request.ResponseStream.ReadAllAsync();
        }
    }
}
