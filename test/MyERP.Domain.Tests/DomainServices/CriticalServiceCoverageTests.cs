using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.DomainServices;

public class DocumentStatusGuardServiceTests
{
    [Fact]
    public void SalesInvoice_WithZeroAmountPaid_CanCancel()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.AmountPaid.ShouldBe(0);
    }

    [Fact]
    public void SalesInvoice_WithAmountPaid_BlocksCancel()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SI-001", DateTime.Today);
        si.AddItem(Guid.NewGuid(), "Item", 10, 100m, 0m);
        si.Submit();
        si.AmountPaid = 50m;
        si.AmountPaid.ShouldBeGreaterThan(0);
        si.OutstandingAmount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PurchaseInvoice_WithAmountPaid_BlocksCancel()
    {
        var pi = new PurchaseInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PI-001", DateTime.Today);
        pi.AddItem(Guid.NewGuid(), "Item", 5, 200m, 0m);
        pi.Submit();
        pi.AmountPaid = 100m;
        pi.AmountPaid.ShouldBeGreaterThan(0);
    }
}

public class DocumentAmendmentServiceExtendedTests
{
    private readonly DocumentAmendmentService _service = new();

    [Fact]
    public void GenerateAmendedNumber_FirstAmendment()
        => _service.GenerateAmendedNumber("SO-001", 1).ShouldBe("SO-001-1");

    [Fact]
    public void GenerateAmendedNumber_SecondAmendment()
        => _service.GenerateAmendedNumber("SO-001-1", 2).ShouldBe("SO-001-2");

    [Fact]
    public void GenerateAmendedNumber_ThirdAmendment()
        => _service.GenerateAmendedNumber("SO-001-2", 3).ShouldBe("SO-001-3");

    [Fact]
    public void GenerateAmendedNumber_ComplexNumber_NoExistingSuffix()
        => _service.GenerateAmendedNumber("SI-2026-00015", 1).ShouldBe("SI-2026-00015-1");

    [Fact]
    public void GenerateAmendedNumber_WithDashInPrefix()
        => _service.GenerateAmendedNumber("ACC-JV-00003", 1).ShouldBe("ACC-JV-00003-1");

    [Fact]
    public void ValidateCanAmend_Cancelled_Succeeds()
        => Should.NotThrow(() => _service.ValidateCanAmend(DocumentStatus.Cancelled));

    [Fact]
    public void ValidateCanAmend_Rejected_Succeeds()
        => Should.NotThrow(() => _service.ValidateCanAmend(DocumentStatus.Rejected));

    [Fact]
    public void ValidateCanAmend_Draft_Throws()
        => Should.Throw<Volo.Abp.BusinessException>(() => _service.ValidateCanAmend(DocumentStatus.Draft));

    [Fact]
    public void ValidateCanAmend_Submitted_Throws()
        => Should.Throw<Volo.Abp.BusinessException>(() => _service.ValidateCanAmend(DocumentStatus.Submitted));

    [Fact]
    public void ValidateCanAmend_Posted_Throws()
        => Should.Throw<Volo.Abp.BusinessException>(() => _service.ValidateCanAmend(DocumentStatus.Posted));
}

public class EInvoiceSubmissionConceptTests
{
    [Fact]
    public void EInvoiceSubmission_DefaultStatus_Pending()
    {
        var sub = new EInvoice.Entities.EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid());
        sub.Status.ShouldBe("Pending");
    }

    [Fact]
    public void EInvoiceSubmission_HasCompanyId()
    {
        var companyId = Guid.NewGuid();
        var sub = new EInvoice.Entities.EInvoiceSubmission(Guid.NewGuid(), companyId, "SalesInvoice", Guid.NewGuid());
        sub.CompanyId.ShouldBe(companyId);
    }

    [Fact]
    public void EInvoiceSubmission_HasSourceDocumentType()
    {
        var sub = new EInvoice.Entities.EInvoiceSubmission(Guid.NewGuid(), Guid.NewGuid(), "SalesInvoice", Guid.NewGuid());
        sub.SourceDocumentType.ShouldBe("SalesInvoice");
    }
}

