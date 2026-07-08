using System;
using System.Threading.Tasks;
using MyERP.ImportExport.DTOs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.ImportExport;

public interface IImportExportAppService : IApplicationService
{
    Task<ImportJobDto> StartImportAsync(StartImportDto input);
    Task<ImportJobDto> GetImportStatusAsync(Guid jobId);
    Task<PagedResultDto<ImportJobDto>> GetImportHistoryAsync(PagedAndSortedResultRequestDto input);
    Task<ExportResultDto> ExportAsync(ExportRequestDto input);
}
