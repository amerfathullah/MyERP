using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace MyERP.Inventory.Entities;

/// <summary>
/// The customer's own item code/reference for an Item, shown on Quotations/Sales Orders
/// for the customer's convenience. Maps to ERPNext's Item Customer Detail child table.
/// </summary>
public class ItemCustomerDetail : Entity<Guid>
{
    public Guid ItemId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>The item code that this customer uses at their end.</summary>
    public string RefCode { get; private set; } = null!;

    protected ItemCustomerDetail() { }

    public ItemCustomerDetail(Guid id, Guid itemId, Guid customerId, string refCode)
        : base(id)
    {
        ItemId = itemId;
        CustomerId = customerId;
        SetRefCode(refCode);
    }

    public void SetRefCode(string refCode)
    {
        RefCode = Check.NotNullOrWhiteSpace(refCode, nameof(refCode), 100);
    }
}
