using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IAvailableSerialNoReportAppService : IApplicationService
{
    Task<AvailableSerialNoReportDto> GetReportAsync(AvailableSerialNoFilterDto input);
}
