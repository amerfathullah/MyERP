using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ITerritoryWiseSalesAppService : IApplicationService
{
    Task<TerritoryWiseSalesReportDto> GetReportAsync(GetTerritoryWiseSalesInput input);
}
