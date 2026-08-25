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

        var summary = CorpusSummaryBuilder.Build(
            snapshot,
            stopwatch.Elapsed,
            new CorpusSummaryOptions(options.Language, options.EnglishFallbackEnabled));
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
        WriteHumanSummary(summary);

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

static void WriteHumanSummary(CorpusSummary summary)
{
    var localisation = summary.Localisation;
    var slowestStage = new[]
        {
            ("discovery", summary.WorkspacePerformance.DiscoveryMilliseconds),
            ("document loading and parsing", summary.WorkspacePerformance.DocumentLoadingMilliseconds),
            ("semantic construction", summary.WorkspacePerformance.SemanticBuildingMilliseconds),
            ("name projection", localisation.NameProjectionMilliseconds),
        }
        .OrderByDescending(stage => stage.Item2)
        .First();
    Console.Error.WriteLine($"{summary.WorkspaceName}: {summary.DocumentsLoaded:N0}/{summary.FilesDiscovered:N0} documents loaded; {summary.DocumentsFailed:N0} failed.");
    Console.Error.WriteLine($"Languages: {(localisation.LanguagesDiscovered.Length == 0 ? "none" : string.Join(", ", localisation.LanguagesDiscovered))}.");
    Console.Error.WriteLine($"Names ({localisation.EffectiveLanguage}, English fallback {(localisation.EnglishFallbackEnabled ? "on" : "off")}): " +
        $"{localisation.StateNames.Exact + localisation.CountryNames.Exact + localisation.StrategicRegionNames.Exact:N0} exact, " +
        $"{localisation.StateNames.EnglishFallback + localisation.CountryNames.EnglishFallback + localisation.StrategicRegionNames.EnglishFallback:N0} fallback, " +
        $"{localisation.StateNames.Unresolved + localisation.CountryNames.Unresolved + localisation.StrategicRegionNames.Unresolved:N0} unresolved.");
    var regions = summary.StrategicRegions;
    Console.Error.WriteLine($"Strategic regions: {regions.EntityCount:N0} entities from {regions.DeclarationCount:N0} declarations; " +
        $"{regions.ProvinceCandidateCount:N0} province claims, {regions.AmbiguousProvinceCount:N0} ambiguous province memberships.");
    Console.Error.WriteLine($"State memberships: {regions.StateMemberships.SingleRegion:N0} single, " +
        $"{regions.StateMemberships.Split:N0} split, {regions.StateMemberships.Partial:N0} partial, " +
        $"{regions.StateMemberships.Missing:N0} missing, {regions.StateMemberships.Ambiguous:N0} ambiguous, " +
        $"{regions.StateMemberships.NoProvinces:N0} without provinces.");
    var contributions = summary.Contributions.AllDomains;
    Console.Error.WriteLine($"Contributions: {contributions.Dispositions.Total:N0} across {contributions.IdentityCount:N0} identities; " +
        $"{contributions.Dispositions.Effective:N0} effective, {contributions.Dispositions.Shadowed:N0} shadowed, " +
        $"{contributions.Dispositions.Ambiguous:N0} ambiguous, {contributions.Dispositions.Invalid:N0} invalid, " +
        $"{contributions.Dispositions.Excluded:N0} excluded. " +
        $"Overrides: {contributions.CrossLayerOverrideCount:N0}; same-layer duplicates: {contributions.SameLayerDuplicateIdentityCount:N0}.");
    Console.Error.WriteLine($"Diagnostics: {summary.SyntaxDiagnosticCount:N0} syntax, {summary.SemanticDiagnosticCount:N0} semantic. Slowest stage: {slowestStage.Item1} ({slowestStage.Item2:N0} ms).");
}

internal sealed record CommandLineOptions(
    string GameRoot,
    string? ModRoot,
    string? WorkspaceName,
    string? OutputPath,
    string Language,
    bool EnglishFallbackEnabled)
{
    public const string Usage = "Usage: dotnet run --project Oxide.CorpusSummary -- --game-root <path> [--mod-root <path>] [--name <name>] [--output <path>] [--language <language>] [--no-english-fallback]";

    public static CommandLineOptions Parse(string[] args)
    {
        string? gameRoot = null;
        string? modRoot = null;
        string? name = null;
        string? output = null;
        var language = "english";
        var englishFallbackEnabled = true;

        for (var index = 0; index < args.Length; index++)
        {
            var option = args[index];
            if (option is "--no-english-fallback")
            {
                englishFallbackEnabled = false;
                continue;
            }

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
                case "--language": language = value; break;
                default: throw new CommandLineException($"Unknown option '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            throw new CommandLineException("--game-root is required.");
        }

        return new CommandLineOptions(gameRoot, modRoot, name, output, language, englishFallbackEnabled);
    }
}

internal sealed class CommandLineException(string message) : Exception(message);
