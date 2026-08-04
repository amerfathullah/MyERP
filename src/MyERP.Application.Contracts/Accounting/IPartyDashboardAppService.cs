using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class PartyDashboardDto
{
    public decimal YtdBilling { get; set; }
    public decimal TotalUnpaid { get; set; }
    public decimal LoyaltyPoints { get; set; }
    public List<CompanyReferenceDto> Companies { get; set; } = new();
}

public class CompanyReferenceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public interface IPartyDashboardAppService : IApplicationService
{
    Task<PartyDashboardDto> GetCustomerDashboardAsync(Guid customerId);
    Task<PartyDashboardDto> GetSupplierDashboardAsync(Guid supplierId);
}
