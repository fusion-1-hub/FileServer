using Grpc.Core;

namespace Fusion1.FileServerHost.FileService;

public class FileServiceUpload : UploadFileService.UploadFileServiceBase
{
    private readonly ILogger<FileServiceUpload> _logger;
    private readonly IConfiguration _configuration;

    public FileServiceUpload(ILogger<FileServiceUpload> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }
    public override async Task <UploadFileResponse> UploadFile(IAsyncStreamReader<UploadFileRequest> requestStream, ServerCallContext context)
    {

        _ = await requestStream.MoveNext();
        if (requestStream.Current != null)
        {
            //Filename proviced continue otherwise invalid
            _logger.LogDebug($"Incoming request to UploadFile {requestStream.Current.Metadata.FileName}");
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            var response = new UploadFileResponse();
            response.Status = Status.Pending;

            var fileStore = _configuration["FileStore"];
            var filePath = $"{fileStore}{Path.DirectorySeparatorChar}";

            FileStream? writeStream = File.Create(Path.Combine(filePath, requestStream.Current.Metadata.FileName));

            await foreach (var message in requestStream.ReadAllAsync())
            {
                if (message.Data != null)
                {
                    await writeStream.WriteAsync(message.Data.Memory);
                }
            }
            writeStream.Close();
            writeStream.Dispose();
            watch.Stop();
            _logger.LogDebug($"Uploaded file in {watch.ElapsedMilliseconds} ms.");
        }


        return new UploadFileResponse
        {
            Status = Status.Success
        };
    }
}
