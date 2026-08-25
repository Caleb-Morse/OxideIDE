using System.Collections.Immutable;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record SemanticInvalidationPlan
{
    private static readonly ImmutableArray<SemanticRefreshDomain> AllDomains =
        Enum.GetValues<SemanticRefreshDomain>().ToImmutableArray();

    private SemanticInvalidationPlan(
        ImmutableHashSet<ContentCategory> changedCategories,
        ImmutableHashSet<SemanticRefreshDomain> rebuiltDomains)
    {
        ChangedCategories = changedCategories;
        RebuiltDomains = AllDomains.Where(rebuiltDomains.Contains).ToImmutableArray();
        ReusedDomains = AllDomains.Where(domain => !rebuiltDomains.Contains(domain)).ToImmutableArray();
    }

    public ImmutableHashSet<ContentCategory> ChangedCategories { get; }

    public ImmutableArray<SemanticRefreshDomain> RebuiltDomains { get; }

    public ImmutableArray<SemanticRefreshDomain> ReusedDomains { get; }

    public bool Rebuilds(SemanticRefreshDomain domain) => RebuiltDomains.Contains(domain);

    public bool Changes(ContentCategory category) => ChangedCategories.Contains(category);

    public static SemanticInvalidationPlan Create(
        IEnumerable<DocumentChange> changes,
        bool rebuildAll = false)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var categories = changes
            .Select(change => change.Category)
            .ToImmutableHashSet();
        if (rebuildAll)
        {
            return new SemanticInvalidationPlan(
                Enum.GetValues<ContentCategory>().ToImmutableHashSet(),
                AllDomains.ToImmutableHashSet());
        }

        var domains = ImmutableHashSet.CreateBuilder<SemanticRefreshDomain>();
        if (categories.Contains(ContentCategory.Localisation))
        {
            domains.Add(SemanticRefreshDomain.Localisations);
        }

        if (categories.Contains(ContentCategory.CountryTags))
        {
            domains.Add(SemanticRefreshDomain.Countries);
            domains.Add(SemanticRefreshDomain.States);
            domains.Add(SemanticRefreshDomain.StateStrategicRegionMemberships);
        }

        if (categories.Contains(ContentCategory.StateHistory))
        {
            domains.Add(SemanticRefreshDomain.States);
            domains.Add(SemanticRefreshDomain.StateStrategicRegionMemberships);
        }

        if (categories.Contains(ContentCategory.StrategicRegion))
        {
            domains.Add(SemanticRefreshDomain.StrategicRegions);
            domains.Add(SemanticRefreshDomain.ProvinceStrategicRegionIndex);
            domains.Add(SemanticRefreshDomain.StateStrategicRegionMemberships);
        }

        return new SemanticInvalidationPlan(categories, domains.ToImmutable());
    }
}
