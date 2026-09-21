using CashRegister.Features.Change.Parsing;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Parsing;

public sealed class TransactionLineParserTests
{
    private static readonly UsdCurrency Usd = new();

    /// <summary>A currency whose decimal separator is an apostrophe, to prove the separator comes from the currency.</summary>
    private sealed class ApostropheCurrency() : Currency("APO", '\'', 2, [new("unit", "units", 1)]);

    private static LineParseResult Parse(string line) => TransactionLineParser.Parse(line, Usd);

    [Fact]
    public void Parse_ValidLine_ReturnsTransactionInCents()
    {
        var result = Parse("2.12,3.00");

        result.Error.ShouldBeNull();
        result.Transaction.ShouldNotBeNull();
        result.Transaction.Owed.ShouldBe(212);
        result.Transaction.Paid.ShouldBe(300);
        result.Transaction.Currency.ShouldBe(Usd);
    }

    [Theory]
    [InlineData(" 2.12 , 3.00 ", 212, 300)]
    [InlineData("5,6", 500, 600)]
    [InlineData("0.5,1.5", 50, 150)]
    [InlineData("0,0", 0, 0)]
    public void Parse_AcceptedShapes_ConvertToCents(string line, long owed, long paid)
    {
        var transaction = Parse(line).Transaction;

        transaction.ShouldNotBeNull();
        transaction.Owed.ShouldBe(owed);
        transaction.Paid.ShouldBe(paid);
    }

    [Theory]
    [Trait("AC", "AC-017")]
    [InlineData("abc")]
    [InlineData("1.00")]
    [InlineData("1,2,3")]
    [InlineData("1.00;2.00")]
    [InlineData("1.00,")]
    [InlineData(",2.00")]
    [InlineData("$1.00,2.00")]
    [InlineData("1.,2.00")]
    [InlineData(".50,2.00")]
    [InlineData("1,000.00,2,000.00")]
    [InlineData("99999999999999999999,1")]
    [InlineData("1.2.3,4.00")]
    [InlineData("1.a0,2.00")]
    public void Parse_Malformed_ReturnsInvalidLineError(string line)
    {
        var result = Parse(line);

        result.Transaction.ShouldBeNull();
        result.Error.ShouldBe("Error: invalid line, expected '<owed>,<paid>'");
    }

    [Theory]
    [Trait("AC", "AC-018")]
    [InlineData("-1.00,2.00")]
    [InlineData("1.00,-2.00")]
    public void Parse_NegativeAmount_ReturnsNegativeError(string line)
    {
        Parse(line).Error.ShouldBe("Error: amounts must not be negative");
    }

    [Fact]
    [Trait("AC", "AC-019")]
    public void Parse_PaidLessThanOwed_ReturnsPaidLessError()
    {
        Parse("3.00,2.00").Error.ShouldBe("Error: amount paid is less than amount owed");
    }

    [Theory]
    [Trait("AC", "AC-020")]
    [InlineData("1.001,2.00")]
    [InlineData("1.00,2.999")]
    public void Parse_TooManyDecimalPlaces_ReturnsDecimalsError(string line)
    {
        Parse(line).Error.ShouldBe("Error: amounts must have at most 2 decimal places");
    }

    [Fact]
    public void Parse_NegativeAndTooManyDecimals_ReportsNegativeFirst()
    {
        Parse("-1.001,2.00").Error.ShouldBe(LineErrors.NegativeAmount);
    }

    [Fact]
    [Trait("AC", "AC-029")]
    public void Parse_UsesTheCurrencysDecimalSeparator()
    {
        var transaction = TransactionLineParser.Parse("1'50,2'00", new ApostropheCurrency()).Transaction;

        transaction.ShouldNotBeNull();
        transaction.Owed.ShouldBe(150);
        transaction.Paid.ShouldBe(200);
    }

    [Fact]
    [Trait("AC", "AC-029")]
    public void Parse_OtherCurrencysSeparator_IsInvalidForUsd()
    {
        Parse("1'50,2'00").Error.ShouldBe(LineErrors.InvalidLine);
    }
}
