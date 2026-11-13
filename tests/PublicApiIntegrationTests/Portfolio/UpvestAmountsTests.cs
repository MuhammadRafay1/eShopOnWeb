using System;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Portfolio;

[TestClass]
public class UpvestAmountsTests
{
    [DataTestMethod]
    [DataRow(100.0, "100.00")]
    [DataRow(1000.5, "1000.50")]
    [DataRow(0.01, "0.01")]
    [DataRow(1234.567, "1234.57")] // rounds to two decimals
    public void ToCashAmount_FormatsTwoDecimals(double amount, string expected)
    {
        Assert.AreEqual(expected, UpvestAmounts.ToCashAmount((decimal)amount));
    }

    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(-5.0)]
    public void ToCashAmount_RejectsNonPositive(double amount)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => UpvestAmounts.ToCashAmount((decimal)amount));
    }

    [TestMethod]
    public void ToCashAmount_RejectsTooManyIntegerDigits()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => UpvestAmounts.ToCashAmount(1_000_000_000m));
    }

    [DataTestMethod]
    [DataRow(100.0, "100")]        // exactly 100 is the literal "100"
    [DataRow(60.0, "60")]
    [DataRow(33.33333, "33.33333")]
    [DataRow(0.0, "0")]
    public void ToWeight_MatchesUpvestFormat(double weight, string expected)
    {
        Assert.AreEqual(expected, UpvestAmounts.ToWeight((decimal)weight));
    }

    [DataTestMethod]
    [DataRow(-1.0)]
    [DataRow(100.01)]
    public void ToWeight_RejectsOutOfRange(double weight)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => UpvestAmounts.ToWeight((decimal)weight));
    }

    [DataTestMethod]
    [DataRow("123.45", 123.45)]
    [DataRow("0", 0.0)]
    [DataRow(null, 0.0)]
    [DataRow("", 0.0)]
    public void ParseDecimal_RoundTrips(string? value, double expected)
    {
        Assert.AreEqual((decimal)expected, UpvestAmounts.ParseDecimal(value));
    }
}
