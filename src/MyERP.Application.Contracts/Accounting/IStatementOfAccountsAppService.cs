using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IStatementOfAccountsAppService : IApplicationService
{
    Task<StatementOfAccountsDto> GetCustomerStatementAsync(Guid customerId, Guid companyId, DateTime fromDate, DateTime toDate);
    Task<SupplierStatementDto> GetSupplierStatementAsync(Guid supplierId, Guid companyId, DateTime fromDate, DateTime toDate);
}
