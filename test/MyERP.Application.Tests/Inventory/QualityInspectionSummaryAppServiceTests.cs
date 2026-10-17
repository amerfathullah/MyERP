using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace MyERP.Inventory;

public class QualityInspectionSummaryAppServiceTests
{
    private readonly IRepository<QualityInspection, Guid> _qiRepo = Substitute.For<IRepository<QualityInspection, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<IdentityUser, Guid> _userRepo = Substitute.For<IRepository<IdentityUser, Guid>>();

    private readonly QualityInspectionSummaryAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public QualityInspectionSummaryAppServiceTests()
    {
        _appService = new QualityInspectionSummaryAppService(
            _qiRepo,
            _itemRepo,
            _userRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsInspections_WithItemAndInspectorResolved()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "RAW-001", "Steel Plate 10mm", ItemType.Goods);

        var userId = Guid.NewGuid();
        var user = new IdentityUser(userId, "john.doe", "john@test.com") { Name = "John Doe" };

        var qi1 = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, new DateTime(2026, 10, 15))
        {
            InspectionNumber = "QI-2026-0001",
            SampleSize = 5m,
            ReferenceType = "Purchase Receipt",
            ReferenceId = Guid.NewGuid(),
            CreatorId = userId,
            Remarks = "All parameters within limits"
        };
        qi1.SetStatus(InspectionStatus.Accepted);

