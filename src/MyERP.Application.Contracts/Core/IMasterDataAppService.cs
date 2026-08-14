using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IMasterDataAppService : IApplicationService
{
    Task<List<ItemGroupLookupDto>> GetItemGroupsAsync();
    Task<List<ModeOfPaymentLookupDto>> GetModesOfPaymentAsync();
    Task<List<CostCenterLookupDto>> GetCostCentersAsync(Guid? companyId = null);
    Task<List<PaymentTermsLookupDto>> GetPaymentTermsAsync();
}
