using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ILoyaltyProgramAppService : IApplicationService
{
    Task<LoyaltyProgramDto> GetAsync(Guid id);
    Task<PagedResultDto<LoyaltyProgramDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<LoyaltyProgramDto> CreateAsync(CreateLoyaltyProgramDto input);
    Task<LoyaltyProgramDto> UpdateAsync(Guid id, UpdateLoyaltyProgramDto input);
    Task DeleteAsync(Guid id);
    Task<LoyaltyBalanceDto> GetCustomerBalanceAsync(Guid customerId, Guid programId);
    Task<List<LoyaltyPointEntryDto>> GetPointHistoryAsync(Guid customerId, Guid programId);
    Task<decimal> RedeemPointsAsync(Guid customerId, Guid programId, int pointsToRedeem, Guid companyId);
}
