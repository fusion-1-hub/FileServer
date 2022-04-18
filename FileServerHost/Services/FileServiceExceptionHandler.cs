using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Fusion1.FileServerHost.ExceptionHandler
{
    public class FileServiceExceptionHandler : Interceptor
    {
        private readonly ILogger<FileServiceExceptionHandler> _logger;

        public FileServiceExceptionHandler(ILogger<FileServiceExceptionHandler> logger)
        {
            _logger = logger;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                return await continuation(request, context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"An error occured when calling {context.Method}");
                var httpContext = context.GetHttpContext();
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                throw new RpcException(new Status(StatusCode.Internal, e.StackTrace));
            }
        }
    }
}
