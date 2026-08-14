using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IPartyDetailsAppService : IApplicationService
{
    Task<PartyDetailsDto> GetCustomerDetailsAsync(GetPartyDetailsInput input);
    Task<PartyDetailsDto> GetSupplierDetailsAsync(GetPartyDetailsInput input);
}
