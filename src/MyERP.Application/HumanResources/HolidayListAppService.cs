using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

public class HolidayListDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public string? WeeklyOff { get; set; }
    public bool IsDefault { get; set; }
    public HolidayDto[] Holidays { get; set; } = [];
    public DateTime CreationTime { get; set; }
}

public class HolidayDto
{
    public Guid Id { get; set; }
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;
    public bool IsWeeklyOff { get; set; }
}

public class CreateHolidayListDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public int Year { get; set; }
    public string? WeeklyOff { get; set; }
    public bool IsDefault { get; set; }
    public CreateHolidayDto[] Holidays { get; set; } = [];
}

public class CreateHolidayDto
{
    public DateTime HolidayDate { get; set; }
    public string Description { get; set; } = null!;
    public bool IsWeeklyOff { get; set; }
}

[Authorize(MyERPPermissions.Employees.Default)]
public class HolidayListAppService : ApplicationService
{
    private readonly IRepository<HolidayList, Guid> _repository;

    public HolidayListAppService(IRepository<HolidayList, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<HolidayListDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(h => h.Year).ThenBy(h => h.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<HolidayListDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<HolidayListDto> GetAsync(Guid id)
    {
        var hl = (await _repository.WithDetailsAsync()).First(h => h.Id == id);
        return MapToDto(hl);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<HolidayListDto> CreateAsync(CreateHolidayListDto input)
    {
        var hl = new HolidayList(GuidGenerator.Create(), input.CompanyId, input.Name, input.Year, CurrentTenant.Id)
        {
            WeeklyOff = input.WeeklyOff,
            IsDefault = input.IsDefault,
        };
        foreach (var h in input.Holidays)
            hl.AddHoliday(new Holiday(Guid.NewGuid(), hl.Id, h.HolidayDate, h.Description, h.IsWeeklyOff));
        await _repository.InsertAsync(hl);
        return MapToDto(hl);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    private static HolidayListDto MapToDto(HolidayList h) => new()
    {
        Id = h.Id, CompanyId = h.CompanyId, Name = h.Name, Year = h.Year,
        WeeklyOff = h.WeeklyOff, IsDefault = h.IsDefault, CreationTime = h.CreationTime,
        Holidays = h.Holidays.Select(x => new HolidayDto
        {
            Id = x.Id, HolidayDate = x.HolidayDate, Description = x.Description, IsWeeklyOff = x.IsWeeklyOff,
        }).ToArray(),
    };
}
