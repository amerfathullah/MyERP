using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ISerialNoWarrantyExpiryAppService : IApplicationService
{
    Task<SerialNoWarrantyExpiryReportDto> GetReportAsync(SerialNoWarrantyExpiryFilterDto input);
}
