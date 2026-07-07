using MyERP.Localization;
using Volo.Abp.Application.Services;

namespace MyERP;

/* Inherit your application services from this class.
 */
public abstract class MyERPAppService : ApplicationService
{
    protected MyERPAppService()
    {
        LocalizationResource = typeof(MyERPResource);
    }
}
