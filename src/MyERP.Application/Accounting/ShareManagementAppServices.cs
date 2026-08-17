using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.ShareManagement.Default)]
public class ShareTypeAppService : ApplicationService, IShareTypeAppService
{
    private readonly IRepository<ShareType, Guid> _repository;

    public ShareTypeAppService(IRepository<ShareType, Guid> repository) => _repository = repository;

    public async Task<ListResultDto<ShareTypeDto>> GetListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var items = query.OrderBy(t => t.Title).ToList();
        return new ListResultDto<ShareTypeDto>(items.Select(ObjectMapper.Map<ShareType, ShareTypeDto>).ToList());
    }

    [Authorize(MyERPPermissions.ShareManagement.Create)]
    public async Task<ShareTypeDto> CreateAsync(CreateUpdateShareTypeDto input)
    {
        var type = new ShareType(GuidGenerator.Create(), input.Title, CurrentTenant.Id) { Description = input.Description };
        await _repository.InsertAsync(type);
        return ObjectMapper.Map<ShareType, ShareTypeDto>(type);
    }

    [Authorize(MyERPPermissions.ShareManagement.Edit)]
    public async Task<ShareTypeDto> UpdateAsync(Guid id, CreateUpdateShareTypeDto input)
    {
        var type = await _repository.GetAsync(id);
        type.Title = input.Title;
        type.Description = input.Description;
        await _repository.UpdateAsync(type);
        return ObjectMapper.Map<ShareType, ShareTypeDto>(type);
    }

    [Authorize(MyERPPermissions.ShareManagement.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}

[Authorize(MyERPPermissions.ShareManagement.Default)]
public class ShareholderAppService : ApplicationService, IShareholderAppService
{
    private readonly IRepository<Shareholder, Guid> _repository;

    public ShareholderAppService(IRepository<Shareholder, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<ShareholderDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.Title.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(s => s.Title).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ShareholderDto>(totalCount, items.Select(ObjectMapper.Map<Shareholder, ShareholderDto>).ToList());
    }

    public async Task<ShareholderDto> GetAsync(Guid id)
    {
        var shareholder = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return ObjectMapper.Map<Shareholder, ShareholderDto>(shareholder);
    }

    [Authorize(MyERPPermissions.ShareManagement.Create)]
    public async Task<ShareholderDto> CreateAsync(CreateUpdateShareholderDto input)
    {
        var shareholder = new Shareholder(GuidGenerator.Create(), input.CompanyId, input.Title, isCompany: false, tenantId: CurrentTenant.Id)
        {
            FolioNo = input.FolioNo,
        };
        await _repository.InsertAsync(shareholder);
        return ObjectMapper.Map<Shareholder, ShareholderDto>(shareholder);
    }

    [Authorize(MyERPPermissions.ShareManagement.Edit)]
    public async Task<ShareholderDto> UpdateAsync(Guid id, CreateUpdateShareholderDto input)
    {
        var shareholder = await _repository.GetAsync(id);
        shareholder.Title = input.Title;
        shareholder.FolioNo = input.FolioNo;
        await _repository.UpdateAsync(shareholder);
        return ObjectMapper.Map<Shareholder, ShareholderDto>(shareholder);
    }

    [Authorize(MyERPPermissions.ShareManagement.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}

[Authorize(MyERPPermissions.ShareManagement.Default)]
public class ShareTransferAppService : ApplicationService, IShareTransferAppService
{
    private readonly IRepository<ShareTransfer, Guid> _repository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly ShareTransferService _shareTransferService;

    public ShareTransferAppService(
        IRepository<ShareTransfer, Guid> repository,
        IRepository<Company, Guid> companyRepository,
        ShareTransferService shareTransferService)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _shareTransferService = shareTransferService;
    }

    public async Task<PagedResultDto<ShareTransferDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(t => t.CompanyId == input.CompanyId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(t => t.Date).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ShareTransferDto>(totalCount, items.Select(ObjectMapper.Map<ShareTransfer, ShareTransferDto>).ToList());
    }

    public async Task<ShareTransferDto> GetAsync(Guid id)
    {
        var transfer = await _repository.GetAsync(id);
        return ObjectMapper.Map<ShareTransfer, ShareTransferDto>(transfer);
    }

    [Authorize(MyERPPermissions.ShareManagement.Create)]
    public async Task<ShareTransferDto> CreateAsync(CreateUpdateShareTransferDto input)
    {
        var transfer = new ShareTransfer(GuidGenerator.Create(), input.CompanyId, input.TransferType, input.Date,
            input.ShareTypeId, input.FromNo, input.ToNo, input.Rate, CurrentTenant.Id);
        ApplyInput(transfer, input);
        transfer.Validate();
        await _repository.InsertAsync(transfer);
        return ObjectMapper.Map<ShareTransfer, ShareTransferDto>(transfer);
    }

    [Authorize(MyERPPermissions.ShareManagement.Edit)]
    public async Task<ShareTransferDto> UpdateAsync(Guid id, CreateUpdateShareTransferDto input)
    {
        var transfer = await _repository.GetAsync(id);
        ApplyInput(transfer, input);
        transfer.Validate();
        await _repository.UpdateAsync(transfer);
        return ObjectMapper.Map<ShareTransfer, ShareTransferDto>(transfer);
    }

    [Authorize(MyERPPermissions.ShareManagement.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    [Authorize(MyERPPermissions.ShareManagement.Submit)]
    public async Task<ShareTransferDto> SubmitAsync(Guid id)
    {
        var transfer = await _repository.GetAsync(id);
        var company = await _companyRepository.GetAsync(transfer.CompanyId);
        await _shareTransferService.SubmitAsync(transfer, company.Name);
        await _repository.UpdateAsync(transfer);
        return ObjectMapper.Map<ShareTransfer, ShareTransferDto>(transfer);
    }

    [Authorize(MyERPPermissions.ShareManagement.Cancel)]
    public async Task<ShareTransferDto> CancelAsync(Guid id)
    {
        var transfer = await _repository.GetAsync(id);
        var company = await _companyRepository.GetAsync(transfer.CompanyId);
        await _shareTransferService.CancelAsync(transfer, company.Name);
        await _repository.UpdateAsync(transfer);
        return ObjectMapper.Map<ShareTransfer, ShareTransferDto>(transfer);
    }

    private static void ApplyInput(ShareTransfer transfer, CreateUpdateShareTransferDto input)
    {
        transfer.CompanyId = input.CompanyId;
        transfer.TransferType = input.TransferType;
        transfer.Date = input.Date;
        transfer.FromShareholderId = input.FromShareholderId;
        transfer.ToShareholderId = input.ToShareholderId;
        transfer.ShareTypeId = input.ShareTypeId;
        transfer.FromNo = input.FromNo;
        transfer.ToNo = input.ToNo;
        transfer.Rate = input.Rate;
        transfer.Amount = input.Rate * (input.ToNo - input.FromNo + 1);
        transfer.EquityOrLiabilityAccountId = input.EquityOrLiabilityAccountId;
        transfer.AssetAccountId = input.AssetAccountId;
        transfer.Remarks = input.Remarks;
    }
}
