using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface ISalesAnalyticsAppService : IApplicationService
{
    Task<SalesAnalyticsReportDto> GetReportAsync(SalesAnalyticsRequestDto input);
    Task<List<CustomerCreditBalanceDto>> GetCustomerCreditBalanceReportAsync(CustomerCreditBalanceInputDto input);
    Task<List<CustomerWithoutSalesDto>> GetCustomersWithoutSalesTransactionsAsync(CustomersWithoutSalesInputDto input);
    Task<List<CustomerWiseItemPriceDto>> GetCustomerWiseItemPriceReportAsync(CustomerWiseItemPriceInputDto input);
    Task<CustomerAcquisitionAndLoyaltyReportDto> GetCustomerAcquisitionAndLoyaltyReportAsync(CustomerAcquisitionAndLoyaltyInputDto input);
    Task<List<AvailableStockForPackingItemDto>> GetAvailableStockForPackingItemsReportAsync(AvailableStockForPackingItemsInputDto input);
}

