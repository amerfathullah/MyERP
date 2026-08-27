using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface ILandedCostVoucherAppService : IApplicationService
{
    Task<PagedResultDto<LandedCostVoucherDto>> GetListAsync(GetLandedCostVoucherListDto input);
    Task<LandedCostVoucherDto> GetAsync(Guid id);
    Task<LandedCostVoucherDto> CreateAsync(CreateLandedCostVoucherDto input);
    Task<LandedCostVoucherDto> SubmitAsync(Guid id);
    Task<LandedCostVoucherDto> CancelAsync(Guid id);
    Task<List<LandedCostItemDto>> GetReceiptItemsAsync(GetLandedCostReceiptItemsInput input);
    Task<LandedCostDistributionResultDto> CalculateDistributionAsync(CalculateLandedCostDistributionDto input);
}
