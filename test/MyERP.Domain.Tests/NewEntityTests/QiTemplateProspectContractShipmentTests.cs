using System;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.NewEntityTests;

/// <summary>
/// Tests for QualityInspectionTemplate, Prospect, Contract, and Shipment entities.
/// </summary>
public class QualityInspectionTemplateTests
{
    [Fact]
    public void Create_DefaultState()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Incoming Inspection");
        template.Name.ShouldBe("Incoming Inspection");
        template.IsEnabled.ShouldBeTrue();
        template.Parameters.ShouldBeEmpty();
        template.ItemId.ShouldBeNull();
        template.BomId.ShouldBeNull();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() => new QualityInspectionTemplate(Guid.NewGuid(), ""));
    }

    [Fact]
    public void AddParameter_ValueBased()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Visual Inspection");
        template.AddParameter(Guid.NewGuid(), "Color", expectedValue: "Red");
        template.Parameters.Count.ShouldBe(1);
        template.Parameters[0].Specification.ShouldBe("Color");
        template.Parameters[0].ExpectedValue.ShouldBe("Red");
        template.Parameters[0].IsNumeric.ShouldBeFalse();
    }

    [Fact]
    public void AddParameter_NumericRange()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Dimensional Check");
        template.AddParameter(Guid.NewGuid(), "Length", minValue: 99.5m, maxValue: 100.5m, isNumeric: true);
        template.Parameters[0].IsNumeric.ShouldBeTrue();
        template.Parameters[0].MinValue.ShouldBe(99.5m);
        template.Parameters[0].MaxValue.ShouldBe(100.5m);
    }

    [Fact]
    public void AddParameter_FormulaBased()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Chemical Test");
        template.AddParameter(Guid.NewGuid(), "pH Level", formulaBased: true, formula: "mean >= 6.5 and mean <= 7.5");
        template.Parameters[0].FormulaBased.ShouldBeTrue();
        template.Parameters[0].Formula.ShouldBe("mean >= 6.5 and mean <= 7.5");
    }

    [Fact]
    public void AddParameter_WithAcceptanceCriteria()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Surface Test");
        template.AddParameter(Guid.NewGuid(), "Roughness", acceptanceCriteria: "Ra ≤ 0.8μm", isNumeric: true, maxValue: 0.8m);
        template.Parameters[0].AcceptanceCriteria.ShouldBe("Ra ≤ 0.8μm");
    }

    [Fact]
    public void Disable_Enable_Lifecycle()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Test");
        template.IsEnabled.ShouldBeTrue();
        template.Disable();
        template.IsEnabled.ShouldBeFalse();
        template.Enable();
        template.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void ItemId_CanBeSet()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Item QI");
        var itemId = Guid.NewGuid();
        template.ItemId = itemId;
        template.ItemId.ShouldBe(itemId);
    }

    [Fact]
    public void MultipleParameters_AllTracked()
    {
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Full QI");
        template.AddParameter(Guid.NewGuid(), "Visual", expectedValue: "Pass");
        template.AddParameter(Guid.NewGuid(), "Weight", minValue: 95m, maxValue: 105m, isNumeric: true);
        template.AddParameter(Guid.NewGuid(), "Moisture", formulaBased: true, formula: "mean < 5");
        template.Parameters.Count.ShouldBe(3);
    }
}

