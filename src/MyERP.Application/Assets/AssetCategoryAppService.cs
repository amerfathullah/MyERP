using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.AssetCategories.Default)]
public class AssetCategoryAppService : ApplicationService, IAssetCategoryAppService
{
    private readonly IRepository<AssetCategory, Guid> _repository;
    private readonly AssetCategoryMapper _mapper;

    public AssetCategoryAppService(
        IRepository<AssetCategory, Guid> repository,
        AssetCategoryMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResultDto<AssetCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.WithDetailsAsync(c => c.Accounts);
        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(c => c.CategoryName)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AssetCategoryDto>(totalCount, items.Select(_mapper.Map).ToList());
    }

    public async Task<AssetCategoryDto> GetAsync(Guid id)
    {
        var query = await _repository.WithDetailsAsync(c => c.Accounts);
        var category = await AsyncExecuter.FirstOrDefaultAsync(query, c => c.Id == id);

        if (category == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        return _mapper.Map(category);
    }

    [Authorize(MyERPPermissions.AssetCategories.Create)]
    public async Task<AssetCategoryDto> CreateAsync(CreateUpdateAssetCategoryDto input)
    {
        var category = new AssetCategory(GuidGenerator.Create(), input.CategoryName, CurrentTenant.Id)
        {
            IsDepreciable = input.IsDepreciable,
            EnableCwipAccounting = input.EnableCwipAccounting,
            NonDepreciableCategory = input.NonDepreciableCategory,
            DefaultDepreciationMethod = input.DefaultDepreciationMethod,
            DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths,
            DefaultDepreciationRate = input.DefaultDepreciationRate,
            DefaultFrequencyMonths = input.DefaultFrequencyMonths,
            AssetAccountId = input.AssetAccountId,
            DepreciationAccountId = input.DepreciationAccountId,
            AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId,
        };

        if (input.Accounts != null)
        {
            foreach (var acc in input.Accounts)
            {
                category.AddAccount(
                    GuidGenerator.Create(),
                    acc.CompanyId,
                    acc.FixedAssetAccountId,
                    acc.AccumulatedDepreciationAccountId,
                    acc.DepreciationExpenseAccountId,
                    acc.CapitalWorkInProgressAccountId);
            }
        }

        await _repository.InsertAsync(category);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetCategory", category.Id,
            "Created", Guid.Empty,
            category.CategoryName, "Draft", "Active", CurrentUser.Id,
            $"Asset category '{category.CategoryName}' created", CurrentTenant.Id));

        return _mapper.Map(category);
    }

    [Authorize(MyERPPermissions.AssetCategories.Edit)]
    public async Task<AssetCategoryDto> UpdateAsync(Guid id, CreateUpdateAssetCategoryDto input)
    {
        var query = await _repository.WithDetailsAsync(c => c.Accounts);
        var category = await AsyncExecuter.FirstOrDefaultAsync(query, c => c.Id == id);

        if (category == null)
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound);

        category.CategoryName = input.CategoryName;
        category.IsDepreciable = input.IsDepreciable;
        category.EnableCwipAccounting = input.EnableCwipAccounting;
        category.NonDepreciableCategory = input.NonDepreciableCategory;
        category.DefaultDepreciationMethod = input.DefaultDepreciationMethod;
        category.DefaultUsefulLifeMonths = input.DefaultUsefulLifeMonths;
        category.DefaultDepreciationRate = input.DefaultDepreciationRate;
        category.DefaultFrequencyMonths = input.DefaultFrequencyMonths;
        category.AssetAccountId = input.AssetAccountId;
        category.DepreciationAccountId = input.DepreciationAccountId;
        category.AccumulatedDepreciationAccountId = input.AccumulatedDepreciationAccountId;

        category.Accounts.Clear();
        if (input.Accounts != null)
        {
            foreach (var acc in input.Accounts)
            {
                category.AddAccount(
                    acc.Id ?? GuidGenerator.Create(),
                    acc.CompanyId,
                    acc.FixedAssetAccountId,
                    acc.AccumulatedDepreciationAccountId,
                    acc.DepreciationExpenseAccountId,
                    acc.CapitalWorkInProgressAccountId);
            }
        }

        await _repository.UpdateAsync(category);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "AssetCategory", category.Id,
            "Updated", Guid.Empty,
            category.CategoryName, "Active", "Active", CurrentUser.Id,
            $"Asset category '{category.CategoryName}' updated", CurrentTenant.Id));

        return _mapper.Map(category);
    }

    [Authorize(MyERPPermissions.AssetCategories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var assetRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Asset, Guid>>();
        var query = await assetRepo.GetQueryableAsync();
        var hasActive = query.Any(a => a.AssetCategoryId == id && a.Status != AssetStatus.Cancelled);
        if (hasActive)
        {
            throw new BusinessException("MyERP:15002")
                .WithData("reason", "Active assets are linked to this category.");
        }
        await _repository.DeleteAsync(id);
    }

    public async Task<AssetCategoryAccountDto?> GetAccountForCompanyAsync(Guid categoryId, Guid companyId)
    {
        var query = await _repository.WithDetailsAsync(c => c.Accounts);
        var category = await AsyncExecuter.FirstOrDefaultAsync(query, c => c.Id == categoryId);

        var account = category?.GetAccountForCompany(companyId);
        if (account == null) return null;

        return new AssetCategoryAccountDto
        {
            Id = account.Id,
            AssetCategoryId = account.AssetCategoryId,
            CompanyId = account.CompanyId,
            FixedAssetAccountId = account.FixedAssetAccountId,
            AccumulatedDepreciationAccountId = account.AccumulatedDepreciationAccountId,
            DepreciationExpenseAccountId = account.DepreciationExpenseAccountId,
            CapitalWorkInProgressAccountId = account.CapitalWorkInProgressAccountId,
        };
    }
}
