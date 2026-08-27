using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Payment Order bank file generation, guards, and summary calculations.
/// Verifies rules from erpnext/accounts/doctype/payment_order (#6005).
/// </summary>
public class PaymentOrderBankFileTests
{
    private readonly IRepository<PaymentOrder, Guid> _poRepo = Substitute.For<IRepository<PaymentOrder, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<BankAccount, Guid> _bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<PaymentRequest, Guid> _prRepo = Substitute.For<IRepository<PaymentRequest, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IDocumentNumberGenerator _numGen = Substitute.For<IDocumentNumberGenerator>();

    private readonly PaymentOrderAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _companyBankAccountId = Guid.NewGuid();
    private readonly Guid _supplier1Id = Guid.NewGuid();
    private readonly Guid _supplier2Id = Guid.NewGuid();
    private readonly Guid _bank1Id = Guid.NewGuid();
    private readonly Guid _bank2Id = Guid.NewGuid();

    public PaymentOrderBankFileTests()
    {
        _appService = new PaymentOrderAppService(
            _poRepo, _supplierRepo, _bankAccountRepo, _fyRepo, _jeRepo, _prRepo, _peRepo, _numGen);
    }

    [Fact]
    public async Task GenerateBankFileAsync_SubmittedOrder_GeneratesCsvContentAndCorrectTotals()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder(orderId, _companyId, PaymentOrderType.PaymentEntry, DateTime.UtcNow, _companyBankAccountId)
        {
            OrderNumber = "PO-2026-0001"
        };
        order.AddReference("PaymentEntry", Guid.NewGuid(), 1500m, _supplier1Id, "Bank Transfer", _bank1Id, "INV-001");
        order.AddReference("PaymentEntry", Guid.NewGuid(), 2500m, _supplier2Id, "Cheque", _bank2Id, "INV-002");
        order.Submit();

        var orderList = new List<PaymentOrder> { order };
        _poRepo.WithDetailsAsync().Returns(Task.FromResult(orderList.AsQueryable()));

        var suppliers = new List<Supplier>
        {
            new Supplier(_supplier1Id, _companyId, "Acme Corp"),
            new Supplier(_supplier2Id, _companyId, "Global Supplies")
        };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(suppliers.AsQueryable()));

        var bankAccounts = new List<BankAccount>
        {
            new BankAccount(_bank1Id, _companyId, "Maybank Operating", Guid.NewGuid(), "Maybank")
            {
                BankAccountNo = "111222333",
                SwiftCode = "MBBEMYKL"
            },
            new BankAccount(_bank2Id, _companyId, "CIMB Operating", Guid.NewGuid(), "CIMB")
            {
                BankAccountNo = "444555666",
                SwiftCode = "CIBBMYKL"
            }
        };
        _bankAccountRepo.GetQueryableAsync().Returns(Task.FromResult(bankAccounts.AsQueryable()));

        var result = await _appService.GenerateBankFileAsync(orderId);

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(4000m, result.TotalAmount);
        Assert.Contains("Acme Corp", result.FileContent);
        Assert.Contains("Global Supplies", result.FileContent);
        Assert.Contains("111222333", result.FileContent);
        Assert.Contains("444555666", result.FileContent);
        Assert.Contains("PO-2026-0001", result.FileName);
    }

    [Fact]
    public async Task GenerateBankFileAsync_DraftOrder_ThrowsValidationException()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder(orderId, _companyId, PaymentOrderType.PaymentEntry, DateTime.UtcNow, _companyBankAccountId)
        {
            OrderNumber = "PO-2026-DRAFT"
        };
        order.AddReference("PaymentEntry", Guid.NewGuid(), 1000m, _supplier1Id, "Bank Transfer", _bank1Id);
        // Not submitted (Draft status)

        var orderList = new List<PaymentOrder> { order };
        _poRepo.WithDetailsAsync().Returns(Task.FromResult(orderList.AsQueryable()));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.GenerateBankFileAsync(orderId));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAggregatedMetrics()
    {
        var orderId = Guid.NewGuid();
        var order = new PaymentOrder(orderId, _companyId, PaymentOrderType.PaymentEntry, DateTime.UtcNow, _companyBankAccountId)
        {
            OrderNumber = "PO-2026-0005"
        };
        order.AddReference("PaymentEntry", Guid.NewGuid(), 500m, _supplier1Id, "Bank Transfer", _bank1Id);
        order.AddReference("PaymentEntry", Guid.NewGuid(), 1200m, _supplier1Id, "Bank Transfer", _bank1Id);
        order.AddReference("PaymentEntry", Guid.NewGuid(), 300m, _supplier2Id, "Cash", _bank2Id);
        order.Submit();

        var orderList = new List<PaymentOrder> { order };
        _poRepo.WithDetailsAsync().Returns(Task.FromResult(orderList.AsQueryable()));

        var summary = await _appService.GetSummaryAsync(orderId);

        Assert.NotNull(summary);
        Assert.Equal(orderId, summary.PaymentOrderId);
        Assert.Equal(3, summary.TotalReferences);
        Assert.Equal(2000m, summary.TotalAmount);
        Assert.Equal(2, summary.DistinctSuppliersCount);
        Assert.Equal(1700m, summary.AmountByModeOfPayment["Bank Transfer"]);
        Assert.Equal(300m, summary.AmountByModeOfPayment["Cash"]);
    }
}