public class ProspectTests
{
    [Fact]
    public void Create_DefaultState()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp");
        prospect.ProspectName.ShouldBe("Acme Corp");
        prospect.IsConverted.ShouldBeFalse();
        prospect.ConvertedCustomerId.ShouldBeNull();
        prospect.Leads.ShouldBeEmpty();
        prospect.Opportunities.ShouldBeEmpty();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Should.Throw<ArgumentException>(() => new Prospect(Guid.NewGuid(), Guid.NewGuid(), ""));
    }

    [Fact]
    public void AddLead_TracksLeads()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Tech Solutions");
        var leadId = Guid.NewGuid();
        prospect.AddLead(Guid.NewGuid(), leadId, "John Doe", "john@tech.com");
        prospect.Leads.Count.ShouldBe(1);
        prospect.Leads[0].LeadId.ShouldBe(leadId);
        prospect.Leads[0].LeadName.ShouldBe("John Doe");
        prospect.Leads[0].Email.ShouldBe("john@tech.com");
    }

    [Fact]
    public void AddOpportunity_TracksOpportunities()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Big Deal Inc");
        var oppId = Guid.NewGuid();
        prospect.AddOpportunity(Guid.NewGuid(), oppId, "Software License", 50_000m);
        prospect.Opportunities.Count.ShouldBe(1);
        prospect.Opportunities[0].Amount.ShouldBe(50_000m);
    }

    [Fact]
    public void ConvertToCustomer_SetsCustomerId()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Convert Me");
        var customerId = Guid.NewGuid();
        prospect.ConvertToCustomer(customerId);
        prospect.IsConverted.ShouldBeTrue();
        prospect.ConvertedCustomerId.ShouldBe(customerId);
    }

    [Fact]
    public void ConvertToCustomer_DoubleConversion_Throws()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Already Done");
        prospect.ConvertToCustomer(Guid.NewGuid());
        Should.Throw<BusinessException>(() => prospect.ConvertToCustomer(Guid.NewGuid()));
    }

    [Fact]
    public void AddLead_AfterConversion_Throws()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Converted");
        prospect.ConvertToCustomer(Guid.NewGuid());
        Should.Throw<BusinessException>(() => prospect.AddLead(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void MultipleLeads_AllTracked()
    {
        var prospect = new Prospect(Guid.NewGuid(), Guid.NewGuid(), "Multi-Contact Corp");
        prospect.AddLead(Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@corp.com");
        prospect.AddLead(Guid.NewGuid(), Guid.NewGuid(), "Bob", "bob@corp.com");
        prospect.AddLead(Guid.NewGuid(), Guid.NewGuid(), "Charlie", "charlie@corp.com");
        prospect.Leads.Count.ShouldBe(3);
    }
}

public class ContractTests
{
    [Fact]
    public void Create_DefaultState()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-001", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Status.ShouldBe(ContractStatus.Unsigned);
        contract.SigningDate.ShouldBeNull();
        contract.EndDate.ShouldBeNull();
        contract.ContractValue.ShouldBeNull();
    }

    [Fact]
    public void Sign_TransitionsToActive()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-002", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        contract.Status.ShouldBe(ContractStatus.Active);
        contract.SigningDate.ShouldBe(DateTime.Today);
    }

    [Fact]
    public void Sign_FromNonUnsigned_Throws()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-003", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        // Try to sign again from Active
        Should.Throw<BusinessException>(() => contract.Sign(DateTime.Today));
    }

    [Fact]
    public void Renew_ExtendsEndDate()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-004", "Supplier",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        var newEnd = DateTime.Today.AddYears(1);
        contract.Renew(newEnd);
        contract.EndDate.ShouldBe(newEnd);
        contract.Status.ShouldBe(ContractStatus.Active); // Still active
    }

    [Fact]
    public void Renew_FromNonActive_Throws()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-005", "Customer",
            Guid.NewGuid(), DateTime.Today);
        // Unsigned cannot renew
        Should.Throw<BusinessException>(() => contract.Renew(DateTime.Today.AddYears(1)));
    }

    [Fact]
    public void IsExpired_WhenPastEndDate()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-006", "Customer",
            Guid.NewGuid(), new DateTime(2025, 1, 1));
        contract.EndDate = new DateTime(2025, 12, 31);
        contract.IsExpired(new DateTime(2026, 1, 1)).ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_FalseWhenBeforeEndDate()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-007", "Customer",
            Guid.NewGuid(), new DateTime(2025, 1, 1));
        contract.EndDate = new DateTime(2026, 12, 31);
        contract.IsExpired(new DateTime(2026, 6, 15)).ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_FalseWhenNoEndDate()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-008", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.IsExpired(DateTime.Today.AddYears(10)).ShouldBeFalse();
    }

    [Fact]
    public void MarkInactive_ByExpiry()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-009", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        contract.MarkInactive(failedRenewal: false);
        contract.Status.ShouldBe(ContractStatus.InactiveByExpiry);
    }

    [Fact]
    public void MarkInactive_ByFailedRenewal()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-010", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Sign(DateTime.Today);
        contract.MarkInactive(failedRenewal: true);
        contract.Status.ShouldBe(ContractStatus.InactiveByAutoRenewFailure);
    }

    [Fact]
    public void Cancel_Succeeds()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-011", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Cancel();
        contract.Status.ShouldBe(ContractStatus.Cancelled);
    }

    [Fact]
    public void Cancel_DoubleCancellation_Throws()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-012", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.Cancel();
        Should.Throw<BusinessException>(() => contract.Cancel());
    }

    [Fact]
    public void ContractValue_CanBeSet()
    {
        var contract = new Contract(Guid.NewGuid(), Guid.NewGuid(), "CTR-013", "Customer",
            Guid.NewGuid(), DateTime.Today);
        contract.ContractValue = 100_000m;
        contract.CurrencyCode = "MYR";
        contract.ContractValue.ShouldBe(100_000m);
        contract.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void AllStatusValues_Exist()
    {
        Enum.GetValues<ContractStatus>().Length.ShouldBe(5);
    }
}

