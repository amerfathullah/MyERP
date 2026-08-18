using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public class SubcontractingBomDto : EntityDto<Guid>
{
    public bool IsActive { get; set; }
    public Guid FinishedGoodId { get; set; }
    public string? FinishedGoodName { get; set; }
    public decimal FinishedGoodQty { get; set; }
    public Guid FinishedGoodBomId { get; set; }
    public string? FinishedGoodUom { get; set; }
    public Guid ServiceItemId { get; set; }
    public string? ServiceItemName { get; set; }
    public decimal ServiceItemQty { get; set; }
    public string? ServiceItemUom { get; set; }
    public decimal ConversionFactor { get; set; }
}

public class CreateUpdateSubcontractingBomDto
{
    public bool IsActive { get; set; } = true;
    public Guid FinishedGoodId { get; set; }
    public decimal FinishedGoodQty { get; set; } = 1;
    public Guid FinishedGoodBomId { get; set; }
    public Guid ServiceItemId { get; set; }
    public decimal ServiceItemQty { get; set; } = 1;
}

public interface ISubcontractingBomAppService : IApplicationService
{
    Task<PagedResultDto<SubcontractingBomDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<SubcontractingBomDto> GetAsync(Guid id);
    Task<SubcontractingBomDto> CreateAsync(CreateUpdateSubcontractingBomDto input);
    Task<SubcontractingBomDto> UpdateAsync(Guid id, CreateUpdateSubcontractingBomDto input);
    Task DeleteAsync(Guid id);
}
