using Oxide.Core.Workspaces.Documents;

namespace Oxide.Tests.Workspaces;

public sealed class VirtualPathTests
{
    [Theory]
    [InlineData("history/states/1-Test.txt")]
    [InlineData("/history//states\\1-Test.txt")]
    public void Virtual_path_normalizes_separators(string input)
    {
        var path = new VirtualPath(input);

        Assert.Equal("history/states/1-Test.txt", path.Value);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("history/../outside.txt")]
    [InlineData("./state.txt")]
    public void Virtual_path_rejects_traversal_segments(string input)
    {
        Assert.Throws<ArgumentException>(() => new VirtualPath(input));
    }
}
