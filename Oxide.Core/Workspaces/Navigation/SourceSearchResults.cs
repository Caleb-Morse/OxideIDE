using System.Collections.Immutable;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Navigation;

public sealed record SourceSearchResults(
    string Query,
    ImmutableArray<TextSpan> Matches,
    bool IsTruncated)
{
    public bool HasMatches => !Matches.IsEmpty;
}
