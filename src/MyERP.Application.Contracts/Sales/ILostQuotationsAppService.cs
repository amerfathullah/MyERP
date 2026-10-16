using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ILostQuotationsAppService : IApplicationService
{
    Task<LostQuotationsReportDto> GetReportAsync(GetLostQuotationsReportInput input);
}
