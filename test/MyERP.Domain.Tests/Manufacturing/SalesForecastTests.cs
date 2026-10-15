using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for Sales Forecast business logic and validation guards.
/// Matches ERPNext PR #60546 / commit c1e7574ad7.
/// </summary>
public class SalesForecastTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _parentWarehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly DateTime _fromDate = new(2026, 10, 1);

    [Fact]
    public void GenerateDemand_Monthly_StartsOnFromDate()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00001", _fromDate, _parentWarehouseId)
        {
            Frequency = SalesForecastFrequency.Monthly,
            DemandNumber = 3
        };
        forecast.SetSelectedItems(new[] { _itemId });

        var itemDetails = new Dictionary<Guid, (string ItemName, string Uom)>
        {
            [_itemId] = ("Widget A", "Unit")
        };

        forecast.GenerateDemand(itemDetails);

        Assert.Equal(3, forecast.Items.Count);
        Assert.Equal(new DateTime(2026, 10, 1), forecast.Items[0].DeliveryDate);
        Assert.Equal(new DateTime(2026, 11, 1), forecast.Items[1].DeliveryDate);
        Assert.Equal(new DateTime(2026, 12, 1), forecast.Items[2].DeliveryDate);
        Assert.All(forecast.Items, item => Assert.Equal(1.0m, item.DemandQty));
    }

    [Fact]
    public void GenerateDemand_Weekly_StartsOnFromDate()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00002", _fromDate, _parentWarehouseId)
        {
            Frequency = SalesForecastFrequency.Weekly,
            DemandNumber = 3
        };
        forecast.SetSelectedItems(new[] { _itemId });

        var itemDetails = new Dictionary<Guid, (string ItemName, string Uom)>
        {
            [_itemId] = ("Widget B", "Unit")
        };

        forecast.GenerateDemand(itemDetails);

        Assert.Equal(3, forecast.Items.Count);
        Assert.Equal(new DateTime(2026, 10, 1), forecast.Items[0].DeliveryDate);
        Assert.Equal(new DateTime(2026, 10, 8), forecast.Items[1].DeliveryDate);
        Assert.Equal(new DateTime(2026, 10, 15), forecast.Items[2].DeliveryDate);
    }

    [Fact]
    public void GenerateDemand_ZeroOrNegativeDemandNumber_Throws()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00003", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 0
        };
        forecast.SetSelectedItems(new[] { _itemId });

        var itemDetails = new Dictionary<Guid, (string ItemName, string Uom)>
        {
            [_itemId] = ("Widget C", "Unit")
        };

        var ex = Assert.Throws<BusinessException>(() => forecast.GenerateDemand(itemDetails));
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastDemandNumberMustBeGreaterThanZero, ex.Code);
    }

    [Fact]
    public void Submit_DemandNumberZeroOrNegative_Throws()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00004", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 0
        };
        forecast.SetSelectedItems(new[] { _itemId });

        var ex = Assert.Throws<BusinessException>(() => forecast.Submit());
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastDemandNumberMustBeGreaterThanZero, ex.Code);
    }

    [Fact]
    public void Submit_NoItems_Throws()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00005", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 3
        };

        var ex = Assert.Throws<BusinessException>(() => forecast.Submit());
        Assert.Equal(MyERPDomainErrorCodes.DocumentMustHaveItems, ex.Code);
    }

    [Fact]
    public void Submit_DeliveryDateBeforeFromDate_Throws()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00006", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 2
        };
        var item = new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit",
            _fromDate.AddDays(-10), 5.0m);
        forecast.AddItem(item);

        var ex = Assert.Throws<BusinessException>(() => forecast.Submit());
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastDeliveryDateBeforeFromDate, ex.Code);
    }

    [Fact]
    public void Submit_DemandQtyZeroOrNegative_Throws()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00007", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 2
        };
        var item = new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit",
            _fromDate.AddDays(5), 0m);
        forecast.AddItem(item);

        var ex = Assert.Throws<BusinessException>(() => forecast.Submit());
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastDemandQtyRequired, ex.Code);
    }

    [Fact]
    public void Submit_ValidForecast_TransitionsToSubmittedAndPlanned()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00008", _fromDate, _parentWarehouseId)
        {
            DemandNumber = 2
        };
        var item = new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit",
            _fromDate, 10.0m);
        forecast.AddItem(item);

        forecast.Submit();

        Assert.Equal(DocumentStatus.Submitted, forecast.Status);
        Assert.Equal(SalesForecastStatus.Planned, forecast.ForecastStatus);
    }

    [Fact]
    public void Cancel_TransitionsToCancelled()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00009", _fromDate, _parentWarehouseId);
        forecast.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, forecast.Status);
        Assert.Equal(SalesForecastStatus.Cancelled, forecast.ForecastStatus);
    }

    [Fact]
    public void MarkMpsGenerated_And_ResetMpsStatus_Cycle()
    {
        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00010", _fromDate, _parentWarehouseId);
        forecast.AddItem(new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit", _fromDate, 1m));
        forecast.Submit();

        Assert.Equal(SalesForecastStatus.Planned, forecast.ForecastStatus);

        forecast.MarkMpsGenerated();
        Assert.Equal(SalesForecastStatus.MpsGenerated, forecast.ForecastStatus);

        var ex = Assert.Throws<BusinessException>(() => forecast.MarkMpsGenerated());
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastAlreadyUsedForMps, ex.Code);

        forecast.ResetMpsStatus();
        Assert.Equal(SalesForecastStatus.Planned, forecast.ForecastStatus);
    }

    [Fact]
    public async Task SalesForecastAppService_RejectNonStockItems()
    {
        var forecastRepo = Substitute.For<IRepository<SalesForecast, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();

        var appService = new SalesForecastAppService(forecastRepo, itemRepo, warehouseRepo, mpsRepo, numGen);

        var nonStockItemId = Guid.NewGuid();
        var serviceItem = new Item(nonStockItemId, _companyId, "SRV-01", "Installation Service", MyERP.Inventory.ItemType.Service)
        {
            MaintainStock = false
        };

        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { serviceItem }.AsQueryable()));

        var groupWarehouse = new Warehouse(_parentWarehouseId, _companyId, "Main Warehouses")
        {
            IsGroup = true
        };
        warehouseRepo.FindAsync(_parentWarehouseId).Returns(Task.FromResult<Warehouse?>(groupWarehouse));

        var input = new CreateSalesForecastDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ParentWarehouseId = _parentWarehouseId,
            DemandNumber = 3,
            SelectedItemIds = new List<Guid> { nonStockItemId }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.CreateAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastOnlyStockItemsAllowed, ex.Code);
    }

    [Fact]
    public async Task SalesForecastAppService_RejectNonGroupWarehouse()
    {
        var forecastRepo = Substitute.For<IRepository<SalesForecast, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();

        var appService = new SalesForecastAppService(forecastRepo, itemRepo, warehouseRepo, mpsRepo, numGen);

        var stockItem = new Item(_itemId, _companyId, "WIDGET-01", "Widget Item", MyERP.Inventory.ItemType.Goods)
        {
            MaintainStock = true
        };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { stockItem }.AsQueryable()));

        var leafWarehouse = new Warehouse(_parentWarehouseId, _companyId, "Rack A")
        {
            IsGroup = false
        };
        warehouseRepo.FindAsync(_parentWarehouseId).Returns(Task.FromResult<Warehouse?>(leafWarehouse));

        var input = new CreateSalesForecastDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ParentWarehouseId = _parentWarehouseId,
            DemandNumber = 3,
            SelectedItemIds = new List<Guid> { _itemId }
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.CreateAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastParentWarehouseInvalid, ex.Code);
    }

    [Fact]
    public async Task SalesForecastAppService_CreateMps_RefusesSecondMps()
    {
        var forecastRepo = Substitute.For<IRepository<SalesForecast, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();

        var appService = new SalesForecastAppService(forecastRepo, itemRepo, warehouseRepo, mpsRepo, numGen);

        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00011", _fromDate, _parentWarehouseId);
        forecast.AddItem(new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit", _fromDate, 1m));
        forecast.Submit();
        forecast.MarkMpsGenerated();

        forecastRepo.GetAsync(forecast.Id, includeDetails: true).Returns(Task.FromResult(forecast));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => appService.CreateMpsAsync(forecast.Id));
        Assert.Equal(MyERPDomainErrorCodes.SalesForecastAlreadyUsedForMps, ex.Code);
    }

    [Fact]
    public async Task MpsAppService_CreateAsync_SyncsSalesForecastStatus()
    {
        var mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var ppRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var leadTimeService = new MasterProductionScheduleService(itemRepo, bomRepo);

        var appService = new MasterProductionScheduleAppService(
            mpsRepo, soRepo, mrRepo, ppRepo, bomRepo, numGen, leadTimeService);

        var forecastRepo = Substitute.For<IRepository<SalesForecast, Guid>>();
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SalesForecast, Guid>>().Returns(forecastRepo);

        var objectMapper = Substitute.For<Volo.Abp.ObjectMapping.IObjectMapper>();
        objectMapper.Map<MasterProductionSchedule, MasterProductionScheduleDto>(Arg.Any<MasterProductionSchedule>())
            .Returns(callInfo => new MasterProductionScheduleDto { Id = Guid.NewGuid() });
        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(objectMapper);
        lazySp.LazyGetService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetRequiredService<Volo.Abp.ObjectMapping.IObjectMapper>().Returns(objectMapper);
        lazySp.LazyGetService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);
        lazySp.LazyGetRequiredService(typeof(Volo.Abp.ObjectMapping.IObjectMapper)).Returns(objectMapper);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        numGen.GenerateAsync(Arg.Any<string>(), Arg.Any<Guid>()).Returns(Task.FromResult("MPS-2026-00001"));

        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00012", _fromDate, _parentWarehouseId);
        forecast.AddItem(new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit", _fromDate, 1m));
        forecast.Submit();

        forecastRepo.GetAsync(forecast.Id).Returns(Task.FromResult(forecast));
        mpsRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MasterProductionSchedule>().AsQueryable()));

        var input = new CreateMasterProductionScheduleDto
        {
            CompanyId = _companyId,
            PostingDate = DateTime.UtcNow,
            FromDate = _fromDate,
            ParentWarehouseId = _parentWarehouseId,
            SalesForecastId = forecast.Id
        };

        var dto = await appService.CreateAsync(input);

        Assert.NotNull(dto);
        Assert.Equal(SalesForecastStatus.MpsGenerated, forecast.ForecastStatus);
        await forecastRepo.Received(1).UpdateAsync(forecast);
    }

    [Fact]
    public async Task MpsAppService_DeleteAsync_ResetsSalesForecastToPlanned()
    {
        var mpsRepo = Substitute.For<IRepository<MasterProductionSchedule, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        var ppRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var leadTimeService = new MasterProductionScheduleService(itemRepo, bomRepo);

        var appService = new MasterProductionScheduleAppService(
            mpsRepo, soRepo, mrRepo, ppRepo, bomRepo, numGen, leadTimeService);

        var forecastRepo = Substitute.For<IRepository<SalesForecast, Guid>>();
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<SalesForecast, Guid>>().Returns(forecastRepo);

        typeof(Volo.Abp.Application.Services.ApplicationService)
            .GetProperty("LazyServiceProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?
            .SetValue(appService, lazySp);

        var forecast = new SalesForecast(Guid.NewGuid(), _companyId, "SF-2026-00013", _fromDate, _parentWarehouseId);
        forecast.AddItem(new SalesForecastItem(Guid.NewGuid(), forecast.Id, _itemId, "Widget", "Unit", _fromDate, 1m));
        forecast.Submit();
        forecast.MarkMpsGenerated();

        var scheduleId = Guid.NewGuid();
        var schedule = new MasterProductionSchedule(scheduleId, _companyId, "MPS-2026-00001", DateTime.UtcNow, _fromDate)
        {
            SalesForecastId = forecast.Id
        };

        mpsRepo.FindAsync(scheduleId).Returns(Task.FromResult<MasterProductionSchedule?>(schedule));
        mpsRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MasterProductionSchedule>().AsQueryable()));
        forecastRepo.FindAsync(forecast.Id).Returns(Task.FromResult<SalesForecast?>(forecast));

        await appService.DeleteAsync(scheduleId);

        Assert.Equal(SalesForecastStatus.Planned, forecast.ForecastStatus);
        await forecastRepo.Received(1).UpdateAsync(forecast);
    }
}
