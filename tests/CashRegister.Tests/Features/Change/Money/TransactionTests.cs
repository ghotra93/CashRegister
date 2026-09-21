using CashRegister.Features.Change.Money;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Money;

public sealed class TransactionTests
{
    private static readonly UsdCurrency Usd = new();

    [Fact]
    [Trait("AC", "AC-005")]
    public void ChangeDue_IsPaidMinusOwed()
    {
        var transaction = new Transaction(owed: 212, paid: 300, Usd);

        transaction.ChangeDue.ShouldBe(88);
    }

    [Fact]
    public void ChangeDue_ExactPayment_IsZero()
    {
        new Transaction(owed: 300, paid: 300, Usd).ChangeDue.ShouldBe(0);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void Constructor_NegativeAmount_Throws(long owed, long paid)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Transaction(owed, paid, Usd));
    }

    [Fact]
    public void Constructor_PaidLessThanOwed_Throws()
    {
        Should.Throw<ArgumentException>(() => new Transaction(owed: 300, paid: 200, Usd));
    }
}
