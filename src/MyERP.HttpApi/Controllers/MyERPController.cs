using MyERP.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace MyERP.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class MyERPController : AbpControllerBase
{
    protected MyERPController()
    {
        LocalizationResource = typeof(MyERPResource);
    }
}
