using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class SerialNoValidationServiceTests
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepo;
    private readonly SerialNoValidationService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _otherWarehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public SerialNoValidationServiceTests()
    {
        _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
        _service = new SerialNoValidationService(_serialNoRepo);
    }

    [Fact]
    public async Task ValidateForStockOutAsync_ValidActiveSerial_Succeeds()
    {
        var serial = new SerialNo(Guid.NewGuid(), _itemId, "SN-001", _companyId, _warehouseId);
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>())
            .Returns(Task.FromResult(new List<SerialNo> { serial }));

        var items = new List<SerialNoValidationItem>
        {
            new(_itemId, "SN-001", _warehouseId, "Item 1")
        };

        await _service.ValidateForStockOutAsync(items);
    }

    [Fact]
    public async Task ValidateForStockOutAsync_DuplicateSerialInTransaction_ThrowsException()
    {
        var items = new List<SerialNoValidationItem>
        {
            new(_itemId, "SN-001", _warehouseId, "Item 1"),
            new(_itemId, "SN-001", _warehouseId, "Item 1")
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.ValidateForStockOutAsync(items));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SerialNoDuplicate);
    }

    [Fact]
    public async Task ValidateForStockOutAsync_SerialNotFound_ThrowsException()
    {
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>())
            .Returns(Task.FromResult(new List<SerialNo>()));

        var items = new List<SerialNoValidationItem>
        {
            new(_itemId, "SN-NONEXISTENT", _warehouseId, "Item 1")
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.ValidateForStockOutAsync(items));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SerialNoNotFound);
    }

    [Fact]
    public async Task ValidateForStockOutAsync_SerialNotActive_ThrowsException()
    {
        var serial = new SerialNo(Guid.NewGuid(), _itemId, "SN-001", _companyId, _warehouseId)
        {
            Status = SerialNoStatus.Delivered
        };
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>())
            .Returns(Task.FromResult(new List<SerialNo> { serial }));

        var items = new List<SerialNoValidationItem>
        {
            new(_itemId, "SN-001", _warehouseId, "Item 1")
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.ValidateForStockOutAsync(items));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SerialNoNotActive);
    }

    [Fact]
    public async Task ValidateForStockOutAsync_SerialWarehouseMismatch_ThrowsException()
    {
        var serial = new SerialNo(Guid.NewGuid(), _itemId, "SN-001", _companyId, _otherWarehouseId);
        _serialNoRepo.GetListAsync(Arg.Any<Expression<Func<SerialNo, bool>>>())
            .Returns(Task.FromResult(new List<SerialNo> { serial }));

        var items = new List<SerialNoValidationItem>
        {
            new(_itemId, "SN-001", _warehouseId, "Item 1")
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.ValidateForStockOutAsync(items));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SerialNoWarehouseMismatch);
    }
}
