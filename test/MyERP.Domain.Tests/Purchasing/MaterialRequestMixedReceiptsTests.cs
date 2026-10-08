using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Domain unit tests for Material Request mixed receipt quantities combination
/// per ERPNext PR #60208 / commit ba48a9d0ad.
/// </summary>
public class MaterialRequestMixedReceiptsTests
{
    private readonly IRepository<MaterialRequest, Guid> _mrRepository;
    private readonly MaterialRequestManager _mrManager;

    public MaterialRequestMixedReceiptsTests()
    {
        _mrRepository = Substitute.For<IRepository<MaterialRequest, Guid>>();
        _mrManager = new MaterialRequestManager(_mrRepository);
    }

    [Fact]
    public void UpdateFulfillmentStatus_TransitionsToCompleted_WhenPerReceivedReaches100()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-0001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Raw Steel", 10m, "Kg");
        mr.Submit();

        mr.Status.ShouldBe(DocumentStatus.Submitted);
        mr.PerReceived.ShouldBe(0m);

        // Receive partial
        mr.Items[0].ReceivedQuantity = 4m;
        mr.UpdateFulfillmentStatus();
        mr.PerReceived.ShouldBe(40m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);

        // Receive remainder
        mr.Items[0].ReceivedQuantity = 10m;
        mr.UpdateFulfillmentStatus();
        mr.PerReceived.ShouldBe(100m);
        mr.Status.ShouldBe(DocumentStatus.Completed);

        // Reversal (cancel receipt)
        mr.Items[0].ReceivedQuantity = 4m;
        mr.UpdateFulfillmentStatus();
        mr.PerReceived.ShouldBe(40m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task UpdateReceiptFulfillmentForItemsAsync_CombinesMixedReceiptQuantities()
    {
        var mrId = Guid.NewGuid();
        var mr = new MaterialRequest(mrId, Guid.NewGuid(), "MR-0002", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Bearings", 10m, "Nos");
        mr.Submit();
        var mrItem = mr.Items[0];

        var mrList = new List<MaterialRequest> { mr };
        _mrRepository.GetQueryableAsync().Returns(Task.FromResult(mrList.AsQueryable()));

        // Receipt 1 (Purchase Receipt: 4 Nos)
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 4m) }, reverse: false);
        mrItem.ReceivedQuantity.ShouldBe(4m);
        mr.PerReceived.ShouldBe(40m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);

        // Receipt 2 (Purchase Invoice with UpdateStock=true: 6 Nos)
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: false);
        mrItem.ReceivedQuantity.ShouldBe(10m);
        mr.PerReceived.ShouldBe(100m);
        mr.Status.ShouldBe(DocumentStatus.Completed);

        // Cancel Receipt 2
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: true);
        mrItem.ReceivedQuantity.ShouldBe(4m);
        mr.PerReceived.ShouldBe(40m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);

        // Cancel Receipt 1
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 4m) }, reverse: true);
        mrItem.ReceivedQuantity.ShouldBe(0m);
        mr.PerReceived.ShouldBe(0m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);
    }

    [Fact]
    public async Task UpdateReceiptFulfillmentForItemsAsync_InverseOrder_CombinesIdentically()
    {
        var mrId = Guid.NewGuid();
        var mr = new MaterialRequest(mrId, Guid.NewGuid(), "MR-0003", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Bearings", 10m, "Nos");
        mr.Submit();
        var mrItem = mr.Items[0];

        var mrList = new List<MaterialRequest> { mr };
        _mrRepository.GetQueryableAsync().Returns(Task.FromResult(mrList.AsQueryable()));

        // Stock-updating Invoice first: 6 Nos
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: false);
        mrItem.ReceivedQuantity.ShouldBe(6m);
        mr.PerReceived.ShouldBe(60m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);

        // Purchase Receipt second: 4 Nos
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 4m) }, reverse: false);
        mrItem.ReceivedQuantity.ShouldBe(10m);
        mr.PerReceived.ShouldBe(100m);
        mr.Status.ShouldBe(DocumentStatus.Completed);
    }

    [Fact]
    public async Task UpdateReceiptFulfillmentForItemsAsync_WithOverOrderAllowance_CapsPerReceivedAt100()
    {
        var mrId = Guid.NewGuid();
        var mr = new MaterialRequest(mrId, Guid.NewGuid(), "MR-0004", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(Guid.NewGuid(), "Fasteners", 10m, "Nos");
        mr.Submit();
        var mrItem = mr.Items[0];

        var mrList = new List<MaterialRequest> { mr };
        _mrRepository.GetQueryableAsync().Returns(Task.FromResult(mrList.AsQueryable()));

        // Receipt 1: 6 Nos
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: false);
        // Receipt 2: 6 Nos (over-ordered to 12)
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: false);

        mrItem.ReceivedQuantity.ShouldBe(12m);
        mr.PerReceived.ShouldBe(100m);
        mr.Status.ShouldBe(DocumentStatus.Completed);

        // Cancel 6 Nos
        await _mrManager.UpdateReceiptFulfillmentForItemsAsync(new[] { (mrItem.Id, 6m) }, reverse: true);
        mrItem.ReceivedQuantity.ShouldBe(6m);
        mr.PerReceived.ShouldBe(60m);
        mr.Status.ShouldBe(DocumentStatus.Submitted);
    }
}
