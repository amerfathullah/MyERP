using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IQuotationTrendsAppService : IApplicationService
{
    Task<QuotationTrendsReportDto> GetReportAsync(QuotationTrendsRequestDto input);
}
