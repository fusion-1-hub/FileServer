using Fusion1.FileServerHost.ExceptionHandler;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Filters;
using Serilog.Sinks.Syslog;
using System.Security.Cryptography.X509Certificates;

// ── Bootstrap ────────────────────────────────────────────────────────────────

var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

Log.Logger = BuildLoggerConfiguration(programData).CreateBootstrapLogger();
Log.ForContext<Program>().Information("Application is starting up...");

ValidateLicense(programData);

// ── Host ─────────────────────────────────────────────────────────────────────

try
{
  var options = new WebApplicationOptions
  {
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService()
          ? AppContext.BaseDirectory
          : default
  };

  var builder = WebApplication.CreateBuilder(options);

  builder.Host
      .UseSerilog((ctx, lc) => BuildLoggerConfiguration(programData)
          .ReadFrom.Configuration(ctx.Configuration))
      .UseWindowsService();

  builder.WebHost.ConfigureKestrel((context, serverOptions) =>
  {
    var thumbprint = context.Configuration["CertificateThumbprint"]
        ?? throw new InvalidOperationException(
            "CertificateThumbprint is missing from configuration.");

    serverOptions.ConfigureEndpointDefaults(listenOptions =>
        listenOptions.UseHttps(GetHttpsCertificateFromLocalMachineStore(thumbprint)));
  });

  // ── Services ─────────────────────────────────────────────────────────────

  builder.Services
      .AddGrpc(options => options.Interceptors.Add<FileServiceExceptionHandler>())
      .Services
      .AddGrpcReflection()
      .AddEndpointsApiExplorer()
      .AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
      {
        Title = "Fusion-1 File Server API",
        Version = "v1"
      }));

  // ── Middleware ────────────────────────────────────────────────────────────

  var app = builder.Build();

  app.UseStaticFiles(new StaticFileOptions
  {
    FileProvider = new PhysicalFileProvider(
          Path.Combine(Directory.GetCurrentDirectory(), "Content")),
    RequestPath = "/Content"
  });

  app.UseSerilogRequestLogging();
  app.UseRouting();
  app.UseSwagger();
  app.UseSwaggerUI(options =>
  {
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fusion-1 File Server API v1");
    options.InjectStylesheet("/Content/css/swagger-mycustom.css");
    options.RoutePrefix = string.Empty;
  });

  Log.ForContext<Program>().Information("Application started.");
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

// ── Local functions ───────────────────────────────────────────────────────────

static LoggerConfiguration BuildLoggerConfiguration(string programData)
{
  var config = new LoggerConfiguration()
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File(
          path: $"{programData}/Fusion-1/logs/FileServerHost-.log",
          rollingInterval: RollingInterval.Day,
          retainedFileCountLimit: 7,
          outputTemplate: "{Timestamp:o} [{Level:u3}] ({SourceContext}) {Message}{NewLine}{Exception}")
      .WriteTo.Logger(lc =>
      {
        var filtered = lc.Filter.ByIncludingOnly(Matching.FromSource("Program"));

        if (OperatingSystem.IsWindows())
          filtered.WriteTo.EventLog(
                source: "Fusion-1 File Server Host",
                logName: "Application",
                outputTemplate: "{Message}{NewLine}{Exception}");
        else if (OperatingSystem.IsLinux())
          filtered.WriteTo.LocalSyslog(
                appName: "Fusion-1 File Server Host",
                facility: Facility.Local0,
                outputTemplate: "{Message}{NewLine}{Exception}");
        else
          filtered.WriteTo.Console();
      });

  return config;
}

static void ValidateLicense(string programData)
{
  var licensePath = $"{programData}/Fusion-1/license/license.lic";

  if (!File.Exists(licensePath))
  {
    var uuid = Fusion1.Resman.Resman.GenerateUID(
        typeof(Program).Assembly.GetName().Name ?? "Fusion1.FileServerHost");

    Log.ForContext<Program>().Information(
        "This application requires a license.lic file in the " +
        $"{programData}\\Fusion-1\\license folder. " +
        $"Send the following UUID '{uuid}' to your software vendor " +
        "to obtain the necessary license.");

    Environment.Exit(1);
  }

  var status = Fusion1.Resman.Resman.Status.UNDEFINED;
  var msg = "";

  Fusion1.Resman.Resman.ParseResourceString(
      typeof(Fusion1.Resman.Resource),
      File.ReadAllText(licensePath),
      out status,
      out msg);

  if (status != Fusion1.Resman.Resman.Status.VALID)
    Environment.Exit(1);
}

static X509Certificate2 GetHttpsCertificateFromLocalMachineStore(string thumbprint)
{
  using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
  store.Open(OpenFlags.ReadOnly);

  var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

  if (matches.Count == 0)
    throw new InvalidOperationException(
        $"No certificate found in {nameof(StoreLocation.LocalMachine)} " +
        $"store with thumbprint '{thumbprint}'.");  // fixed typo: 'thumbprintt'

  var cert = matches[0];
  cert.GetRSAPrivateKey();
  return cert;
}