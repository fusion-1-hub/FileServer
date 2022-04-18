using CommandLine;
using Fusion1.FileServerClient;

public enum TransferDirection
{
    Up,
    Down
}

public class Options
{
    [Option('u', "URL", Required = true, Default = "https://localhost:5001", HelpText = "File Server URL in format https://[HostName]:[Port].")]
    public string URL { get; set; }

    [Option('t', "TransferDirection", Required = true, HelpText = "Transfer direction Up for Upload Down for download.")]
    public TransferDirection TransferDirection { get; set; }

    [Option('f', "FileName", Required = true, HelpText = "File name with path if necessary.")]
    public string FileName { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
    public bool Verbose { get; set; }
}
public class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            var result = await Parser.Default.ParseArguments<Options>(args)
                .MapResult((Options o) =>
                   MainFunctions.TransferFileAsync(o.TransferDirection, o.FileName, o.URL),
                   e => Task.FromResult(-1));
            watch.Stop();
            if (result == 1)
            {
                Console.WriteLine($"\nTransferred file in {watch.ElapsedMilliseconds} ms.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            Console.WriteLine("\nPress any key to continue.");
            Console.ReadKey();
        }
    }
}