using System.Threading.Tasks;

namespace StorageVisualiser.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await CliCommandRunner.RunAsync(args);
    }
}
