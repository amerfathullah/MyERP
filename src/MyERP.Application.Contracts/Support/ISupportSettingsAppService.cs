using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

public interface ISupportSettingsAppService : IApplicationService
{
    Task<SupportSettingsDto?> GetForCompanyAsync(Guid companyId);
    Task<SupportSettingsDto> SaveAsync(SaveSupportSettingsDto input);
}
