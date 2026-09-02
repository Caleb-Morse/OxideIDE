namespace Oxide.Core.Workspaces.Editing;

public enum EditRefusalReason
{
    ReadOnlyLayer,
    StaleSnapshot,
    AmbiguousDeclaration,
    MalformedSource,
    UnsupportedEncoding,
    UnsupportedOperation,
    ExternalConflict,
    MissingProvenance,
    FailedDocument,
    NoChangeRequired,
}

public sealed record EditCapability
{
    private EditCapability(bool isEditable, EditRefusalReason? refusalReason, string explanation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        if (isEditable == refusalReason.HasValue)
        {
            throw new ArgumentException("Editable capabilities cannot have a refusal reason, and refused capabilities require one.");
        }

        IsEditable = isEditable;
        RefusalReason = refusalReason;
        Explanation = explanation.Trim();
    }

    public bool IsEditable { get; }

    public EditRefusalReason? RefusalReason { get; }

    public string Explanation { get; }

    public static EditCapability Editable(string explanation) => new(true, null, explanation);

    public static EditCapability Refused(EditRefusalReason reason, string explanation) => new(false, reason, explanation);
}
