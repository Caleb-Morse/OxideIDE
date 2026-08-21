using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public interface ISemanticEntity
{
    EntityId Id { get; }

    SemanticEntityStatus Status { get; }
}
