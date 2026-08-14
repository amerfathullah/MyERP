using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IGeneralLedgerAppService : IApplicationService
{
    Task<GeneralLedgerReportDto> GetReportAsync(GeneralLedgerFilterDto input);
    Task<VoucherLedgerDto> GetForVoucherAsync(string voucherType, Guid voucherId);
}
