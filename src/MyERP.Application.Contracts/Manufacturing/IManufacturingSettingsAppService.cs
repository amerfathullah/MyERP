using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IManufacturingSettingsAppService : IApplicationService
{
    Task<ManufacturingSettingsDto?> GetForCompanyAsync(Guid companyId);
    Task<ManufacturingSettingsDto> SaveAsync(SaveManufacturingSettingsDto input);
}
