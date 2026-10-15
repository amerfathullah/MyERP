using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.EInvoice;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Application.Tests.EInvoice;

/// <summary>
/// Unit tests for company and invoice LHDN submission enable guards per MyInvois commit d9adf36.
/// Verifies that invoice submission wrappers and validation gracefully bypass / report errors
/// when LHDN invoice submission is not enabled in company settings or when document is not checked for submission.
/// </summary>
public class CompanyLhdnSubmissionGuardTests
{
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepo = Substitute.For<IRepository<EInvoiceSubmission, Guid>>();
    private readonly IRepository<EInvoiceConsolidation, Guid> _consolidationRepo = Substitute.For<IRepository<EInvoiceConsolidation, Guid>>();
    private readonly IRepository<LhdnSuccessLog, Guid> _successLogRepo = Substitute.For<IRepository<LhdnSuccessLog, Guid>>();

    private readonly EInvoiceValidationService _validationService;
    private readonly EInvoiceAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _invoiceId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public CompanyLhdnSubmissionGuardTests()
    {
        _validationService = new EInvoiceValidationService(
            _companyRepo, _customerRepo, _supplierRepo, _salesInvoiceRepo, _purchaseInvoiceRepo, _consolidationRepo);

        var lhdnApiClient = Substitute.For<ILhdnApiClient>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var guidGenerator = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        var eInvoiceService = new EInvoiceService(lhdnApiClient, _submissionRepo, _successLogRepo);
        var docBuilder = new InvoiceDocumentBuilder();
        var docSigner = new InvoiceDocumentSigner();
        var consolidationService = new EInvoiceConsolidationService(_salesInvoiceRepo, _consolidationRepo, _customerRepo, guidGenerator);
        var taxpayerValidationService = new TaxpayerValidationService(lhdnApiClient, settingProvider);

        _appService = new EInvoiceAppService(
            eInvoiceService,
            docBuilder,
            docSigner,
            _validationService,
            consolidationService,
            taxpayerValidationService,
            _submissionRepo,
            _consolidationRepo,
            _successLogRepo,
            _salesInvoiceRepo,
            _purchaseInvoiceRepo,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            settingProvider);
    }

    [Fact]
    public async Task SubmitAsync_WhenCompanyLhdnDisabled_ReturnsNotSubmittedWithoutThrowing()
    {
        // Arrange
        var company = new Company(_companyId, "Disabled LHDN Co")
        {
            EnableLhdnInvoice = false
        };
        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));

        // Act
        var result = await _appService.SubmitAsync(new SubmitEInvoiceDto
        {
            CompanyId = _companyId,
            SourceDocumentType = "SalesInvoice",
            SourceDocumentId = _invoiceId
        });

        // Assert
        result.Status.ShouldBe("NotSubmitted");
    }

    [Fact]
    public async Task SubmitAsync_WhenInvoiceIsSubmitToLhdnFalse_ReturnsNotSubmitted()
    {
        // Arrange
        var company = new Company(_companyId, "Active LHDN Co")
        {
            EnableLhdnInvoice = true,
            TaxId = "C1234567890",
            MsicCode = "01111",
            RegistrationNumber = "202001012345"
        };
        var invoice = new SalesInvoice(_invoiceId, _companyId, _customerId, "SINV-001", DateTime.UtcNow)
        {
            IsSubmitToLhdn = false // explicitly not for submission
        };

        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));
        _salesInvoiceRepo.GetAsync(_invoiceId, includeDetails: true).Returns(Task.FromResult(invoice));

        // Act
        var result = await _appService.SubmitAsync(new SubmitEInvoiceDto
        {
            CompanyId = _companyId,
            SourceDocumentType = "SalesInvoice",
            SourceDocumentId = _invoiceId
        });

        // Assert
        result.Status.ShouldBe("NotSubmitted");
    }

    [Fact]
    public async Task BatchSubmitAsync_WhenCompanyLhdnDisabled_SkipsAllDocuments()
    {
        // Arrange
        var company = new Company(_companyId, "Disabled LHDN Co")
        {
            EnableLhdnInvoice = false
        };
        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));

        // Act
        var result = await _appService.BatchSubmitAsync(new BatchSubmitEInvoiceDto
        {
            CompanyId = _companyId,
            SourceDocumentType = "SalesInvoice",
            DocumentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        });

        // Assert
        result.TotalRequested.ShouldBe(2);
        result.SkippedCount.ShouldBe(2);
        result.SucceededCount.ShouldBe(0);
        result.Results.ShouldAllBe(r => r.ErrorMessage != null && r.ErrorMessage.Contains("not enabled in company settings"));
    }

    [Fact]
    public async Task BatchSubmitAsync_WhenInvoiceIsSubmitToLhdnFalse_SkipsDocument()
    {
        // Arrange
        var company = new Company(_companyId, "Active LHDN Co")
        {
            EnableLhdnInvoice = true
        };
        var doc1Id = Guid.NewGuid();
        var invoice = new SalesInvoice(doc1Id, _companyId, _customerId, "SINV-2026-001", DateTime.UtcNow)
        {
            IsSubmitToLhdn = false,
        };

        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));
        _salesInvoiceRepo.FindAsync(doc1Id).Returns(Task.FromResult<SalesInvoice?>(invoice));

        // Act
        var result = await _appService.BatchSubmitAsync(new BatchSubmitEInvoiceDto
        {
            CompanyId = _companyId,
            SourceDocumentType = "SalesInvoice",
            DocumentIds = new List<Guid> { doc1Id }
        });

        // Assert
        result.TotalRequested.ShouldBe(1);
        result.SkippedCount.ShouldBe(1);
        result.Results[0].ErrorMessage.ShouldNotBeNull();
        result.Results[0].ErrorMessage!.ShouldContain("Submit to LHDN is not checked");
    }

    [Fact]
    public async Task ValidationService_ReportsErrors_WhenCompanyDisabledOrNotChecked()
    {
        // Arrange
        var company = new Company(_companyId, "Disabled LHDN Co")
        {
            EnableLhdnInvoice = false,
            TaxId = "C1234567890",
            MsicCode = "01111",
            RegistrationNumber = "202001012345"
        };
        var invoice = new SalesInvoice(_invoiceId, _companyId, _customerId, "SINV-002", DateTime.UtcNow)
        {
            IsSubmitToLhdn = false
        };

        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(company));

        // Act
        var errors = await _validationService.ValidateForSubmissionAsync(invoice, _companyId);

        // Assert
        errors.ShouldContain(e => e.Contains("LHDN Invoice Submission is not enabled for company"));
        errors.ShouldContain(e => e.Contains("Invoice is not marked for submission to LHDN"));
    }
}
