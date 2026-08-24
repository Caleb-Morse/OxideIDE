using System.Collections.Immutable;

namespace Oxide.Core.Workspaces.Configuration;

public sealed record WorkspaceConfiguration
{
    public WorkspaceConfiguration(string gameRoot, string? activeModRoot = null, string? displayName = null)
        : this(CreateCompatibleLayers(gameRoot, activeModRoot), displayName)
    {
    }

    public WorkspaceConfiguration(IEnumerable<ContentLayer> layers, string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(layers);
        Layers = NormalizeLayers(layers);
        var baseGame = Layers.FirstOrDefault(layer => layer.Kind is ContentLayerKind.BaseGame);
        GameRoot = baseGame?.RootPath ?? Layers[0].RootPath;
        ActiveModRoot = Layers
            .Where(layer => layer.Kind is ContentLayerKind.Mod)
            .OrderBy(layer => layer.Position)
            .LastOrDefault()?.RootPath;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(ActiveModRoot ?? GameRoot)
            : displayName.Trim();
    }

    public string GameRoot { get; }

    public string? ActiveModRoot { get; }

    public string DisplayName { get; }

    public ImmutableArray<ContentLayer> Layers { get; }

    private static IEnumerable<ContentLayer> CreateCompatibleLayers(string gameRoot, string? activeModRoot)
    {
        yield return ContentLayer.BaseGame(gameRoot);
        if (!string.IsNullOrWhiteSpace(activeModRoot))
        {
            yield return ContentLayer.ActiveMod(activeModRoot);
        }
    }

    private static ImmutableArray<ContentLayer> NormalizeLayers(IEnumerable<ContentLayer> layers)
    {
        var normalized = layers
            .Select((layer, index) => NormalizeLayer(layer, index))
            .OrderBy(layer => layer.Position)
            .ThenBy(layer => layer.Id.Value, StringComparer.Ordinal)
            .ToImmutableArray();
        if (normalized.IsEmpty)
        {
            throw new ArgumentException("A workspace must contain at least one content layer.", nameof(layers));
        }

        var duplicateId = normalized
            .GroupBy(layer => layer.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new ArgumentException($"Content layer ID '{duplicateId.Key}' is duplicated.", nameof(layers));
        }

        var duplicatePosition = normalized
            .GroupBy(layer => layer.Position)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePosition is not null)
        {
            throw new ArgumentException($"Content layer position '{duplicatePosition.Key}' is duplicated.", nameof(layers));
        }

        return normalized;
    }

    private static ContentLayer NormalizeLayer(ContentLayer layer, int index)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (string.IsNullOrWhiteSpace(layer.Id.Value))
        {
            throw new ArgumentException($"Content layer at index {index} must have an ID.", nameof(layer));
        }

        if (string.IsNullOrWhiteSpace(layer.DisplayName))
        {
            throw new ArgumentException($"Content layer at index {index} must have a display name.", nameof(layer));
        }

        if (layer.Position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layer), "Content layer positions cannot be negative.");
        }

        return layer with
        {
            DisplayName = layer.DisplayName.Trim(),
            RootPath = NormalizeRoot(layer.RootPath, nameof(layer.RootPath)),
        };
    }

    private static string NormalizeRoot(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}
