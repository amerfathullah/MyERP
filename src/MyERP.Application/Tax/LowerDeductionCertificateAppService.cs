using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Tax.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

/// <summary>
/// Manages Lower Deduction Certificates — supplier-held certificates entitling a reduced
/// withholding tax rate, up to a limit, per Tax Withholding Category. Consumed by
/// TaxWithholdingService.GetLdcDetailsAsync when calculating withholding on Purchase Invoices.
/// </summary>
[Authorize(MyERPPermissions.TaxCategories.Default)]
public class LowerDeductionCertificateAppService : ApplicationService, ILowerDeductionCertificateAppService
{
    private readonly IRepository<LowerDeductionCertificate, Guid> _repository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<TaxWithholdingCategory, Guid> _categoryRepository;

    public LowerDeductionCertificateAppService(
        IRepository<LowerDeductionCertificate, Guid> repository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<TaxWithholdingCategory, Guid> categoryRepository)
    {
        _repository = repository;
        _supplierRepository = supplierRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<LowerDeductionCertificateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return await ToDtoAsync(entity);
    }

    public async Task<PagedResultDto<LowerDeductionCertificateDto>> GetListAsync(GetLowerDeductionCertificateListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);
        if (input.SupplierId.HasValue)
            query = query.Where(x => x.SupplierId == input.SupplierId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(x => x.ValidFrom)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        var dtos = await Task.WhenAll(items.Select(ToDtoAsync));
        return new PagedResultDto<LowerDeductionCertificateDto>(totalCount, dtos.ToList());
    }

    public async Task<LowerDeductionCertificateDto> CreateAsync(CreateUpdateLowerDeductionCertificateDto input)
    {
        var entity = new LowerDeductionCertificate(
            GuidGenerator.Create(), input.CompanyId, input.SupplierId, input.TaxWithholdingCategoryId,
            input.CertificateNumber, input.Rate, input.CertificateLimit,
            input.ValidFrom, input.ValidUpto, CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return await ToDtoAsync(entity);
    }

    public async Task<LowerDeductionCertificateDto> UpdateAsync(Guid id, CreateUpdateLowerDeductionCertificateDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetCertificateNumber(input.CertificateNumber);
        entity.SetValidity(input.ValidFrom, input.ValidUpto);
        entity.SetTerms(input.Rate, input.CertificateLimit);

        await _repository.UpdateAsync(entity);
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task<LowerDeductionCertificateDto> ToDtoAsync(LowerDeductionCertificate entity)
    {
        var dto = ObjectMapper.Map<LowerDeductionCertificate, LowerDeductionCertificateDto>(entity);

        var supplier = await _supplierRepository.FindAsync(entity.SupplierId);
        dto.SupplierName = supplier?.Name;

        var category = await _categoryRepository.FindAsync(entity.TaxWithholdingCategoryId);
        dto.TaxWithholdingCategoryName = category?.CategoryName;

        return dto;
    }
}
