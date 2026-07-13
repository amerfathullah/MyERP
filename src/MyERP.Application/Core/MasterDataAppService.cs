using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Provides lookup data for forms: Item Groups, Modes of Payment, Cost Centers, Payment Terms.
/// </summary>
[Authorize]
public class MasterDataAppService : ApplicationService
{
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepository;
    private readonly IRepository<ModeOfPayment, Guid> _mopRepository;
    private readonly IRepository<CostCenter, Guid> _costCenterRepository;
    private readonly IRepository<PaymentTermsTemplate, Guid> _paymentTermsRepository;

    public MasterDataAppService(
        IRepository<ItemGroup, Guid> itemGroupRepository,
        IRepository<ModeOfPayment, Guid> mopRepository,
        IRepository<CostCenter, Guid> costCenterRepository,
        IRepository<PaymentTermsTemplate, Guid> paymentTermsRepository)
    {
        _itemGroupRepository = itemGroupRepository;
        _mopRepository = mopRepository;
        _costCenterRepository = costCenterRepository;
        _paymentTermsRepository = paymentTermsRepository;
    }

    public async Task<List<ItemGroupLookupDto>> GetItemGroupsAsync()
    {
        var items = await _itemGroupRepository.GetListAsync();
        return items.Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new ItemGroupLookupDto { Id = g.Id, Name = g.Name, IsGroup = g.IsGroup, ParentId = g.ParentId })
            .ToList();
    }

    public async Task<List<ModeOfPaymentLookupDto>> GetModesOfPaymentAsync()
    {
        var items = await _mopRepository.GetListAsync();
        return items.Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .Select(m => new ModeOfPaymentLookupDto { Id = m.Id, Name = m.Name, Type = m.Type })
            .ToList();
    }

    public async Task<List<CostCenterLookupDto>> GetCostCentersAsync(Guid? companyId = null)
    {
        var query = await _costCenterRepository.GetQueryableAsync();
        if (companyId.HasValue)
            query = query.Where(c => c.CompanyId == companyId.Value);
        return query.Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CostCenterLookupDto { Id = c.Id, Name = c.Name, IsGroup = c.IsGroup, ParentId = c.ParentId })
            .ToList();
    }

    public async Task<List<PaymentTermsLookupDto>> GetPaymentTermsAsync()
    {
        var items = await _paymentTermsRepository.GetListAsync();
        return items.Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new PaymentTermsLookupDto { Id = p.Id, Name = p.Name })
            .ToList();
    }
}

public class ItemGroupLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class ModeOfPaymentLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
}

public class CostCenterLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class PaymentTermsLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
