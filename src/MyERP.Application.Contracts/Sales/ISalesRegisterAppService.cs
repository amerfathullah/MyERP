using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesRegisterAppService : IApplicationService
{
    Task<RegisterReportDto<SalesRegisterLineDto>> GetReportAsync(RegisterFilterDto input);
}
