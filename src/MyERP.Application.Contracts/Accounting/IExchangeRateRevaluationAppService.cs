using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IExchangeRateRevaluationAppService : IApplicationService
{
    Task<PagedResultDto<ExchangeRateRevaluationDto>> GetListAsync(Guid companyId, int maxResultCount = 20);
    Task<List<EligibleAccountDto>> GetEligibleAccountsAsync(Guid companyId, string companyCurrency, DateTime postingDate);
    Task<ExchangeRateRevaluationDto> CreateRevaluationAsync(CreateRevaluationDto input);
    Task<Guid> CreateReversalAsync(Guid revaluationJournalEntryId);
}
