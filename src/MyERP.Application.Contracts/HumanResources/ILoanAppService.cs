using System;
using System.Threading.Tasks;
using MyERP.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.HumanResources;

public interface ILoanAppService : IApplicationService
{
    Task<PagedResultDto<LoanDto>> GetListAsync(CompanyFilteredPagedRequestDto input);
    Task<LoanDto> GetAsync(Guid id);
    Task<LoanDto> CreateAsync(CreateLoanDto input);
    Task<LoanDto> SanctionAsync(Guid id);
    Task<LoanDto> DisburseAsync(Guid id, DisburseLoanDto input);
    Task<LoanDto> RecordRepaymentAsync(Guid id, RecordRepaymentDto input);
    Task<LoanDto> CancelAsync(Guid id);
}
