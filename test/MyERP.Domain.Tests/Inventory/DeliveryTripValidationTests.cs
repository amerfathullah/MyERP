using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Delivery Trip driver requirement and Delivery Note submission validation.
/// Verifies rules migrated from erpnext/stock/doctype/delivery_trip (Gotcha #4155).
/// </summary>
public class DeliveryTripValidationTests
{
    private readonly IRepository<DeliveryTrip, Guid> _tripRepo = Substitute.For<IRepository<DeliveryTrip, Guid>>();
    private readonly DeliveryTripAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public DeliveryTripValidationTests()
    {
        var dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var emailSender = Substitute.For<IEmailSender>();
        _appService = new DeliveryTripAppService(_tripRepo, dnRepo, custRepo, emailSender);
    }

    [Fact]
    public async Task CreateAsync_EmptyDriver_ThrowsValidationException()
    {
        var input = new CreateUpdateDeliveryTripDto
        {
            CompanyId = _companyId,
            Driver = "   ", // Empty driver
            Vehicle = "VAN-01",
            DepartureTime = DateTime.UtcNow,
            DeliveryStops = new List<CreateUpdateDeliveryStopDto>
            {
                new() { Address = "Customer Location 1", Distance = 15m }
            }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.CreateAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("Driver is required", ex.Data["detail"]?.ToString());
    }
}
