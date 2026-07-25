using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Dtos;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.QualityInspections.Default)]
public class QualityInspectionAppService : ApplicationService
{
    private readonly IRepository<QualityInspection, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public QualityInspectionAppService(
        IRepository<QualityInspection, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResultDto<QualityInspectionDto>> GetListAsync(GetQualityInspectionListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(q => q.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            query = query.Where(q => q.ItemId == input.ItemId.Value);
        if (input.Status.HasValue)
            query = query.Where(q => q.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(q => (q.ItemName ?? "").Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(q => q.InspectionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<QualityInspectionDto>(totalCount, items.Select(x => ObjectMapper.Map<QualityInspection, QualityInspectionDto>(x)).ToList());
    }

    public async Task<QualityInspectionDto> GetAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Create)]
    public async Task<QualityInspectionDto> CreateAsync(CreateQualityInspectionDto input)
    {
        var number = await _numberGenerator.GenerateAsync("QI", input.CompanyId);
        var qi = new QualityInspection(GuidGenerator.Create(), input.CompanyId, input.ItemId,
            input.InspectionType, input.InspectionDate, CurrentTenant.Id)
        {
            InspectionNumber = number,
            ItemName = input.ItemName,
            ReferenceType = input.ReferenceType,
            ReferenceId = input.ReferenceId,
            BatchNo = input.BatchNo,
            SampleSize = input.SampleSize,
            ManualInspection = input.ManualInspection,
        };

        foreach (var r in input.Readings)
            qi.AddReading(r.Specification, r.ExpectedValue, r.MinValue, r.MaxValue,
                r.ReadingValue, r.IsNumeric, r.FormulaBased, r.Formula);

        await _repository.InsertAsync(qi);
        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Submit)]
    public async Task<QualityInspectionDto> SubmitAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        qi.Submit();
        await _repository.UpdateAsync(qi);
        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }
}

