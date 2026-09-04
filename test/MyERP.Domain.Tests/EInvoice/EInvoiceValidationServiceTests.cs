using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.EInvoice.Services;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.EInvoice;

/// <summary>
/// Unit tests for EInvoiceValidationService pre-submission validations.
/// Verifies rules migrated from myinvois original.py and submit_purchase.py.
/// </summary>
public class EInvoiceValidationServiceTests
{
    private readonly IRepository<Company, Guid> _companyRepository = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepository = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepository = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly EInvoiceValidationService _validator;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public EInvoiceValidationServiceTests()
    {
        _validator = new EInvoiceValidationService(_companyRepository, _customerRepository, _supplierRepository, _salesInvoiceRepository, _purchaseInvoiceRepository);

        _companyRepository.GetAsync(_companyId).Returns(new Company(_companyId, "Test Corp")
        {
            TaxId = "C1234567890",
            MsicCode = "62010",
            RegistrationNumber = "202001012345",
            Address = "123 Tech Park, Cyberjaya",
            Country = "MYS"
        });

        _customerRepository.FindAsync(_customerId).Returns(new Customer(_customerId, _companyId, "Test Customer")
        {
            Tin = "C9876543210",
            RegistrationNumber = "202101054321",
            Country = "MYS"
        });

        _supplierRepository.FindAsync(_supplierId).Returns(new Supplier(_supplierId, _companyId, "Test Supplier")
        {
            Tin = "C1122334455",
            RegistrationNumber = "201901099887",
            Country = "MYS"
        });
    }