        var qi2 = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, new DateTime(2026, 10, 16))
        {
            InspectionNumber = "QI-2026-0002",
            SampleSize = 10m,
            ReferenceType = "Purchase Receipt",
            ReferenceId = Guid.NewGuid(),
            CreatorId = userId,
            Remarks = "Surface crack detected"
        };
        qi2.SetStatus(InspectionStatus.Rejected);

        _qiRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<QualityInspection> { qi1, qi2 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        _userRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<IdentityUser> { user }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new QualityInspectionSummaryRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalInspections.ShouldBe(2);
        result.AcceptedCount.ShouldBe(1);
        result.RejectedCount.ShouldBe(1);
        result.DraftCount.ShouldBe(0);
        result.AcceptanceRate.ShouldBe(50.0m);
        result.Rows.Count.ShouldBe(2);

        var row1 = result.Rows.First(r => r.InspectionNumber == "QI-2026-0001");
        row1.Status.ShouldBe(InspectionStatus.Accepted);
        row1.StatusName.ShouldBe("Accepted");
        row1.ItemCode.ShouldBe("RAW-001");
        row1.ItemName.ShouldBe("Steel Plate 10mm");
        row1.SampleSize.ShouldBe(5m);
        row1.InspectionType.ShouldBe(InspectionType.Incoming);
        row1.InspectionTypeName.ShouldBe("Incoming");
        row1.InspectedByName.ShouldBe("John Doe (john.doe)");
        row1.Remarks.ShouldBe("All parameters within limits");

        var row2 = result.Rows.First(r => r.InspectionNumber == "QI-2026-0002");
        row2.Status.ShouldBe(InspectionStatus.Rejected);
        row2.StatusName.ShouldBe("Rejected");
    }

    [Fact]
    public async Task GetReportAsync_FiltersByDateRange()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var qiInside = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, new DateTime(2026, 10, 10))
        {
            InspectionNumber = "QI-IN"
        };
        qiInside.SetStatus(InspectionStatus.Accepted);

        var qiOutside = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, new DateTime(2026, 9, 1))
        {
            InspectionNumber = "QI-OUT"
        };
        qiOutside.SetStatus(InspectionStatus.Accepted);

        _qiRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<QualityInspection> { qiInside, qiOutside }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));

        _userRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<IdentityUser>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new QualityInspectionSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 10, 1),
            ToDate = new DateTime(2026, 10, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].InspectionNumber.ShouldBe("QI-IN");
    }

    [Fact]
    public async Task GetReportAsync_FiltersByStatus()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var accepted = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, DateTime.UtcNow)
        {
            InspectionNumber = "QI-ACC"
        };
        accepted.SetStatus(InspectionStatus.Accepted);

        var rejected = new QualityInspection(Guid.NewGuid(), _companyId, itemId, InspectionType.Incoming, DateTime.UtcNow)
        {
            InspectionNumber = "QI-REJ"
        };
        rejected.SetStatus(InspectionStatus.Rejected);

        _qiRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<QualityInspection> { accepted, rejected }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));

        _userRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<IdentityUser>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new QualityInspectionSummaryRequestDto
        {
            CompanyId = _companyId,
            Status = InspectionStatus.Accepted
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].InspectionNumber.ShouldBe("QI-ACC");
        result.TotalInspections.ShouldBe(1);
        result.AcceptedCount.ShouldBe(1);
        result.RejectedCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_FiltersByItemAndInspectionType()
    {
        // Arrange
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();

        var qi1 = new QualityInspection(Guid.NewGuid(), _companyId, item1, InspectionType.Incoming, DateTime.UtcNow)
        {
            InspectionNumber = "QI-ITEM1"
        };
        var qi2 = new QualityInspection(Guid.NewGuid(), _companyId, item2, InspectionType.Outgoing, DateTime.UtcNow)
        {
            InspectionNumber = "QI-ITEM2"
        };

        _qiRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<QualityInspection> { qi1, qi2 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item>().AsQueryable()));

        _userRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<IdentityUser>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new QualityInspectionSummaryRequestDto
        {
            CompanyId = _companyId,
            ItemId = item1,
            InspectionType = InspectionType.Incoming
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].InspectionNumber.ShouldBe("QI-ITEM1");
    }

    [Fact]
    public async Task GetReportAsync_ComputesStatusAndItemSummaries()
    {
        // Arrange
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "ITEM-1", "Widget A", ItemType.Goods);
        var item2 = new Item(item2Id, _companyId, "ITEM-2", "Widget B", ItemType.Goods);

        var qi1 = new QualityInspection(Guid.NewGuid(), _companyId, item1Id, InspectionType.Incoming, DateTime.UtcNow);
        qi1.SetStatus(InspectionStatus.Accepted);

        var qi2 = new QualityInspection(Guid.NewGuid(), _companyId, item1Id, InspectionType.Incoming, DateTime.UtcNow);
        qi2.SetStatus(InspectionStatus.Rejected);

        var qi3 = new QualityInspection(Guid.NewGuid(), _companyId, item2Id, InspectionType.Outgoing, DateTime.UtcNow);
        qi3.SetStatus(InspectionStatus.Accepted);

        _qiRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<QualityInspection> { qi1, qi2, qi3 }.AsQueryable()));

        _itemRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        _userRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<IdentityUser>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new QualityInspectionSummaryRequestDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.TotalInspections.ShouldBe(3);
        result.AcceptedCount.ShouldBe(2);
        result.RejectedCount.ShouldBe(1);
        result.AcceptanceRate.ShouldBe(66.67m);

        // Status summaries
        result.StatusSummaries.Count.ShouldBe(2);
        var accSummary = result.StatusSummaries.First(s => s.Status == "Accepted");
        accSummary.Count.ShouldBe(2);
        accSummary.Percentage.ShouldBe(66.7m);

        var rejSummary = result.StatusSummaries.First(s => s.Status == "Rejected");
        rejSummary.Count.ShouldBe(1);
        rejSummary.Percentage.ShouldBe(33.3m);

        // Item summaries
        result.ItemSummaries.Count.ShouldBe(2);
        var item1Summary = result.ItemSummaries.First(i => i.ItemId == item1Id);
        item1Summary.ItemCode.ShouldBe("ITEM-1");
        item1Summary.ItemName.ShouldBe("Widget A");
        item1Summary.TotalInspected.ShouldBe(2);
        item1Summary.AcceptedCount.ShouldBe(1);
        item1Summary.RejectedCount.ShouldBe(1);

        var item2Summary = result.ItemSummaries.First(i => i.ItemId == item2Id);
        item2Summary.ItemCode.ShouldBe("ITEM-2");
        item2Summary.ItemName.ShouldBe("Widget B");
        item2Summary.TotalInspected.ShouldBe(1);
        item2Summary.AcceptedCount.ShouldBe(1);
        item2Summary.RejectedCount.ShouldBe(0);
    }
}
