using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyERP.ImportExport.DTOs;
using MyERP.ImportExport.Entities;
using MyERP.Inventory;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.ImportExport;

[Authorize(MyERPPermissions.ImportExport.Default)]
public class ImportExportAppService : ApplicationService, IImportExportAppService
{
    private readonly IRepository<ImportJob, Guid> _importJobRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public ImportExportAppService(
        IRepository<ImportJob, Guid> importJobRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _importJobRepository = importJobRepository;
        _customerRepository = customerRepository;
        _itemRepository = itemRepository;
    }

    [Authorize(MyERPPermissions.ImportExport.Import)]
    public async Task<ImportJobDto> StartImportAsync(StartImportDto input)
    {
        var job = new ImportJob(GuidGenerator.Create(), input.FileName, input.EntityType, CurrentTenant.Id)
        {
            CompanyId = input.CompanyId
        };

        await _importJobRepository.InsertAsync(job);

        // Process import synchronously for now (small files)
        // For large files, use ABP BackgroundJobs
        try
        {
            job.MarkProcessing();
            var (success, failure, errors) = await ProcessImportAsync(input.EntityType, input.FileContent);
            job.TotalRows = success + failure;
            job.MarkCompleted(success, failure, errors);
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
        }

        await _importJobRepository.UpdateAsync(job);
        return ObjectMapper.Map<ImportJob, ImportJobDto>(job);
    }

    [Authorize(MyERPPermissions.ImportExport.Import)]
    public async Task<ImportJobDto> GetImportStatusAsync(Guid jobId)
    {
        var job = await _importJobRepository.GetAsync(jobId);
        return ObjectMapper.Map<ImportJob, ImportJobDto>(job);
    }

    [Authorize(MyERPPermissions.ImportExport.Import)]
    public async Task<PagedResultDto<ImportJobDto>> GetImportHistoryAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _importJobRepository.GetCountAsync();
        var jobs = await _importJobRepository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "CreationTime DESC");

        return new PagedResultDto<ImportJobDto>(
            totalCount,
            jobs.Select(ObjectMapper.Map<ImportJob, ImportJobDto>).ToList());
    }

    [Authorize(MyERPPermissions.ImportExport.Export)]
    public async Task<ExportResultDto> ExportAsync(ExportRequestDto input)
    {
        var csvContent = input.EntityType switch
        {
            "Customer" => await ExportCustomersAsync(),
            "Item" => await ExportItemsAsync(),
            _ => throw new Volo.Abp.BusinessException("MyERP:05001")
                .WithData("entityType", input.EntityType)
        };

        var bytes = Encoding.UTF8.GetBytes(csvContent);
        return new ExportResultDto
        {
            FileName = $"{input.EntityType}_{DateTime.UtcNow:yyyyMMdd}.csv",
            ContentType = "text/csv",
            FileContent = Convert.ToBase64String(bytes)
        };
    }

    private async Task<(int success, int failure, string? errors)> ProcessImportAsync(string entityType, string base64Content)
    {
        var bytes = Convert.FromBase64String(base64Content);
        var content = Encoding.UTF8.GetString(bytes);
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2)
            return (0, 0, null);

        var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
        var success = 0;
        var failure = 0;
        var errors = new List<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var values = ParseCsvLine(lines[i]);
                await ImportRowAsync(entityType, headers, values);
                success++;
            }
            catch (Exception ex)
            {
                failure++;
                errors.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        return (success, failure, errors.Any() ? string.Join("; ", errors) : null);
    }

    private async Task ImportRowAsync(string entityType, string[] headers, string[] values)
    {
        switch (entityType)
        {
            case "Customer":
                await ImportCustomerAsync(headers, values);
                break;
            case "Item":
                await ImportItemAsync(headers, values);
                break;
            default:
                throw new NotSupportedException($"Import not supported for entity type: {entityType}");
        }
    }

    private async Task ImportCustomerAsync(string[] headers, string[] values)
    {
        var name = GetValue(headers, values, "Name") ?? GetValue(headers, values, "CustomerName")
            ?? throw new InvalidOperationException("Name column is required");
        var companyIdStr = GetValue(headers, values, "CompanyId");
        var companyId = companyIdStr != null ? Guid.Parse(companyIdStr) : Guid.Empty;
        var customer = new Customer(GuidGenerator.Create(), companyId, name, CurrentTenant.Id)
        {
            CustomerCode = GetValue(headers, values, "CustomerCode") ?? GetValue(headers, values, "Code"),
            Tin = GetValue(headers, values, "TIN") ?? GetValue(headers, values, "TaxId"),
            Email = GetValue(headers, values, "Email"),
            Phone = GetValue(headers, values, "Phone"),
        };
        await _customerRepository.InsertAsync(customer);
    }

    private async Task ImportItemAsync(string[] headers, string[] values)
    {
        var name = GetValue(headers, values, "ItemName") ?? GetValue(headers, values, "Name")
            ?? throw new InvalidOperationException("ItemName column is required");
        var code = GetValue(headers, values, "ItemCode") ?? GetValue(headers, values, "Code") ?? name;
        var companyIdStr = GetValue(headers, values, "CompanyId");
        var companyId = companyIdStr != null ? Guid.Parse(companyIdStr) : Guid.Empty;
        var item = new Item(GuidGenerator.Create(), companyId, code, name, ItemType.Goods, CurrentTenant.Id)
        {
            Uom = GetValue(headers, values, "UOM") ?? "Unit",
        };
        var priceStr = GetValue(headers, values, "StandardSellingPrice") ?? GetValue(headers, values, "Price");
        if (decimal.TryParse(priceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            item.StandardSellingPrice = price;
        await _itemRepository.InsertAsync(item);
    }

    private async Task<string> ExportCustomersAsync()
    {
        var customers = await _customerRepository.GetListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Name,CustomerCode,TIN,Email,Phone");
        foreach (var c in customers)
        {
            sb.AppendLine($"\"{Escape(c.Name)}\",\"{Escape(c.CustomerCode)}\",\"{Escape(c.Tin)}\",\"{Escape(c.Email)}\",\"{Escape(c.Phone)}\"");
        }
        return sb.ToString();
    }

    private async Task<string> ExportItemsAsync()
    {
        var items = await _itemRepository.GetListAsync();
        var sb = new StringBuilder();
        sb.AppendLine("ItemName,ItemCode,UOM,StandardSellingPrice");
        foreach (var item in items)
        {
            sb.AppendLine($"\"{Escape(item.ItemName)}\",\"{Escape(item.ItemCode)}\",\"{Escape(item.Uom)}\",{item.StandardSellingPrice}");
        }
        return sb.ToString();
    }

    private static string? GetValue(string[] headers, string[] values, string columnName)
    {
        var idx = Array.FindIndex(headers, h => h.Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx >= values.Length) return null;
        var val = values[idx].Trim().Trim('"');
        return string.IsNullOrEmpty(val) ? null : val;
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parsing — handles quoted fields
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string Escape(string? value) => value?.Replace("\"", "\"\"") ?? "";
}
