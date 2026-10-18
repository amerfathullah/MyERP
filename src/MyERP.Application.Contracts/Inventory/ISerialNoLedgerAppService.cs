using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialNoLedgerAppService : IApplicationService
{
    Task<SerialNoLedgerReportDto> GetReportAsync(SerialNoLedgerFilterDto input);
}
