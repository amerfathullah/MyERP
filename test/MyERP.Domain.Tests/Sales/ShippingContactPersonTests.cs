using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class ShippingContactPersonTests
{
    [Fact]
    public void SalesOrder_SupportsBillingAndShippingContactPerson()
    {
        var billingContactId = Guid.NewGuid();
        var shippingContactId = Guid.NewGuid();

        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow)
        {
            ContactPersonId = billingContactId,
            ShippingContactPersonId = shippingContactId
        };

        so.ContactPersonId.ShouldBe(billingContactId);
        so.ShippingContactPersonId.ShouldBe(shippingContactId);
    }

    [Fact]
    public void DeliveryNote_SupportsBillingAndShippingContactPerson()
    {
        var billingContactId = Guid.NewGuid();
        var shippingContactId = Guid.NewGuid();

        var dn = new DeliveryNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "DN-001", DateTime.UtcNow)
        {
            ContactPersonId = billingContactId,
            ShippingContactPersonId = shippingContactId
        };

        dn.ContactPersonId.ShouldBe(billingContactId);
        dn.ShippingContactPersonId.ShouldBe(shippingContactId);
    }

    [Fact]
    public void SalesInvoice_SupportsBillingAndShippingContactPerson()
    {
        var billingContactId = Guid.NewGuid();
        var shippingContactId = Guid.NewGuid();

        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SINV-001", DateTime.UtcNow)
        {
            ContactPersonId = billingContactId,
            ShippingContactPersonId = shippingContactId
        };

        si.ContactPersonId.ShouldBe(billingContactId);
        si.ShippingContactPersonId.ShouldBe(shippingContactId);
    }
}
