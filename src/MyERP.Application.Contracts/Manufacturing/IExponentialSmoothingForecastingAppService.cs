using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IExponentialSmoothingForecastingAppService : IApplicationService
{
    Task<ExponentialSmoothingForecastingDto> GetForecastAsync(ExponentialSmoothingForecastingFilterDto input);
}
