using System;
using MyERP.Core;
using MyERP.Core.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Core;

/// <summary>
/// Unit tests for LetterHead and Address entity properties:
/// - LetterHead supports DocType vs Report with default tracking (Gotcha #147)
/// - Address supports TaxCategory and IsYourCompanyAddress native fields (Gotcha #399)
/// </summary>
public class LetterHeadAndAddressTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void LetterHead_DocTypeAndReport_CategoriesTrackedSeparately()
    {
        var docTypeLh = new LetterHead(Guid.NewGuid(), _companyId, "Invoice Header", LetterHeadFor.DocType)
        {
            IsDefault = true,
            HeaderContent = "<h1>MyERP Invoicing</h1>"
        };

        var reportLh = new LetterHead(Guid.NewGuid(), _companyId, "P&L Report Header", LetterHeadFor.Report)
        {
            IsDefault = true,
            HeaderContent = "<h1>Financial Statement</h1>"
        };

        Assert.Equal(LetterHeadFor.DocType, docTypeLh.LetterHeadFor);
        Assert.True(docTypeLh.IsDefault);

        Assert.Equal(LetterHeadFor.Report, reportLh.LetterHeadFor);
        Assert.True(reportLh.IsDefault);
    }

    [Fact]
    public void Address_TaxCategoryAndIsYourCompanyAddress_StoredCorrectly()
    {
        var addr = new Address(
            Guid.NewGuid(),
            "HQ Office",
            "Company",
            _companyId,
            "123 Tech Park",
            "Malaysia"
        )
        {
            TaxCategory = "01 : Sales Tax",
            IsYourCompanyAddress = true
        };

        Assert.Equal("01 : Sales Tax", addr.TaxCategory);
        Assert.True(addr.IsYourCompanyAddress);
    }
}
