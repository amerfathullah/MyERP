using System;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class BankAndChequePrintTemplateTests
{
    [Fact]
    public void Bank_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var bank = new Bank(id, "Maybank", "MBBEMYKL", "https://www.maybank2u.com.my", isActive: true);

        Assert.Equal(id, bank.Id);
        Assert.Equal("Maybank", bank.BankName);
        Assert.Equal("MBBEMYKL", bank.SwiftNumber);
        Assert.Equal("https://www.maybank2u.com.my", bank.Website);
        Assert.True(bank.IsActive);
    }

    [Fact]
    public void ChequePrintTemplate_Creation_And_GenerateHtml_WorksCorrectly()
    {
        var id = Guid.NewGuid();
        var template = new ChequePrintTemplate(id, "Maybank", ChequeSize.Regular, 20.00m, 9.00m)
        {
            IsAccountPayable = true,
            MessageToShow = "Acc. Payee Only",
            ChequeWidth = 20.00m,
            ChequeHeight = 9.00m,
            DateDistFromTopEdge = 1.00m,
            DateDistFromLeftEdge = 15.00m,
            PayerNameFromTopEdge = 2.00m,
            PayerNameFromLeftEdge = 3.00m,
            AmtInWordsFromTopEdge = 3.00m,
            AmtInWordsFromLeftEdge = 4.00m,
            AmtInFiguresFromTopEdge = 3.50m,
            AmtInFiguresFromLeftEdge = 16.00m
        };

        Assert.Equal("Maybank", template.BankName);
        Assert.Equal(ChequeSize.Regular, template.ChequeSize);
        Assert.True(template.IsAccountPayable);

        var html = template.GenerateHtmlTemplate();
        Assert.Contains("Acc. Payee Only", html);
        Assert.Contains("width:20.00cm;height:9.00cm;", html);
        Assert.Contains("{{ reference_date }}", html);
        Assert.Contains("{{ party_name }}", html);
        Assert.Contains("{{ amount_in_words }}", html);
        Assert.Contains("{{ amount_in_figures }}", html);
    }
}
