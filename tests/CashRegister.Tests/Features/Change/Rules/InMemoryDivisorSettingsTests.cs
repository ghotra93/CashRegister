using CashRegister.Features.Change.Rules;

namespace CashRegister.Tests.Features.Change.Rules;

public sealed class InMemoryDivisorSettingsTests
{
    [Fact]
    public void Current_Initially_IsThree()
    {
        new InMemoryDivisorSettings().Current.ShouldBe(3);
    }

    [Theory]
    [Trait("AC", "AC-025")]
    [InlineData(1)]
    [InlineData(5)]
    public void Change_PositiveInteger_ChangesCurrent(int divisor)
    {
        var settings = new InMemoryDivisorSettings();

        settings.Change(divisor);

        settings.Current.ShouldBe(divisor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Change_LessThanOne_ThrowsAndKeepsCurrent(int divisor)
    {
        var settings = new InMemoryDivisorSettings();

        Should.Throw<ArgumentOutOfRangeException>(() => settings.Change(divisor));

        settings.Current.ShouldBe(3);
    }
}
