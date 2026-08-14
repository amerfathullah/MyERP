using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IItemSalesAppService : IApplicationService
{
    Task<ItemSalesReportDto> GetReportAsync(RegisterFilterDto input);
}
