using System.Diagnostics;
using System.Text.Json;
using Oxide.Core.Verification;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        var options = CommandLineOptions.Parse(args);
        using var workspace = new WorkspaceService();
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await workspace.OpenAsync(new WorkspaceConfiguration(
            options.GameRoot,
            options.ModRoot,
            options.WorkspaceName));
        stopwatch.Stop();

        var summary = CorpusSummaryBuilder.Build(snapshot, stopwatch.Elapsed);
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        if (options.OutputPath is not null)
        {
            var outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, json + Environment.NewLine);
            Console.Error.WriteLine($"Corpus summary written to {outputPath}");
        }

        return 0;
    }
    catch (CommandLineException exception)
    {
        Console.Error.WriteLine(exception.Message);
        Console.Error.WriteLine(CommandLineOptions.Usage);
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Corpus summary failed: {exception.Message}");
        return 1;
    }
}

internal sealed record CommandLineOptions(
    string GameRoot,
    string? ModRoot,
    string? WorkspaceName,
    string? OutputPath)
{
    public const string Usage = "Usage: dotnet run --project Oxide.CorpusSummary -- --game-root <path> [--mod-root <path>] [--name <name>] [--output <path>]";

    public static CommandLineOptions Parse(string[] args)
    {
        string? gameRoot = null;
        string? modRoot = null;
        string? name = null;
        string? output = null;

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            if (index + 1 >= args.Length)
            {
                throw new CommandLineException($"Option '{option}' requires a value.");
            }

            var value = args[++index];
            switch (option)
            {
                case "--game-root": gameRoot = value; break;
                case "--mod-root": modRoot = value; break;
                case "--name": name = value; break;
                case "--output": output = value; break;
                default: throw new CommandLineException($"Unknown option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            throw new CommandLineException("--game-root is required.");
        }

        return new CommandLineOptions(gameRoot, modRoot, name, output);
    }
}

internal sealed class CommandLineException(string message) : Exception(message);
