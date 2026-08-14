using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IBankStatementImportAppService : IApplicationService
{
    Task<BankStatementImportResult> ImportFromCsvAsync(BankStatementImportInput input);
    Task<BankStatementImportResult> ImportFromMt940Async(Mt940ImportInput input);
}
