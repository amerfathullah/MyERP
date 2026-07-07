using System;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

public class ItemAppService :
    CrudAppService<
        Item,
        ItemDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateUpdateItemDto>,
    IItemAppService
{
    public ItemAppService(IRepository<Item, Guid> repository)
        : base(repository)
    {
        GetPolicyName = MyERPPermissions.Items.Default;
        GetListPolicyName = MyERPPermissions.Items.Default;
        CreatePolicyName = MyERPPermissions.Items.Create;
        UpdatePolicyName = MyERPPermissions.Items.Edit;
        DeletePolicyName = MyERPPermissions.Items.Delete;
    }
}
