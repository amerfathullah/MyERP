using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Finance Book management — enables multi-book depreciation and GL reporting.
/// Per ERPNext: each company can have multiple finance books for tax vs management reporting.
/// Only ONE book can be default per company.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class FinanceBookAppService : ApplicationService
{
    private readonly IRepository<FinanceBook, Guid> _repository;

    public FinanceBookAppService(IRepository<FinanceBook, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<FinanceBookDto> GetAsync(Guid id)
    {
        var book = await _repository.GetAsync(id);
        return MapToDto(book);
    }

    public async Task<PagedResultDto<FinanceBookDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var count = query.Count();
        var list = query.OrderBy(x => x.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<FinanceBookDto>(count,
            list.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<FinanceBookDto> CreateAsync(CreateFinanceBookDto input)
    {
        // Validate only ONE default per company
        if (input.IsDefault)
        {
            var query = await _repository.GetQueryableAsync();
            var existingDefault = query.FirstOrDefault(
                x => x.CompanyId == input.CompanyId && x.IsDefault);
            if (existingDefault != null)
            {
                // Demote existing default (per ERPNext: no auto-demotion → must manually unset)
                throw new BusinessException("MyERP:02029")
                    .WithData("existingBook", existingDefault.Name);
            }
        }

        var book = new FinanceBook(GuidGenerator.Create(), input.CompanyId, input.Name, CurrentTenant.Id)
        {
            IsDefault = input.IsDefault,
            Description = input.Description
        };

        await _repository.InsertAsync(book, autoSave: true);
        return MapToDto(book);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<FinanceBookDto> SetDefaultAsync(Guid id)
    {
        var book = await _repository.GetAsync(id);

        // Unset current default for this company
        var query = await _repository.GetQueryableAsync();
        var currentDefault = query.FirstOrDefault(
            x => x.CompanyId == book.CompanyId && x.IsDefault && x.Id != id);
        if (currentDefault != null)
        {
            currentDefault.IsDefault = false;
            await _repository.UpdateAsync(currentDefault);
        }

        book.IsDefault = true;
        await _repository.UpdateAsync(book, autoSave: true);
        return MapToDto(book);
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static FinanceBookDto MapToDto(FinanceBook book) => new()
    {
        Id = book.Id,
        CompanyId = book.CompanyId,
        Name = book.Name,
        IsDefault = book.IsDefault,
        Description = book.Description
    };
}

public class FinanceBookDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
}

public class CreateFinanceBookDto
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsDefault { get; set; }
    public string? Description { get; set; }
}
