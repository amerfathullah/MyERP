using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICashierClosingAppService : IApplicationService
{
    Task<PagedResultDto<CashierClosingDto>> GetListAsync(CashierClosingGetListInput input);
    Task<CashierClosingDto> GetAsync(Guid id);
    Task<CashierClosingDto> CreateAsync(CreateCashierClosingDto input);
    Task<CashierClosingDto> UpdateAsync(Guid id, UpdateCashierClosingDto input);
    Task DeleteAsync(Guid id);
    Task<CashierClosingDto> SubmitAsync(Guid id);
    Task<CalculateCashierClosingTotalsResponseDto> CalculateShiftTotalsAsync(CalculateCashierClosingTotalsRequestDto input);
}