public class ShipmentTests
{
    [Fact]
    public void Create_DefaultState()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-001");
        shipment.ShipmentNumber.ShouldBe("SHP-001");
        shipment.Status.ShouldBe(ShipmentStatus.Draft);
        shipment.DeliveryNotes.ShouldBeEmpty();
        shipment.TrackingNumber.ShouldBeNull();
    }

    [Fact]
    public void Create_EmptyNumber_Throws()
    {
        Should.Throw<ArgumentException>(() => new Shipment(Guid.NewGuid(), Guid.NewGuid(), ""));
    }

    [Fact]
    public void AddDeliveryNote_Links()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-002");
        var dnId = Guid.NewGuid();
        shipment.AddDeliveryNote(Guid.NewGuid(), dnId, "DN-001", 5_000m);
        shipment.DeliveryNotes.Count.ShouldBe(1);
        shipment.DeliveryNotes[0].DeliveryNoteId.ShouldBe(dnId);
        shipment.DeliveryNotes[0].GrandTotal.ShouldBe(5_000m);
    }

    [Fact]
    public void Submit_Transitions_ToBooked()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-003");
        shipment.Submit();
        shipment.Status.ShouldBe(ShipmentStatus.Booked);
    }

    [Fact]
    public void Submit_FromNonDraft_Throws()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-004");
        shipment.Submit();
        Should.Throw<BusinessException>(() => shipment.Submit());
    }

    [Fact]
    public void MarkInTransit_FromBooked()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-005");
        shipment.Submit();
        shipment.MarkInTransit();
        shipment.Status.ShouldBe(ShipmentStatus.InTransit);
    }

    [Fact]
    public void MarkDelivered_FromInTransit()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-006");
        shipment.Submit();
        shipment.MarkInTransit();
        shipment.MarkDelivered(DateTime.Today);
        shipment.Status.ShouldBe(ShipmentStatus.Delivered);
        shipment.DeliveryDate.ShouldBe(DateTime.Today);
    }

    [Fact]
    public void MarkDelivered_FromBooked_Succeeds()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-007");
        shipment.Submit();
        shipment.MarkDelivered(DateTime.Today); // Direct: Booked → Delivered (skip InTransit)
        shipment.Status.ShouldBe(ShipmentStatus.Delivered);
    }

    [Fact]
    public void Cancel_FromBooked()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-008");
        shipment.Submit();
        shipment.Cancel();
        shipment.Status.ShouldBe(ShipmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromDelivered_Throws()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-009");
        shipment.Submit();
        shipment.MarkDelivered(DateTime.Today);
        Should.Throw<BusinessException>(() => shipment.Cancel());
    }

    [Fact]
    public void Cancel_DoubleCancellation_Throws()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-010");
        shipment.Cancel();
        Should.Throw<BusinessException>(() => shipment.Cancel());
    }

    [Fact]
    public void AddDeliveryNote_AfterCancel_Throws()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-011");
        shipment.Cancel();
        Should.Throw<BusinessException>(() =>
            shipment.AddDeliveryNote(Guid.NewGuid(), Guid.NewGuid(), "DN-X", 1000m));
    }

    [Fact]
    public void FullLifecycle_Draft_Booked_InTransit_Delivered()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-012");
        shipment.AddDeliveryNote(Guid.NewGuid(), Guid.NewGuid(), "DN-100", 10_000m);
        shipment.Submit();
        shipment.MarkInTransit();
        shipment.MarkDelivered(DateTime.Today.AddDays(3));
        shipment.Status.ShouldBe(ShipmentStatus.Delivered);
        shipment.DeliveryNotes.Count.ShouldBe(1);
    }

    [Fact]
    public void TrackingDetails_CanBeSet()
    {
        var shipment = new Shipment(Guid.NewGuid(), Guid.NewGuid(), "SHP-013");
        shipment.Carrier = "DHL";
        shipment.TrackingNumber = "1234567890";
        shipment.TrackingUrl = "https://track.dhl.com/1234567890";
        shipment.TotalNetWeight = 15.5m;
        shipment.TotalGrossWeight = 18.0m;
        shipment.WeightUom = "Kg";
        shipment.ValueOfGoods = 25_000m;
        shipment.CurrencyCode = "MYR";

        shipment.Carrier.ShouldBe("DHL");
        shipment.TrackingNumber.ShouldBe("1234567890");
        shipment.TotalNetWeight.ShouldBe(15.5m);
        shipment.ValueOfGoods.ShouldBe(25_000m);
    }

    [Fact]
    public void AllStatusValues_Exist()
    {
        Enum.GetValues<ShipmentStatus>().Length.ShouldBe(5);
    }
}
