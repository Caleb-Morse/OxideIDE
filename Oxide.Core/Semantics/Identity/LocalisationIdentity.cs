namespace Oxide.Core.Semantics.Identity;

public readonly record struct LocalisationIdentity(
    LocalisationLanguage Language,
    LocalisationKey Key);
