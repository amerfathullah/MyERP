using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IInactiveSalesItemsAppService : IApplicationService
{
    Task<InactiveSalesItemsReportDto> GetReportAsync(InactiveSalesItemsFilterDto input);
}
