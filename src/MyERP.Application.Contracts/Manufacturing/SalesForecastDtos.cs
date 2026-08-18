using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public class SalesForecastItemDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public decimal DemandQty { get; set; }
}

public class SalesForecastDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ForecastNumber { get; set; } = null!;
    public int Status { get; set; }
    public string ForecastStatus { get; set; } = null!;
    public DateTime PostingDate { get; set; }
    public DateTime FromDate { get; set; }
    public string Frequency { get; set; } = null!;
    public int DemandNumber { get; set; }
    public Guid ParentWarehouseId { get; set; }
    public List<Guid> SelectedItemIds { get; set; } = new();
    public List<SalesForecastItemDto> Items { get; set; } = new();
}

public class CreateSalesForecastDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public Guid ParentWarehouseId { get; set; }
    public SalesForecastFrequency Frequency { get; set; } = SalesForecastFrequency.Monthly;
    public int DemandNumber { get; set; } = 6;
    public List<Guid> SelectedItemIds { get; set; } = new();
}

public class UpdateSalesForecastDto
{
    public DateTime FromDate { get; set; }
    public Guid ParentWarehouseId { get; set; }
    public SalesForecastFrequency Frequency { get; set; }
    public int DemandNumber { get; set; }
    public List<Guid> SelectedItemIds { get; set; } = new();
}

public interface ISalesForecastAppService : IApplicationService
{
    Task<SalesForecastDto> GetAsync(Guid id);
    Task<PagedResultDto<SalesForecastDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<SalesForecastDto> CreateAsync(CreateSalesForecastDto input);
    Task<SalesForecastDto> UpdateAsync(Guid id, UpdateSalesForecastDto input);
    Task DeleteAsync(Guid id);
    Task<SalesForecastDto> GenerateDemandAsync(Guid id);
    Task<SalesForecastDto> SubmitAsync(Guid id);
    Task<SalesForecastDto> CancelAsync(Guid id);
    Task<Guid> CreateMpsAsync(Guid id);
}
