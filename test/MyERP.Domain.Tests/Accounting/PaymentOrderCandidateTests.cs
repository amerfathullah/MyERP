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
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Payment Order candidate payment queries (Payment Requests and Payment Entries).
/// Verifies rules migrated from erpnext/accounts/doctype/payment_order/payment_order.js (Gotcha #6005).
/// </summary>
public class PaymentOrderCandidateTests
{
    private readonly IRepository<PaymentOrder, Guid> _orderRepository = Substitute.For<IRepository<PaymentOrder, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepository = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<BankAccount, Guid> _bankAccountRepository = Substitute.For<IRepository<BankAccount, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<PaymentRequest, Guid> _paymentRequestRepository = Substitute.For<IRepository<PaymentRequest, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IDocumentNumberGenerator _numberGenerator = Substitute.For<IDocumentNumberGenerator>();

    private readonly PaymentOrderAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();

    public PaymentOrderCandidateTests()
    {
        _appService = new PaymentOrderAppService(
            _orderRepository,
            _supplierRepository,
            _bankAccountRepository,
            _fiscalYearRepository,
            _journalEntryRepository,
            _paymentRequestRepository,
            _paymentEntryRepository,
            _numberGenerator);
    }

    [Fact]
    public async Task GetCandidatePaymentRequestsAsync_FiltersToInitiatedOutwardAndExcludesExisting()
    {
        var supplierId = Guid.NewGuid();

        var validPrId = Guid.NewGuid();
        var alreadyLinkedPrId = Guid.NewGuid();
        var draftPrId = Guid.NewGuid();
        var inwardPrId = Guid.NewGuid();

        var validPr = new PaymentRequest(validPrId, _companyId, "PurchaseOrder", Guid.NewGuid(), supplierId, "Supplier", 1000m)
        {
            PaymentRequestType = "Outward"
        };
        validPr.Submit(); // Initiated

        var alreadyLinkedPr = new PaymentRequest(alreadyLinkedPrId, _companyId, "PurchaseOrder", Guid.NewGuid(), supplierId, "Supplier", 500m)
        {
            PaymentRequestType = "Outward"
        };
        alreadyLinkedPr.Submit();

        var draftPr = new PaymentRequest(draftPrId, _companyId, "PurchaseOrder", Guid.NewGuid(), supplierId, "Supplier", 300m)
        {
            PaymentRequestType = "Outward"
        }; // Draft

        var inwardPr = new PaymentRequest(inwardPrId, _companyId, "SalesInvoice", Guid.NewGuid(), Guid.NewGuid(), "Customer", 800m)
        {
            PaymentRequestType = "Inward"
        };
        inwardPr.Submit();

        var allPrs = new List<PaymentRequest> { validPr, alreadyLinkedPr, draftPr, inwardPr };
        _paymentRequestRepository.GetQueryableAsync().Returns(Task.FromResult(allPrs.AsQueryable()));

        var existingOrder = new PaymentOrder(Guid.NewGuid(), _companyId, PaymentOrderType.PaymentRequest, DateTime.UtcNow, _bankAccountId);
        existingOrder.AddReference("PaymentRequest", alreadyLinkedPrId, 500m, supplierId, "Bank Transfer", _bankAccountId);

        var allOrders = new List<PaymentOrder> { existingOrder };
        _orderRepository.WithDetailsAsync().Returns(Task.FromResult(allOrders.AsQueryable()));

        var candidates = await _appService.GetCandidatePaymentRequestsAsync(_companyId);

        Assert.Single(candidates);
        Assert.Equal(validPrId, candidates[0].Id);
        Assert.Equal(1000m, candidates[0].GrandTotal);
    }

    [Fact]
    public async Task GetCandidatePaymentEntriesAsync_FiltersToSubmittedNonReceiveAndExcludesExisting()
    {
        var supplierId = Guid.NewGuid();

        var validPeId = Guid.NewGuid();
        var alreadyLinkedPeId = Guid.NewGuid();
        var receivePeId = Guid.NewGuid();
        var draftPeId = Guid.NewGuid();

        var validPe = new PaymentEntry(validPeId, _companyId, PaymentType.Pay, DateTime.UtcNow,
            2000m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyId = supplierId,
            PartyType = "Supplier"
        };
        validPe.Submit();

        var alreadyLinkedPe = new PaymentEntry(alreadyLinkedPeId, _companyId, PaymentType.Pay, DateTime.UtcNow,
            1500m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyId = supplierId,
            PartyType = "Supplier"
        };
        alreadyLinkedPe.Submit();

        var receivePe = new PaymentEntry(receivePeId, _companyId, PaymentType.Receive, DateTime.UtcNow,
            800m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyId = Guid.NewGuid(),
            PartyType = "Customer"
        };
        receivePe.Submit();

        var draftPe = new PaymentEntry(draftPeId, _companyId, PaymentType.Pay, DateTime.UtcNow,
            400m, Guid.NewGuid(), Guid.NewGuid())
        {
            PartyId = supplierId,
            PartyType = "Supplier"
        }; // Draft

        var allPes = new List<PaymentEntry> { validPe, alreadyLinkedPe, receivePe, draftPe };
        _paymentEntryRepository.GetQueryableAsync().Returns(Task.FromResult(allPes.AsQueryable()));

        var existingOrder = new PaymentOrder(Guid.NewGuid(), _companyId, PaymentOrderType.PaymentEntry, DateTime.UtcNow, _bankAccountId);
        existingOrder.AddReference("PaymentEntry", alreadyLinkedPeId, 1500m, supplierId, "Bank Transfer", _bankAccountId);

        var allOrders = new List<PaymentOrder> { existingOrder };
        _orderRepository.WithDetailsAsync().Returns(Task.FromResult(allOrders.AsQueryable()));

        var candidates = await _appService.GetCandidatePaymentEntriesAsync(_companyId);

        Assert.Single(candidates);
        Assert.Equal(validPeId, candidates[0].Id);
        Assert.Equal(2000m, candidates[0].PaidAmount);
    }
}
