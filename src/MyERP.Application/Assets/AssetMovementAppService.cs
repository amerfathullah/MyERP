using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

public class AssetMovementDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string MovementType { get; set; } = null!;
    public DateTime MovementDate { get; set; }
    public string? SourceLocation { get; set; }
    public string? TargetLocation { get; set; }
    public string? Purpose { get; set; }
    public int Status { get; set; }
}

public class CreateAssetMovementDto
{
    public Guid CompanyId { get; set; }
    public Guid AssetId { get; set; }
    public string MovementType { get; set; } = "Transfer";
    public DateTime MovementDate { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? SourceEmployeeId { get; set; }
    public string? TargetLocation { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public string? Purpose { get; set; }
}

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetMovementAppService : ApplicationService
{
    private readonly IRepository<AssetMovement, Guid> _repository;
    public AssetMovementAppService(IRepository<AssetMovement, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<AssetMovementDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.MovementDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<AssetMovementDto>(totalCount, items.Select(x => ObjectMapper.Map<AssetMovement, AssetMovementDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<AssetMovementDto> CreateAsync(CreateAssetMovementDto input)
    {
        var am = new AssetMovement(GuidGenerator.Create(), input.CompanyId, input.AssetId,
            input.MovementType, input.MovementDate, CurrentTenant.Id)
        {
            SourceLocation = input.SourceLocation, SourceEmployeeId = input.SourceEmployeeId,
            TargetLocation = input.TargetLocation, TargetEmployeeId = input.TargetEmployeeId,
            Purpose = input.Purpose,
        };
        await _repository.InsertAsync(am);
        return ObjectMapper.Map<AssetMovement, AssetMovementDto>(am);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<AssetMovementDto> SubmitAsync(Guid id)
    {
        var am = await _repository.GetAsync(id);
        am.Submit();
        await _repository.UpdateAsync(am);
        return ObjectMapper.Map<AssetMovement, AssetMovementDto>(am);
    }
}
