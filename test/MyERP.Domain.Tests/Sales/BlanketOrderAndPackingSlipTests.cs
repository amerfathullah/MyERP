using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Unit tests for Blanket Order duplicate items check, party item code, and Packing Slip DN draft guards.
/// Verifies rules migrated from erpnext/selling/doctype/blanket_order and packing_slip (Gotchas #4153, #4160).
/// </summary>
public class BlanketOrderAndPackingSlipTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    [Fact]
    public void BlanketOrder_AddItem_DuplicateItem_ThrowsValidationException()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), _companyId, "BO-2026-0001", "Selling", _partyId, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));
        bo.AddItem(_itemId, 100m, 50m, "Widget A", "CUST-ITEM-01");

        // Attempt to add same ItemId again
        var ex = Assert.Throws<BusinessException>(() => bo.AddItem(_itemId, 50m, 50m, "Widget A"));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void BlanketOrder_AddItem_StoresPartyItemCode()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), _companyId, "BO-2026-0002", "Selling", _partyId, DateTime.UtcNow, DateTime.UtcNow.AddMonths(6));
        bo.AddItem(_itemId, 100m, 50m, "Widget A", "CUST-ITEM-01");

        Assert.Single(bo.Items);
        Assert.Equal("CUST-ITEM-01", bo.Items[0].PartyItemCode);
    }

    [Fact]
    public async Task PackingSlip_CreateAndSubmit_NonDraftDN_ThrowsValidationException()
    {
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var psRepo = Substitute.For<IRepository<PackingSlip, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var service = new PackingSlipAppService(psRepo, dnRepo, itemRepo);

        var dnId = Guid.NewGuid();
        var dn = new DeliveryNote(dnId, _companyId, Guid.NewGuid(), Guid.NewGuid(), "DN-2026-0001", DateTime.UtcNow);
        dn.AddItem(_itemId, "Widget", 10m, 50m, 0m);
        dn.Submit(); // Status = Submitted

        dnRepo.FindAsync(dnId).Returns(dn);
        dnRepo.GetAsync(dnId).Returns(dn);

        var createDto = new CreatePackingSlipDto
        {
            CompanyId = _companyId,
            DeliveryNoteId = dnId,
            FromCaseNo = 1,
            ToCaseNo = 2,
            GrossWeightKg = 10m,
            Items = new List<CreatePackingSlipItemDto>
            {
                new() { ItemId = _itemId, Qty = 5m, NetWeight = 2m }
            }
        };

        // Create on submitted DN must be blocked
        await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(createDto));
    }
}
