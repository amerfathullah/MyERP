using System;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Core;

public class DocumentAmendmentTests
{
    [Fact]
    public void GenerateAmendedNumber_FirstAmendment()
    {
        var service = new DocumentAmendmentService();
        var result = service.GenerateAmendedNumber("SI-001", 1);
        result.ShouldBe("SI-001-1");
    }

    [Fact]
    public void GenerateAmendedNumber_SecondAmendment()
    {
        var service = new DocumentAmendmentService();
        var result = service.GenerateAmendedNumber("SI-001-1", 2);
        result.ShouldBe("SI-001-2");
    }

    [Fact]
    public void GenerateAmendedNumber_ThirdAmendment()
    {
        var service = new DocumentAmendmentService();
        var result = service.GenerateAmendedNumber("SI-001-2", 3);
        result.ShouldBe("SI-001-3");
    }

    [Fact]
    public void GenerateAmendedNumber_ComplexNumber()
    {
        var service = new DocumentAmendmentService();
        var result = service.GenerateAmendedNumber("INV-2026-00042", 1);
        result.ShouldBe("INV-2026-00042-1");
    }

    [Fact]
    public void ValidateCanAmend_Cancelled_Succeeds()
    {
        var service = new DocumentAmendmentService();
        Should.NotThrow(() => service.ValidateCanAmend(DocumentStatus.Cancelled));
    }

    [Fact]
    public void ValidateCanAmend_Draft_Throws()
    {
        var service = new DocumentAmendmentService();
        Should.Throw<BusinessException>(() => service.ValidateCanAmend(DocumentStatus.Draft));
    }

    [Fact]
    public void ValidateCanAmend_Submitted_Throws()
    {
        var service = new DocumentAmendmentService();
        Should.Throw<BusinessException>(() => service.ValidateCanAmend(DocumentStatus.Submitted));
    }

    [Fact]
    public void SalesInvoice_AmendedFromId_DefaultNull()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        si.AmendedFromId.ShouldBeNull();
        si.AmendmentIndex.ShouldBe(0);
    }

    [Fact]
    public void SalesInvoice_CanSetAmendmentFields()
    {
        var originalId = Guid.NewGuid();
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001-1", DateTime.UtcNow);
        si.AmendedFromId = originalId;
        si.AmendmentIndex = 1;

        si.AmendedFromId.ShouldBe(originalId);
        si.AmendmentIndex.ShouldBe(1);
    }

    [Fact]
    public void IAmendable_Interface_ImplementedBySalesInvoice()
    {
        var si = new SalesInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);
        (si is IAmendable).ShouldBeTrue();
    }
}
