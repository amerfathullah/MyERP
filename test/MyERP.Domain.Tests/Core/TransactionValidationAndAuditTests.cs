using System;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Tests.Core;

public class TransactionValidationAndAuditTests
{
    [Fact]
    public void ValidatePostingDate_Today_Succeeds()
    {
        var service = new TransactionValidationService(null!);
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date));
    }

    [Fact]
    public void ValidatePostingDate_Yesterday_Succeeds()
    {
        var service = new TransactionValidationService(null!);
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(-1)));
    }

    [Fact]
    public void ValidatePostingDate_Tomorrow_Succeeds()
    {
        // Allow 1 day into future (timezone tolerance)
        var service = new TransactionValidationService(null!);
        Should.NotThrow(() => service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void ValidatePostingDate_FarFuture_Throws()
    {
        var service = new TransactionValidationService(null!);
        Should.Throw<BusinessException>(() =>
            service.ValidatePostingDate(DateTime.UtcNow.Date.AddDays(30)));
    }

    [Fact]
    public void ValidatePostingDate_PastDate_Succeeds()
    {
        var service = new TransactionValidationService(null!);
        Should.NotThrow(() => service.ValidatePostingDate(new DateTime(2025, 1, 1)));
    }

    [Fact]
    public void DocumentActivityLog_Create_SetsFields()
    {
        var docId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesInvoice", docId,
            "Submitted", companyId,
            documentNumber: "SI-001",
            previousStatus: "Draft",
            newStatus: "Submitted",
            performedByUserId: userId,
            details: "Submitted with tax recalculation");

        log.DocumentType.ShouldBe("SalesInvoice");
        log.DocumentId.ShouldBe(docId);
        log.ActivityType.ShouldBe("Submitted");
        log.CompanyId.ShouldBe(companyId);
        log.DocumentNumber.ShouldBe("SI-001");
        log.PreviousStatus.ShouldBe("Draft");
        log.NewStatus.ShouldBe("Submitted");
        log.PerformedByUserId.ShouldBe(userId);
    }

    [Fact]
    public void DocumentActivityLog_CancelActivity()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "PurchaseOrder", Guid.NewGuid(),
            "Cancelled", Guid.NewGuid(),
            documentNumber: "PO-042",
            previousStatus: "ToDeliverAndBill",
            newStatus: "Cancelled");

        log.ActivityType.ShouldBe("Cancelled");
        log.NewStatus.ShouldBe("Cancelled");
    }

    [Fact]
    public void DocumentActivityLog_PaymentActivity()
    {
        var log = new DocumentActivityLog(
            Guid.NewGuid(), "SalesInvoice", Guid.NewGuid(),
            "PaymentReceived", Guid.NewGuid(),
            details: "RM 5,000 received via PE-001");

        log.ActivityType.ShouldBe("PaymentReceived");
        log.Details!.ShouldContain("5,000");
    }

    [Fact]
    public void ErrorCodes_TransactionValidation_Exist()
    {
        MyERPDomainErrorCodes.FuturePostingDate.ShouldBe("MyERP:01003");
        MyERPDomainErrorCodes.BaseCurrencyExchangeRateMustBeOne.ShouldBe("MyERP:01004");
        MyERPDomainErrorCodes.InvalidExchangeRate.ShouldBe("MyERP:01005");
    }
}