    [Fact]
    public async Task ValidateSalesInvoice_ValidStandardInvoice_ReturnsNoErrors()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-001", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Consulting", 1, 100m, 6m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.BuyerTin = "C9876543210";

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidateSalesInvoice_ReturnWithoutOriginalInvoice_ReturnsError()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "CRN-001", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(_itemId, "Refund item", -1, 50m, 0m);
        invoice.Submit();
        invoice.EInvoiceDocType = EInvoiceDocumentType.CreditNote;
        invoice.ReturnAgainstId = null; // Missing original reference

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("must reference an original invoice"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_NonReturnWithCreditNoteType_ReturnsError()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-002", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Item", 1, 50m, 0m);
        invoice.Submit();
        invoice.IsReturn = false;
        invoice.EInvoiceDocType = EInvoiceDocumentType.CreditNote; // Mismatch

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("Credit Note or Refund Note type code can only be used on return invoices"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_AllOrNothingTaxTemplate_Mismatch_ReturnsError()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-003", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Item 1", 1, 100m, 6m);
        invoice.AddItem(Guid.NewGuid(), "Item 2", 1, 50m, 0m);
        invoice.Items[0].TaxCategoryId = Guid.NewGuid();
        invoice.Items[1].TaxCategoryId = null;
        invoice.Submit();

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("all items must have a Tax Category/Template"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_ForeignCurrency_ZeroExchangeRate_ReturnsError()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-004", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Item", 1, 100m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "USD";
        invoice.ExchangeRate = 0m; // Invalid exchange rate

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("conversion rate must be greater than zero"));
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_ValidSelfBilledInvoice_ReturnsNoErrors()
    {
        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-001", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Raw material", 10, 20m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.SupplierTin = "C1122334455";
        invoice.EInvoiceDocType = EInvoiceDocumentType.SelfBilledInvoice;

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_ReturnWithoutOriginal_ReturnsError()
    {
        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-002", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(_itemId, "Return item", -2, 20m, 0m);
        invoice.Submit();
        invoice.EInvoiceDocType = EInvoiceDocumentType.SelfBilledCreditNote;
        invoice.ReturnAgainstId = null;

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("must reference an original purchase invoice"));
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_AllOrNothingTaxTemplate_Mismatch_ReturnsError()
    {
        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-003", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Part 1", 1, 100m, 6m);
        invoice.AddItem(Guid.NewGuid(), "Part 2", 1, 50m, 0m);
        invoice.Items[0].TaxCategoryId = Guid.NewGuid();
        invoice.Items[1].TaxCategoryId = null;
        invoice.Submit();

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("all items must have a Tax Category/Template"));
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_NonReturnWithSelfBilledCreditNoteType_ReturnsError()
    {
        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-004", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Item", 1, 50m, 0m);
        invoice.Submit();
        invoice.IsReturn = false;
        invoice.EInvoiceDocType = EInvoiceDocumentType.SelfBilledCreditNote;

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("Self-Billed Credit Note or Refund Note type code can only be used on return purchase invoices"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_ReturnWithoutOriginalInvoice_AllowedWhenCompanySettingEnabled()
    {
        var companyWithSetting = new Company(_companyId, "Test Corp")
        {
            TaxId = "C1234567890",
            MsicCode = "62010",
            RegistrationNumber = "202001012345",
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        _companyRepository.GetAsync(_companyId).Returns(companyWithSetting);

        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "CRN-002", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(_itemId, "Refund item", -1, 50m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.BuyerTin = "C9876543210";
        invoice.EInvoiceDocType = EInvoiceDocumentType.CreditNote;
        invoice.ReturnAgainstId = null; // No original reference

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.DoesNotContain(errors, e => e.Contains("must reference an original invoice"));
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_ReturnWithoutOriginalInvoice_AllowedWhenCompanySettingEnabled()
    {
        var companyWithSetting = new Company(_companyId, "Test Corp")
        {
            TaxId = "C1234567890",
            MsicCode = "62010",
            RegistrationNumber = "202001012345",
            AllowCreditNoteWithoutOriginalInvoice = true
        };
        _companyRepository.GetAsync(_companyId).Returns(companyWithSetting);

        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-CRN-002", DateTime.UtcNow);
        invoice.IsReturn = true;
        invoice.AddItem(_itemId, "Return item", -2, 20m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.SupplierTin = "C1122334455";
        invoice.EInvoiceDocType = EInvoiceDocumentType.SelfBilledCreditNote;
        invoice.ReturnAgainstId = null;

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.DoesNotContain(errors, e => e.Contains("must reference an original purchase invoice"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_DebitNoteWithoutOriginalInvoice_ReturnsErrorWhenSettingDisabled()
    {
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "DBN-001", DateTime.UtcNow);
        invoice.IsReturn = false;
        invoice.AddItem(_itemId, "Underbilled price correction", 1, 30m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.BuyerTin = "C9876543210";
        invoice.EInvoiceDocType = EInvoiceDocumentType.DebitNote; // Type 03
        invoice.ReturnAgainstId = null;

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("Debit Note must reference an original invoice"));
    }

    [Fact]
    public async Task ValidateSalesInvoice_DebitNoteWithOriginalInvoiceWithoutLhdnUuid_ReturnsError()
    {
        var originalId = Guid.NewGuid();
        var origInvoice = new SalesInvoice(originalId, _companyId, _customerId, "SINV-000", DateTime.UtcNow)
        {
            LhdnUuid = null // Not yet submitted to LHDN
        };
        _salesInvoiceRepository.FindAsync(originalId).Returns(origInvoice);

        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "DBN-002", DateTime.UtcNow);
        invoice.IsReturn = false;
        invoice.AddItem(_itemId, "Price increase", 1, 25m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.BuyerTin = "C9876543210";
        invoice.EInvoiceDocType = EInvoiceDocumentType.DebitNote;
        invoice.ReturnAgainstId = originalId;

        var errors = await _validator.ValidateForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("must have a valid LHDN submission (LhdnUuid) before its Debit Note can be submitted"));
    }

    [Fact]
    public async Task ValidatePurchaseInvoice_SelfBilledDebitNoteWithoutOriginalInvoice_ReturnsErrorWhenSettingDisabled()
    {
        var invoice = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-DBN-001", DateTime.UtcNow);
        invoice.IsReturn = false;
        invoice.AddItem(_itemId, "Supplier surcharge", 1, 40m, 0m);
        invoice.Submit();
        invoice.CurrencyCode = "MYR";
        invoice.ExchangeRate = 1m;
        invoice.SupplierTin = "C1122334455";
        invoice.EInvoiceDocType = EInvoiceDocumentType.SelfBilledDebitNote; // Type 13
        invoice.ReturnAgainstId = null;

        var errors = await _validator.ValidatePurchaseInvoiceForSubmissionAsync(invoice, _companyId);
        Assert.Contains(errors, e => e.Contains("Self-billed Debit Note must reference an original purchase invoice"));
    }
}
