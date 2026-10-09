using Shouldly;
using StorageVisualiser.Core.Formatting;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class SizeFormatterTests
{
    [Fact]
    public void Format_ExactBytes_FormatsWithSeparators()
    {
        SizeFormatter.Format(1_374_830_776, exact: true).ShouldBe("1,374,830,776 bytes");
        SizeFormatter.Format(0, exact: true).ShouldBe("0 bytes");
    }

    [Fact]
    public void Format_WindowsUnits_ScalesAccurately()
    {
        SizeFormatter.Format(500).ShouldBe("500 bytes");
        SizeFormatter.Format(1024).ShouldBe("1.00 KB");
        SizeFormatter.Format(1536).ShouldBe("1.50 KB");
        SizeFormatter.Format(1048576 * 10).ShouldBe("10.0 MB");
        SizeFormatter.Format(1073741824L * 150).ShouldBe("150 GB");
    }

    [Fact]
    public void Format_IecUnits_UsesKiBGiB()
    {
        SizeFormatter.Format(1024 * 1024, UnitSystem.Iec).ShouldBe("1.00 MiB");
        SizeFormatter.Format(1073741824L * 2, UnitSystem.Iec).ShouldBe("2.00 GiB");
    }

    [Fact]
    public void Format_SiUnits_UsesDecimalDivisor()
    {
        SizeFormatter.Format(1000, UnitSystem.Si).ShouldBe("1.00 kB");
        SizeFormatter.Format(1000_000, UnitSystem.Si).ShouldBe("1.00 MB");
    }

    [Fact]
    public void Format_DefaultUnitSystem_AppliesConfiguredDefault()
    {
        var original = SizeFormatter.DefaultUnitSystem;
        try
        {
            SizeFormatter.DefaultUnitSystem = UnitSystem.Iec;
            SizeFormatter.Format(1024 * 1024).ShouldBe("1.00 MiB");

            SizeFormatter.DefaultUnitSystem = UnitSystem.Si;
            SizeFormatter.Format(1000 * 1000).ShouldBe("1.00 MB");

            // Explicit unit parameter overrides default
            SizeFormatter.Format(1024 * 1024, UnitSystem.Windows).ShouldBe("1.00 MB");
        }
        finally
        {
            SizeFormatter.DefaultUnitSystem = original;
        }
    }
}
