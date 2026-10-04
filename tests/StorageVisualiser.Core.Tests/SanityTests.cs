using Shouldly;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class SanityTests
{
    [Fact]
    public void Environment_IsWorking()
    {
        true.ShouldBeTrue();
    }
}
