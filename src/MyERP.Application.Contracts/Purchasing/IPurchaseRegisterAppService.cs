using System.Threading.Tasks;
using MyERP.Sales;
using Volo.Abp.Application.Services;

namespace MyERP.Purchasing;

public interface IPurchaseRegisterAppService : IApplicationService
{
    Task<RegisterReportDto<PurchaseRegisterLineDto>> GetReportAsync(RegisterFilterDto input);
}
