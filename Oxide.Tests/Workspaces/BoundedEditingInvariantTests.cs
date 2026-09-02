using System.Text;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Editing;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Workspaces;

public sealed class BoundedEditingInvariantTests
{
    [Fact]
    public async Task Randomized_length_changing_scalar_edits_are_lossless_bounded_and_never_touch_disk()
    {
        const int scenarioCount = 75;
        var random = new Random(0x11_07_51);
        using var fixture = new TemporaryWorkspace();
        var originals = new Dictionary<int, byte[]>();
        for (var stateId = 1; stateId <= scenarioCount; stateId++)
        {
            var newline = stateId % 2 == 0 ? "\r\n" : "\n";
            var source = stateId % 3 == 0
                ? $"state = {{ id = {stateId} manpower = {random.Next(1, 999)} state_category = rural }} # state {stateId}"
                : $"state = {{{newline}\tid = {stateId}{newline}\t# retained {stateId}{newline}\tmanpower = {random.Next(1, 999)}{newline}\tstate_category = rural{newline}}}{newline}";
            var bytes = Encoding.UTF8.GetBytes(source);
            if (stateId % 4 == 0)
            {
                bytes = Encoding.UTF8.GetPreamble().Concat(bytes).ToArray();
            }

            var path = fixture.WriteModFile($"history/states/{stateId}-Test.txt", source);
            File.WriteAllBytes(path, bytes);
            originals.Add(stateId, bytes);
        }

        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        foreach (var stateId in Enumerable.Range(1, scenarioCount))
        {
            var state = snapshot.Semantics.States[stateId];
            var manpower = state.Manpower!;
            var category = state.StateCategory!;
            var newManpower = random.NextInt64(1_000, 9_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var newCategory = $"category_{random.Next(1, 10_000)}";
            var document = snapshot.DocumentsById[manpower.Provenance.DocumentId];
            var changes = new[]
            {
                new TextChange(manpower.Provenance.Span, newManpower),
                new TextChange(category.Provenance.Span, newCategory),
            };
            var edit = new WorkspaceEdit(
                WorkspaceEditId.Create(),
                snapshot.Version,
                $"Randomized edit {stateId}",
                [new DocumentEdit(EditCapabilityEvaluator.CreateTarget(snapshot, document.Id), changes)]);

            var prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit);
            var result = Assert.Single(prepared.Documents);

            Assert.True(prepared.IsValid);
            Assert.Equal(ApplyReference(document.Text!.Text, changes), result.UpdatedSource.Text);
            Assert.Equal(document.Text.Encoding, result.UpdatedSource.Encoding);
            Assert.Equal(document.Text.Newlines, result.UpdatedSource.Newlines);
            Assert.NotNull(result.SyntaxTree);
            Assert.Equal(originals[stateId], File.ReadAllBytes(document.PhysicalPath));
        }
    }

    private static string ApplyReference(string source, IEnumerable<TextChange> changes)
    {
        var result = source;
        foreach (var change in changes.OrderByDescending(change => change.Span.Start))
        {
            result = result.Remove(change.Span.Start, change.Span.Length)
                .Insert(change.Span.Start, change.Replacement);
        }

        return result;
    }
}
