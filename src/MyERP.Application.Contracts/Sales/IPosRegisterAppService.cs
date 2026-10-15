using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IPosRegisterAppService : IApplicationService
{
    Task<PosRegisterDto> GetReportAsync(PosRegisterRequestDto input);
}
