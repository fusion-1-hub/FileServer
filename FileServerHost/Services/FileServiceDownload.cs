using Google.Protobuf;
using Grpc.Core;

namespace Fusion1.FileServerHost.FileService;

public class FileServiceDownload : DownloadFileService.DownloadFileServiceBase
{
    private readonly ILogger<FileServiceDownload> _logger;
    private readonly IConfiguration _configuration;

    public FileServiceDownload(ILogger<FileServiceDownload> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override async Task DownloadFile(DownloadFileRequest request,
            IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
    {
        const int ChunkSize = 1024 * 32; // 32 KB
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        _logger.LogDebug($"Starting call. Request: {context.GetHttpContext().Request.Path} , {request.FileName}");

        var fileStore = _configuration["FileStore"];
        var filePath = $"{fileStore}{Path.DirectorySeparatorChar}{request.FileName}";
        var buffer = new byte[ChunkSize];
        await using var readStream = File.OpenRead(filePath);

        while (true)
        {
            var count = await readStream.ReadAsync(buffer);
            if (count == 0)
            {
                break;
            }

            _logger.LogDebug($"Sending file data chunk of length {count}");
            await responseStream.WriteAsync(
                new DownloadFileResponse
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, count))
                }
            );
        }
        watch.Stop();
        _logger.LogDebug($"Downloaded file in {watch.ElapsedMilliseconds} ms.");
    }
}
