using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ICouponCodeAppService : IApplicationService
{
    Task<PagedResultDto<CouponCodeDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<CouponCodeDto> GetAsync(Guid id);
    Task<CouponCodeDto> CreateAsync(CreateCouponCodeDto input);
    Task<Guid> ValidateAndApplyAsync(string couponCode, Guid? customerId, DateTime transactionDate);
    Task ReverseUsageAsync(string couponCode);
    Task ToggleAsync(Guid id);
    Task DeleteAsync(Guid id);
}
