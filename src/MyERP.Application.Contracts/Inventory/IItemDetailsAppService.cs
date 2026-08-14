using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IItemDetailsAppService : IApplicationService
{
    Task<ItemDetailsDto> GetItemDetailsAsync(GetItemDetailsInput input);
}
