using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IRepostAccountingLedgerAppService : IApplicationService
{
    Task<PagedResultDto<RepostAccountingLedgerDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<RepostAccountingLedgerDto> GetAsync(Guid id);
    Task<RepostAccountingLedgerDto> CreateAsync(CreateRepostAccountingLedgerDto input);

    /// <summary>Draft -&gt; Queued and enqueues the background job. Returns the updated document.</summary>
    Task<RepostAccountingLedgerDto> SubmitAsync(Guid id);

    Task<RepostAccountingLedgerDto> CancelAsync(Guid id);

    /// <summary>Allowed voucher types for the picker dropdown (see RepostAllowedVoucherTypes).</summary>
    Task<List<string>> GetAllowedVoucherTypesAsync();

    /// <summary>Resolves a Posted voucher by number for the picker — throws if not found, wrong
    /// status, or its type isn't allowed.</summary>
    Task<RepostableVoucherDto> ResolveVoucherAsync(string voucherType, string voucherNumber);
}
