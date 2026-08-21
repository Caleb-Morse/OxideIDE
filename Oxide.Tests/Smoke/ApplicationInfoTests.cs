using Oxide.Core;

namespace Oxide.Tests.Smoke;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void Default_application_name_is_Oxide()
    {
        Assert.Equal("Oxide", ApplicationInfo.Oxide.Name);
    }
}
