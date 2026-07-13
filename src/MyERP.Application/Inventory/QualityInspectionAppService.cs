using System;
using System.Linq;
using System.Threading.Tasks;
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

    public QualityInspectionAppService(IRepository<QualityInspection, Guid> repository)
    {
        _repository = repository;
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
            var f = input.Filter.ToLower();
            query = query.Where(q => (q.ItemName ?? "").ToLower().Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(q => q.InspectionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<QualityInspectionDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<QualityInspectionDto> GetAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        return MapToDto(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Create)]
    public async Task<QualityInspectionDto> CreateAsync(CreateQualityInspectionDto input)
    {
        var qi = new QualityInspection(GuidGenerator.Create(), input.CompanyId, input.ItemId,
            input.InspectionType, input.InspectionDate, CurrentTenant.Id)
        {
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
        return MapToDto(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Submit)]
    public async Task<QualityInspectionDto> SubmitAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        qi.Submit();
        await _repository.UpdateAsync(qi);
        return MapToDto(qi);
    }

    private static QualityInspectionDto MapToDto(QualityInspection q) => new()
    {
        Id = q.Id,
        CompanyId = q.CompanyId,
        ItemId = q.ItemId,
        ItemName = q.ItemName,
        InspectionType = q.InspectionType,
        ReferenceType = q.ReferenceType,
        ReferenceId = q.ReferenceId,
        BatchNo = q.BatchNo,
        SampleSize = q.SampleSize,
        InspectionDate = q.InspectionDate,
        Status = q.Status,
        DocStatus = q.DocStatus,
        Remarks = q.Remarks,
        ManualInspection = q.ManualInspection,
        Readings = q.Readings.Select(r => new QualityInspectionReadingDto
        {
            Id = r.Id,
            Specification = r.Specification,
            ExpectedValue = r.ExpectedValue,
            MinValue = r.MinValue,
            MaxValue = r.MaxValue,
            ReadingValue = r.ReadingValue,
            IsNumeric = r.IsNumeric,
            FormulaBased = r.FormulaBased,
            Status = r.Status,
        }).ToArray(),
        CreationTime = q.CreationTime,
    };
}
