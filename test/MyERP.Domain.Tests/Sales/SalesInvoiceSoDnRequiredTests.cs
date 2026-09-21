using System;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Sales;

public class SalesInvoiceSoDnRequiredTests
{
    private static SalesInvoice NewInvoice()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-SODN-1", DateTime.UtcNow.Date);
        invoice.AddItem(Guid.NewGuid(), "Widget", 1, 100m, 0m);
        return invoice;
    }

    [Fact]
    public void ValidateSoRequired_UnlinkedItem_Throws()
        => Should.Throw<BusinessException>(() => SalesInvoiceManager.ValidateSoRequired(NewInvoice(), true));

    [Fact]
    public void ValidateSoRequired_UpdateStockInvoice_StillRequiresSalesOrder()
    {
        var invoice = NewInvoice();
        invoice.UpdateStock = true;
        Should.Throw<BusinessException>(() => SalesInvoiceManager.ValidateSoRequired(invoice, true));
    }

    [Fact]
    public void ValidateSoRequired_PosInvoice_Skipped()
    {
        var invoice = NewInvoice();
        invoice.IsPos = true;
        Should.NotThrow(() => SalesInvoiceManager.ValidateSoRequired(invoice, true));
    }

    [Fact]
    public void ValidateDnRequired_UpdateStockInvoice_Skipped()
    {
        var invoice = NewInvoice();
        invoice.UpdateStock = true;
        Should.NotThrow(() => SalesInvoiceManager.ValidateDnRequired(invoice, true));
    }
}
