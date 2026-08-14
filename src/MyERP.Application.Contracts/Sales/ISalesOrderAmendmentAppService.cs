using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesOrderAmendmentAppService : IApplicationService
{
    Task<Guid> AmendAsync(Guid cancelledOrderId);
}
