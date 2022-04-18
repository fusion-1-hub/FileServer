using Grpc.Net.Client;
using Fusion1.FileServerHost.FileService;
using Google.Protobuf;

namespace Fusion1.FileServerClient
{
    static class MainFunctions
    {
        public static async Task<int> TransferFileAsync(TransferDirection direction, string fileName, string URL)
        {
            switch (direction)
            {
                case TransferDirection.Up:
                    await UploadFile(fileName, URL);
                    break;
                case TransferDirection.Down:
                    await DownloadFile(fileName, URL);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), $"Not expected direction value: {direction}");
            }
            return 1;
        }

        private static async Task DownloadFile(string fileName, string URL)
        {
            Console.WriteLine("Download  " + fileName);

            var currentDirectory = Directory.GetCurrentDirectory();
            var filePath = $"{currentDirectory}{Path.DirectorySeparatorChar}{fileName}";
            var client2 = new DownloadFileClient(URL);
            FileStream? writeStream = null;
            writeStream = File.Create(Path.Combine(filePath));

            var fileStream = client2.DownloadFile(fileName);

            await foreach (var fileStreamChunk in fileStream)
            {
                await writeStream.WriteAsync(fileStreamChunk.Data.Memory);
            }

            writeStream.Close();
            writeStream.Dispose();


            Console.WriteLine("Download  " + fileName + " complete");
        }

        private static async Task UploadFile(string fileName, string URL)
        {
            const int ChunkSize = 1024 * 32; // 32 KB

            using var channel = GrpcChannel.ForAddress(URL);

            var client = new UploadFileService.UploadFileServiceClient(channel);

            Console.WriteLine("Starting call");
            var call = client.UploadFile();

            Console.WriteLine("Sending file metadata");
            await call.RequestStream.WriteAsync(new UploadFileRequest
            {
                Metadata = new FileMetadata
                {
                    FileName = fileName
                }
            });



            var buffer = new byte[ChunkSize];
            await using var readStream = File.OpenRead(fileName);

            while (true)
            {
                var count = await readStream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }

                Console.WriteLine("Sending file data chunk of length " + count);
                await call.RequestStream.WriteAsync(new UploadFileRequest
                {
                    Data = UnsafeByteOperations.UnsafeWrap(buffer.AsMemory(0, count))
                });
            }

            Console.WriteLine("Complete request");
            await call.RequestStream.CompleteAsync();

            var response = await call;
            Console.WriteLine("Upload id: " + response.Status);

        }

    }
}
