using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetActivityAppService : ApplicationService, IAssetActivityAppService
{
    private readonly IRepository<AssetActivity, Guid> _repository;
    private readonly AssetActivityMapper _mapper;

    public AssetActivityAppService(
        IRepository<AssetActivity, Guid> repository,
        AssetActivityMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<AssetActivityDto>> GetListByAssetAsync(Guid assetId)
    {
        var query = await _repository.GetQueryableAsync();
        var list = await AsyncExecuter.ToListAsync(
            query.Where(a => a.AssetId == assetId)
                .OrderByDescending(a => a.TransactionDate));

        return list.Select(_mapper.Map).ToList();
    }

    public async Task<AssetActivityDto> CreateAsync(CreateAssetActivityDto input)
    {
        var activity = new AssetActivity(
            GuidGenerator.Create(),
            input.AssetId,
            input.ActivityType,
            input.Subject,
            input.TransactionDate,
            input.Details,
            input.ReferenceType,
            input.ReferenceId,
            CurrentTenant.Id);

        await _repository.InsertAsync(activity);
        return _mapper.Map(activity);
    }
}
