using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.CRM;

public interface IAddressAndContactsAppService : IApplicationService
{
    Task<AddressAndContactsReportDto> GetReportAsync(GetAddressAndContactsInput input);
}
