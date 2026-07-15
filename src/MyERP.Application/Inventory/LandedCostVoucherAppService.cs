using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Dtos;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.LandedCostVouchers.Default)]
public class LandedCostVoucherAppService : ApplicationService
{
    private readonly IRepository<LandedCostVoucher, Guid> _repository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;

    public LandedCostVoucherAppService(
        IRepository<LandedCostVoucher, Guid> repository,
        IRepository<SerialNo, Guid> serialNoRepository,
        StockValuationService valuationService,
        BinService binService)
    {
        _repository = repository;
        _serialNoRepository = serialNoRepository;
        _valuationService = valuationService;
        _binService = binService;
    }

    public async Task<PagedResultDto<LandedCostVoucherDto>> GetListAsync(GetLandedCostVoucherListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(l => l.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            query = query.Where(l => (l.VoucherNumber ?? "").ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(l => l.PostingDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<LandedCostVoucherDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<LandedCostVoucherDto> GetAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        return MapToDto(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Create)]
    public async Task<LandedCostVoucherDto> CreateAsync(CreateLandedCostVoucherDto input)
    {
        var lcv = new LandedCostVoucher(GuidGenerator.Create(), input.CompanyId,
            input.PostingDate, CurrentTenant.Id)
        {
            DistributionMethod = input.DistributionMethod,
            Notes = input.Notes,
        };

        foreach (var item in input.Items)
            lcv.AddItem(item.ReceiptId, item.ReceiptType, item.ItemId,
                item.Quantity, item.Amount, item.Description);

        foreach (var charge in input.Charges)
            lcv.AddCharge(charge.Description, charge.ExpenseAccountId, charge.Amount);

        await _repository.InsertAsync(lcv);
        return MapToDto(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Submit)]
    public async Task<LandedCostVoucherDto> SubmitAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);

        // Validate posting period is not frozen/closed before creating SLE entries
        var postingOrchestrator = LazyServiceProvider.LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(lcv.CompanyId, lcv.PostingDate, "LandedCostVoucher");

        lcv.Submit();

        // Update stock valuation: create SLE entries for the additional cost per item
        // Each item gets a zero-qty entry with value = ApplicableCharges (rate adjustment)
        foreach (var item in lcv.Items)
        {
            if (item.ApplicableCharges <= 0) continue;

            // Get the warehouse from the receipt (LCV items reference the PR warehouse)
            // Create a rate-adjustment SLE: qty=0, value=ApplicableCharges
            await _valuationService.CreateLedgerEntryAsync(
                lcv.CompanyId,
                item.ItemId,
                item.ReceiptId, // The ReceiptId references the warehouse via the receipt
                lcv.PostingDate,
                quantityChange: 0, // Zero qty — pure valuation adjustment
                incomingRate: item.ApplicableCharges, // The additional cost amount
                voucherType: "LandedCostVoucher",
                voucherId: lcv.Id,
                tenantId: lcv.TenantId);

            // Update Bin stock value (no qty change, only value change)
            await _binService.ApplyStockMovementAsync(
                item.ItemId, item.ReceiptId,
                0, item.ApplicableCharges, lcv.TenantId);
        }

        // Update serial number valuation rates (per DO-NOT: serials must reflect true landed cost)
        foreach (var item in lcv.Items.Where(i => i.ApplicableCharges > 0))
        {
            var serialQuery = await _serialNoRepository.GetQueryableAsync();
            var serials = serialQuery
                .Where(s => s.ItemId == item.ItemId && s.WarehouseId == item.ReceiptId)
                .ToList();

            if (serials.Count > 0)
            {
                // Distribute landed cost equally across serial numbers for this item
                var perSerialCharge = item.ApplicableCharges / serials.Count;
                foreach (var serial in serials)
                {
                    serial.PurchaseRate += perSerialCharge;
                    await _serialNoRepository.UpdateAsync(serial);
                }
            }
        }

        await _repository.UpdateAsync(lcv);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LandedCostVoucher", lcv.Id, "Submitted",
            lcv.CompanyId, lcv.VoucherNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: lcv.TenantId));

        return MapToDto(lcv);
    }

    [Authorize(MyERPPermissions.LandedCostVouchers.Cancel)]
    public async Task<LandedCostVoucherDto> CancelAsync(Guid id)
    {
        var lcv = (await _repository.WithDetailsAsync()).First(l => l.Id == id);
        lcv.Cancel();

        // Reverse the valuation adjustments
        foreach (var item in lcv.Items)
        {
            if (item.ApplicableCharges <= 0) continue;

            await _valuationService.CreateLedgerEntryAsync(
                lcv.CompanyId, item.ItemId, item.ReceiptId,
                lcv.PostingDate,
                quantityChange: 0,
                incomingRate: -item.ApplicableCharges, // Negative = reversal
                voucherType: "LandedCostVoucher",
                voucherId: lcv.Id,
                tenantId: lcv.TenantId);

            await _binService.ApplyStockMovementAsync(
                item.ItemId, item.ReceiptId,
                0, -item.ApplicableCharges, lcv.TenantId);
        }

        await _repository.UpdateAsync(lcv);

        var activityRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo2.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LandedCostVoucher", lcv.Id, "Cancelled",
            lcv.CompanyId, lcv.VoucherNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: lcv.TenantId));

        return MapToDto(lcv);
    }

    private static LandedCostVoucherDto MapToDto(LandedCostVoucher l) => new()
    {
        Id = l.Id,
        CompanyId = l.CompanyId,
        VoucherNumber = l.VoucherNumber,
        PostingDate = l.PostingDate,
        DistributionMethod = l.DistributionMethod,
        Status = l.Status,
        TotalCharges = l.TotalCharges,
        TotalDistributedAmount = l.TotalDistributedAmount,
        Notes = l.Notes,
        Items = l.Items.Select(i => new LandedCostItemDto
        {
            Id = i.Id, ReceiptId = i.ReceiptId, ReceiptType = i.ReceiptType,
            ItemId = i.ItemId, Description = i.Description,
            Quantity = i.Quantity, Amount = i.Amount, ApplicableCharges = i.ApplicableCharges,
        }).ToArray(),
        Charges = l.Charges.Select(c => new LandedCostChargeDto
        {
            Id = c.Id, Description = c.Description,
            ExpenseAccountId = c.ExpenseAccountId, Amount = c.Amount,
        }).ToArray(),
        CreationTime = l.CreationTime,
    };
}
