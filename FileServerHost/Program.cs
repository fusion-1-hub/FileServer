using Fusion1.FileServerHost.ExceptionHandler;
using Fusion1.FileServerHost.FileService;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Serilog.Filters;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.FileProviders;

var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
            path: $"{programData}/Fusion-1/logs/FileServerHost-.log", 
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:o} [{Level:u3}] ({SourceContext}) {Message}{NewLine}{Exception}")
    .WriteTo.Logger(lc => lc 
        .Filter.ByIncludingOnly(Matching.FromSource("Program"))
        .WriteTo.EventLog(
            "Fusion-1 File Server Host",
            outputTemplate: "{Message}{NewLine}{Exception}"))
    .CreateBootstrapLogger();
Log.ForContext<Program>().Information("Application is starting up...");

//Check if the XML license file exists
if (File.Exists($"{programData}/Fusion-1/license/license.lic"))
{
    var _status = Fusion1.Resman.Resman.Status.UNDEFINED;
    var _msg = "";
    var _lic = (Fusion1.Resman.Resource)Fusion1.Resman.Resman.ParseResourceString(
        typeof(Fusion1.Resman.Resource),
        File.ReadAllText($"{programData}/Fusion-1/license/license.lic"),
        out _status,
        out _msg);
    if (_status != Fusion1.Resman.Resman.Status.VALID)
    {
        System.Environment.Exit(1);
    }
}
else
{
    var uuid = Fusion1.Resman.Resman.GenerateUID(typeof(Program).Assembly.GetName().Name);
    var message = "This application requires a license.lic file " + 
                  $"in the {programData}\\Fusion-1\\license folder. " + 
                  $"Send the following UUID '{uuid}' to your software " +
                  "vendor to obtain the necessary license.";
    Log.ForContext<Program>().Information(message);
    System.Environment.Exit(1);
}


try
{
    var options = new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
    };

    var builder = WebApplication.CreateBuilder(options);
    builder.Host.UseSerilog((ctx, lc) => lc
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
                path: $"{programData}/Fusion-1/logs/FileServerHost-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:o} [{Level:u3}] ({SourceContext}) {Message}{NewLine}{Exception}")
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(Matching.FromSource("Program"))
            .WriteTo.EventLog(
                "Fusion-1 File Server Host",
                outputTemplate: "{Message}{NewLine}{Exception}"))
        .ReadFrom.Configuration(ctx.Configuration))
        .UseWindowsService();

    builder.WebHost.ConfigureKestrel((context, serverOptions) =>
    {
        serverOptions.ConfigureEndpointDefaults(listenOptions =>
        {
            listenOptions.UseHttps(GetHttpsCertificateFromLocalMachineStore(context.Configuration["CertificateThumbprint"]));
        });
    });

    builder.Services.AddGrpc();
    builder.Services.AddGrpc(options =>
    {
        options.Interceptors.Add<FileServiceExceptionHandler>();
    });

    builder.Services.AddGrpcReflection();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Fusion-1 File Server API",
            Version = "v1"
        });
    });

    var app = builder.Build();

    app.UseStaticFiles(new StaticFileOptions()
    {
        FileProvider = new PhysicalFileProvider(
            Path.Combine(Directory.GetCurrentDirectory(), "Content")),
            RequestPath = "/Content"
    });


    app.UseSerilogRequestLogging();

    app.UseRouting();
    app.UseSwagger();

    app.UseEndpoints(endpoints =>
    {
    //endpoints.MapMagicOnionHttpGateway("_", app.Services.GetService<MagicOnion.Server.MagicOnionServiceDefinition>().MethodHandlers, GrpcChannel.ForAddress("https://localhost:5001"));
    //endpoints.MapMagicOnionSwagger("swagger", app.Services.GetService<MagicOnion.Server.MagicOnionServiceDefinition>().MethodHandlers, "/_/");
    endpoints.MapGrpcService<FileServiceUpload>();
        endpoints.MapGrpcService<FileServiceDownload>();
        endpoints.MapGrpcReflectionService();
        //endpoints.MapGet("/", async context =>
        //{
        //    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        //});
    });
    //app.MapPost("/todoitems", async ([FromServices] TodoDbContext dbContext, TodoItem todoItem) =>
    //{
    //    //dbContext.TodoItems.Add(todoItem);
    //    //await dbContext.SaveChangesAsync();
    //    //return Results.Created($"/todoitems/{todoItem.Id}", todoItem);
    //});

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fusion-1 File Server API v1");
        options.InjectStylesheet("/Content/css/swagger-mycustom.css");
        options.RoutePrefix = string.Empty;
    });
    Log.ForContext<Program>().Information("Application Started.");
    app.Run();
}
catch (Exception ex)
{
    Log.ForContext<Program>().Fatal(ex, "Unhandled exception");
}
finally
{
    Log.ForContext<Program>().Information("Application shut down complete.");
    Log.CloseAndFlush();
}

static X509Certificate2 GetHttpsCertificateFromLocalMachineStore(string thumbprint)
{
    using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
    {
        store.Open(OpenFlags.ReadOnly);
        var certCollection = store.Certificates;

        var currentCerts = certCollection.Find(X509FindType.FindByThumbprint, thumbprint, false);
        X509Certificate2 foundCertificate;

        if (currentCerts.Count > 0)
        {
            foundCertificate = currentCerts[0];
            foundCertificate.GetRSAPrivateKey();
            return foundCertificate;
        }
        throw new InvalidOperationException($"No certificate was found in the {nameof(StoreLocation.LocalMachine)} store with thumbprintt '{thumbprint}'.");
    }
}