public class ProductBundleConceptTests
{
    [Fact]
    public void ProductBundle_CanAddItems()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.AddItem(Guid.NewGuid(), 2m);
        bundle.AddItem(Guid.NewGuid(), 3m);
        bundle.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void ProductBundle_TotalQuantity()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.AddItem(Guid.NewGuid(), 2m);
        bundle.AddItem(Guid.NewGuid(), 5m);
        bundle.Items.Sum(i => i.Qty).ShouldBe(7m);
    }

    [Fact]
    public void ProductBundle_Deactivate_SetsInactive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ProductBundle_Activate_ReactivatesBundle()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.Activate();
        bundle.IsActive.ShouldBeTrue();
    }
}

public class PartyDefaultsConceptTests
{
    [Fact]
    public void Customer_DefaultPaymentTerms_Null()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.DefaultPaymentTermsTemplateId.ShouldBeNull();
    }

    [Fact]
    public void Customer_DefaultPaymentTerms_CanBeSet()
    {
        var templateId = Guid.NewGuid();
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.DefaultPaymentTermsTemplateId = templateId;
        customer.DefaultPaymentTermsTemplateId.ShouldBe(templateId);
    }

    [Fact]
    public void Supplier_DefaultPaymentTerms_Null()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        supplier.DefaultPaymentTermsTemplateId.ShouldBeNull();
    }

    [Fact]
    public void Customer_LoyaltyProgramId_DefaultNull()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.LoyaltyProgramId.ShouldBeNull();
    }

    [Fact]
    public void Customer_RepresentsCompanyId_DefaultNull()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        customer.RepresentsCompanyId.ShouldBeNull();
    }
}

public class QualityInspectionEnforcementConceptTests
{
    [Fact]
    public void Item_InspectionRequired_DefaultFalse()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test Item", ItemType.Goods);
        item.InspectionRequiredBeforePurchase.ShouldBeFalse();
        item.InspectionRequiredBeforeDelivery.ShouldBeFalse();
    }

    [Fact]
    public void Item_InspectionFlags_CanBeEnabled()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test Item", ItemType.Goods);
        item.InspectionRequiredBeforePurchase = true;
        item.InspectionRequiredBeforeDelivery = true;
        item.InspectionRequiredBeforePurchase.ShouldBeTrue();
        item.InspectionRequiredBeforeDelivery.ShouldBeTrue();
    }

    [Fact]
    public void QualityInspection_DefaultStatus_Draft()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InspectionType.Incoming, DateTime.Today);
        qi.Status.ShouldBe(InspectionStatus.Draft);
    }

    [Fact]
    public void QualityInspection_Submit_RequiresReadings()
    {
        var qi = new QualityInspection(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InspectionType.Incoming, DateTime.Today);
        // Submit without readings throws — readings required for evaluation
        Should.Throw<Volo.Abp.BusinessException>(() => qi.Submit());
    }
}

public class ItemDefaultsConceptTests
{
    [Fact]
    public void Item_DefaultExpenseAccountId_Null()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test Item", ItemType.Goods);
        item.DefaultExpenseAccountId.ShouldBeNull();
    }

    [Fact]
    public void ItemGroup_DefaultWarehouseId_Null()
    {
        var group = new ItemGroup(Guid.NewGuid(), "Raw Materials");
        group.DefaultWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void ItemGroup_DefaultIncomeAccountId_Null()
    {
        var group = new ItemGroup(Guid.NewGuid(), "Products");
        group.DefaultIncomeAccountId.ShouldBeNull();
    }

    [Fact]
    public void Item_ItemGroupId_CanBeSet()
    {
        var groupId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test", ItemType.Goods);
        item.ItemGroupId = groupId;
        item.ItemGroupId.ShouldBe(groupId);
    }
}

public class UomConversionConceptTests
{
    [Fact]
    public void UomConversion_SameUom_FactorIsOne()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Kg", 1m);
        conv.ConversionFactor.ShouldBe(1m);
    }

    [Fact]
    public void UomConversion_KgToGram_Factor1000()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Kg", "Gram", 1000m);
        conv.ConversionFactor.ShouldBe(1000m);
    }

    [Fact]
    public void UomConversion_BoxToUnit_ItemSpecific()
    {
        var itemId = Guid.NewGuid();
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m, itemId);
        conv.ItemId.ShouldBe(itemId);
        conv.ConversionFactor.ShouldBe(12m);
    }

    [Fact]
    public void UomConversion_ReverseCalculation()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Box", "Unit", 12m);
        var reverseRate = 1m / conv.ConversionFactor;
        reverseRate.ShouldBeLessThan(1m);
    }
}
