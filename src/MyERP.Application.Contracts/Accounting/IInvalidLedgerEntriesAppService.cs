using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IInvalidLedgerEntriesAppService : IApplicationService
{
    Task<InvalidLedgerEntriesReportDto> GetReportAsync(InvalidLedgerEntriesRequestDto input);
}
