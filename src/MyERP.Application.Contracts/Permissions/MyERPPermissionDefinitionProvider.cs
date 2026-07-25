using MyERP.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace MyERP.Permissions;

public class MyERPPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(MyERPPermissions.GroupName);

        var companiesPermission = myGroup.AddPermission(MyERPPermissions.Companies.Default, L("Permission:Companies"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Create, L("Permission:Companies.Create"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Edit, L("Permission:Companies.Edit"));
        companiesPermission.AddChild(MyERPPermissions.Companies.Delete, L("Permission:Companies.Delete"));

        var branchesPermission = myGroup.AddPermission(MyERPPermissions.Branches.Default, L("Permission:Branches"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Create, L("Permission:Branches.Create"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Edit, L("Permission:Branches.Edit"));
        branchesPermission.AddChild(MyERPPermissions.Branches.Delete, L("Permission:Branches.Delete"));

        var accountsPermission = myGroup.AddPermission(MyERPPermissions.Accounts.Default, L("Permission:Accounts"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Create, L("Permission:Accounts.Create"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Edit, L("Permission:Accounts.Edit"));
        accountsPermission.AddChild(MyERPPermissions.Accounts.Delete, L("Permission:Accounts.Delete"));

        var customersPermission = myGroup.AddPermission(MyERPPermissions.Customers.Default, L("Permission:Customers"));
        customersPermission.AddChild(MyERPPermissions.Customers.Create, L("Permission:Customers.Create"));
        customersPermission.AddChild(MyERPPermissions.Customers.Edit, L("Permission:Customers.Edit"));
        customersPermission.AddChild(MyERPPermissions.Customers.Delete, L("Permission:Customers.Delete"));

        var suppliersPermission = myGroup.AddPermission(MyERPPermissions.Suppliers.Default, L("Permission:Suppliers"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Create, L("Permission:Suppliers.Create"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Edit, L("Permission:Suppliers.Edit"));
        suppliersPermission.AddChild(MyERPPermissions.Suppliers.Delete, L("Permission:Suppliers.Delete"));

        var itemsPermission = myGroup.AddPermission(MyERPPermissions.Items.Default, L("Permission:Items"));
        itemsPermission.AddChild(MyERPPermissions.Items.Create, L("Permission:Items.Create"));
        itemsPermission.AddChild(MyERPPermissions.Items.Edit, L("Permission:Items.Edit"));
        itemsPermission.AddChild(MyERPPermissions.Items.Delete, L("Permission:Items.Delete"));

        var warehousesPermission = myGroup.AddPermission(MyERPPermissions.Warehouses.Default, L("Permission:Warehouses"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Create, L("Permission:Warehouses.Create"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Edit, L("Permission:Warehouses.Edit"));
        warehousesPermission.AddChild(MyERPPermissions.Warehouses.Delete, L("Permission:Warehouses.Delete"));

        var taxCategoriesPermission = myGroup.AddPermission(MyERPPermissions.TaxCategories.Default, L("Permission:TaxCategories"));
        taxCategoriesPermission.AddChild(MyERPPermissions.TaxCategories.Create, L("Permission:TaxCategories.Create"));
        taxCategoriesPermission.AddChild(MyERPPermissions.TaxCategories.Edit, L("Permission:TaxCategories.Edit"));
        taxCategoriesPermission.AddChild(MyERPPermissions.TaxCategories.Delete, L("Permission:TaxCategories.Delete"));

        var salesInvoicesPermission = myGroup.AddPermission(MyERPPermissions.SalesInvoices.Default, L("Permission:SalesInvoices"));
        salesInvoicesPermission.AddChild(MyERPPermissions.SalesInvoices.Create, L("Permission:SalesInvoices.Create"));
        salesInvoicesPermission.AddChild(MyERPPermissions.SalesInvoices.Edit, L("Permission:SalesInvoices.Edit"));
        salesInvoicesPermission.AddChild(MyERPPermissions.SalesInvoices.Delete, L("Permission:SalesInvoices.Delete"));
        salesInvoicesPermission.AddChild(MyERPPermissions.SalesInvoices.Submit, L("Permission:SalesInvoices.Submit"));
        salesInvoicesPermission.AddChild(MyERPPermissions.SalesInvoices.Cancel, L("Permission:SalesInvoices.Cancel"));

        var journalEntriesPermission = myGroup.AddPermission(MyERPPermissions.JournalEntries.Default, L("Permission:JournalEntries"));
        journalEntriesPermission.AddChild(MyERPPermissions.JournalEntries.Create, L("Permission:JournalEntries.Create"));
        journalEntriesPermission.AddChild(MyERPPermissions.JournalEntries.Post, L("Permission:JournalEntries.Post"));

        var quotationsPermission = myGroup.AddPermission(MyERPPermissions.Quotations.Default, L("Permission:Quotations"));
        quotationsPermission.AddChild(MyERPPermissions.Quotations.Create, L("Permission:Quotations.Create"));
        quotationsPermission.AddChild(MyERPPermissions.Quotations.Edit, L("Permission:Quotations.Edit"));
        quotationsPermission.AddChild(MyERPPermissions.Quotations.Delete, L("Permission:Quotations.Delete"));
        quotationsPermission.AddChild(MyERPPermissions.Quotations.Submit, L("Permission:Quotations.Submit"));
        quotationsPermission.AddChild(MyERPPermissions.Quotations.Cancel, L("Permission:Quotations.Cancel"));

        var salesOrdersPermission = myGroup.AddPermission(MyERPPermissions.SalesOrders.Default, L("Permission:SalesOrders"));
        salesOrdersPermission.AddChild(MyERPPermissions.SalesOrders.Create, L("Permission:SalesOrders.Create"));
        salesOrdersPermission.AddChild(MyERPPermissions.SalesOrders.Edit, L("Permission:SalesOrders.Edit"));
        salesOrdersPermission.AddChild(MyERPPermissions.SalesOrders.Delete, L("Permission:SalesOrders.Delete"));
        salesOrdersPermission.AddChild(MyERPPermissions.SalesOrders.Submit, L("Permission:SalesOrders.Submit"));
        salesOrdersPermission.AddChild(MyERPPermissions.SalesOrders.Cancel, L("Permission:SalesOrders.Cancel"));

        var stockEntriesPermission = myGroup.AddPermission(MyERPPermissions.StockEntries.Default, L("Permission:StockEntries"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Create, L("Permission:StockEntries.Create"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Edit, L("Permission:StockEntries.Edit"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Delete, L("Permission:StockEntries.Delete"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Submit, L("Permission:StockEntries.Submit"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Post, L("Permission:StockEntries.Post"));
        stockEntriesPermission.AddChild(MyERPPermissions.StockEntries.Cancel, L("Permission:StockEntries.Cancel"));

        var purchaseOrdersPermission = myGroup.AddPermission(MyERPPermissions.PurchaseOrders.Default, L("Permission:PurchaseOrders"));
        purchaseOrdersPermission.AddChild(MyERPPermissions.PurchaseOrders.Create, L("Permission:PurchaseOrders.Create"));
        purchaseOrdersPermission.AddChild(MyERPPermissions.PurchaseOrders.Edit, L("Permission:PurchaseOrders.Edit"));
        purchaseOrdersPermission.AddChild(MyERPPermissions.PurchaseOrders.Delete, L("Permission:PurchaseOrders.Delete"));
        purchaseOrdersPermission.AddChild(MyERPPermissions.PurchaseOrders.Submit, L("Permission:PurchaseOrders.Submit"));
        purchaseOrdersPermission.AddChild(MyERPPermissions.PurchaseOrders.Cancel, L("Permission:PurchaseOrders.Cancel"));

        var purchaseInvoicesPermission = myGroup.AddPermission(MyERPPermissions.PurchaseInvoices.Default, L("Permission:PurchaseInvoices"));
        purchaseInvoicesPermission.AddChild(MyERPPermissions.PurchaseInvoices.Create, L("Permission:PurchaseInvoices.Create"));
        purchaseInvoicesPermission.AddChild(MyERPPermissions.PurchaseInvoices.Edit, L("Permission:PurchaseInvoices.Edit"));
        purchaseInvoicesPermission.AddChild(MyERPPermissions.PurchaseInvoices.Delete, L("Permission:PurchaseInvoices.Delete"));
        purchaseInvoicesPermission.AddChild(MyERPPermissions.PurchaseInvoices.Submit, L("Permission:PurchaseInvoices.Submit"));
        purchaseInvoicesPermission.AddChild(MyERPPermissions.PurchaseInvoices.Cancel, L("Permission:PurchaseInvoices.Cancel"));

        var paymentEntriesPermission = myGroup.AddPermission(MyERPPermissions.PaymentEntries.Default, L("Permission:PaymentEntries"));
        paymentEntriesPermission.AddChild(MyERPPermissions.PaymentEntries.Create, L("Permission:PaymentEntries.Create"));
        paymentEntriesPermission.AddChild(MyERPPermissions.PaymentEntries.Edit, L("Permission:PaymentEntries.Edit"));
        paymentEntriesPermission.AddChild(MyERPPermissions.PaymentEntries.Delete, L("Permission:PaymentEntries.Delete"));
        paymentEntriesPermission.AddChild(MyERPPermissions.PaymentEntries.Submit, L("Permission:PaymentEntries.Submit"));
        paymentEntriesPermission.AddChild(MyERPPermissions.PaymentEntries.Cancel, L("Permission:PaymentEntries.Cancel"));

        var deliveryNotesPermission = myGroup.AddPermission(MyERPPermissions.DeliveryNotes.Default, L("Permission:DeliveryNotes"));
        deliveryNotesPermission.AddChild(MyERPPermissions.DeliveryNotes.Create, L("Permission:DeliveryNotes.Create"));
        deliveryNotesPermission.AddChild(MyERPPermissions.DeliveryNotes.Edit, L("Permission:DeliveryNotes.Edit"));
        deliveryNotesPermission.AddChild(MyERPPermissions.DeliveryNotes.Delete, L("Permission:DeliveryNotes.Delete"));
        deliveryNotesPermission.AddChild(MyERPPermissions.DeliveryNotes.Submit, L("Permission:DeliveryNotes.Submit"));
        deliveryNotesPermission.AddChild(MyERPPermissions.DeliveryNotes.Cancel, L("Permission:DeliveryNotes.Cancel"));

        var packingSlipsPermission = myGroup.AddPermission(MyERPPermissions.PackingSlips.Default, L("Permission:PackingSlips"));
        packingSlipsPermission.AddChild(MyERPPermissions.PackingSlips.Create, L("Permission:PackingSlips.Create"));
        packingSlipsPermission.AddChild(MyERPPermissions.PackingSlips.Delete, L("Permission:PackingSlips.Delete"));
        packingSlipsPermission.AddChild(MyERPPermissions.PackingSlips.Submit, L("Permission:PackingSlips.Submit"));
        packingSlipsPermission.AddChild(MyERPPermissions.PackingSlips.Cancel, L("Permission:PackingSlips.Cancel"));

        var eInvoicePermission = myGroup.AddPermission(MyERPPermissions.EInvoice.Default, L("Permission:EInvoice"));
        eInvoicePermission.AddChild(MyERPPermissions.EInvoice.Submit, L("Permission:EInvoice.Submit"));
        eInvoicePermission.AddChild(MyERPPermissions.EInvoice.Cancel, L("Permission:EInvoice.Cancel"));

        var purchaseReceiptsPermission = myGroup.AddPermission(MyERPPermissions.PurchaseReceipts.Default, L("Permission:PurchaseReceipts"));
        purchaseReceiptsPermission.AddChild(MyERPPermissions.PurchaseReceipts.Create, L("Permission:PurchaseReceipts.Create"));
        purchaseReceiptsPermission.AddChild(MyERPPermissions.PurchaseReceipts.Edit, L("Permission:PurchaseReceipts.Edit"));
        purchaseReceiptsPermission.AddChild(MyERPPermissions.PurchaseReceipts.Delete, L("Permission:PurchaseReceipts.Delete"));
        purchaseReceiptsPermission.AddChild(MyERPPermissions.PurchaseReceipts.Submit, L("Permission:PurchaseReceipts.Submit"));
        purchaseReceiptsPermission.AddChild(MyERPPermissions.PurchaseReceipts.Cancel, L("Permission:PurchaseReceipts.Cancel"));

        var approvalWorkflowPermission = myGroup.AddPermission(MyERPPermissions.ApprovalWorkflows.Default, L("Permission:ApprovalWorkflows"));
        approvalWorkflowPermission.AddChild(MyERPPermissions.ApprovalWorkflows.Create, L("Permission:ApprovalWorkflows.Create"));
        approvalWorkflowPermission.AddChild(MyERPPermissions.ApprovalWorkflows.Edit, L("Permission:ApprovalWorkflows.Edit"));
        approvalWorkflowPermission.AddChild(MyERPPermissions.ApprovalWorkflows.Delete, L("Permission:ApprovalWorkflows.Delete"));

        var importExportPermission = myGroup.AddPermission(MyERPPermissions.ImportExport.Default, L("Permission:ImportExport"));
        importExportPermission.AddChild(MyERPPermissions.ImportExport.Import, L("Permission:ImportExport.Import"));
        importExportPermission.AddChild(MyERPPermissions.ImportExport.Export, L("Permission:ImportExport.Export"));

        var automationPermission = myGroup.AddPermission(MyERPPermissions.AutomationRules.Default, L("Permission:AutomationRules"));
        automationPermission.AddChild(MyERPPermissions.AutomationRules.Create, L("Permission:AutomationRules.Create"));
        automationPermission.AddChild(MyERPPermissions.AutomationRules.Edit, L("Permission:AutomationRules.Edit"));
        automationPermission.AddChild(MyERPPermissions.AutomationRules.Delete, L("Permission:AutomationRules.Delete"));

        var employeesPermission = myGroup.AddPermission(MyERPPermissions.Employees.Default, L("Permission:Employees"));
        employeesPermission.AddChild(MyERPPermissions.Employees.Create, L("Permission:Employees.Create"));
        employeesPermission.AddChild(MyERPPermissions.Employees.Edit, L("Permission:Employees.Edit"));
        employeesPermission.AddChild(MyERPPermissions.Employees.Delete, L("Permission:Employees.Delete"));

        var leadsPermission = myGroup.AddPermission(MyERPPermissions.Leads.Default, L("Permission:Leads"));
        leadsPermission.AddChild(MyERPPermissions.Leads.Create, L("Permission:Leads.Create"));
        leadsPermission.AddChild(MyERPPermissions.Leads.Edit, L("Permission:Leads.Edit"));
        leadsPermission.AddChild(MyERPPermissions.Leads.Delete, L("Permission:Leads.Delete"));
        leadsPermission.AddChild(MyERPPermissions.Leads.Convert, L("Permission:Leads.Convert"));

        var opportunitiesPermission = myGroup.AddPermission(MyERPPermissions.Opportunities.Default, L("Permission:Opportunities"));
        opportunitiesPermission.AddChild(MyERPPermissions.Opportunities.Create, L("Permission:Opportunities.Create"));
        opportunitiesPermission.AddChild(MyERPPermissions.Opportunities.Edit, L("Permission:Opportunities.Edit"));
        opportunitiesPermission.AddChild(MyERPPermissions.Opportunities.Delete, L("Permission:Opportunities.Delete"));
        opportunitiesPermission.AddChild(MyERPPermissions.Opportunities.Convert, L("Permission:Opportunities.Convert"));

        var payrollPermission = myGroup.AddPermission(MyERPPermissions.Payroll.Default, L("Permission:Payroll"));
        payrollPermission.AddChild(MyERPPermissions.Payroll.Create, L("Permission:Payroll.Create"));
        payrollPermission.AddChild(MyERPPermissions.Payroll.Submit, L("Permission:Payroll.Submit"));
        payrollPermission.AddChild(MyERPPermissions.Payroll.Cancel, L("Permission:Payroll.Cancel"));

        var projectsPermission = myGroup.AddPermission(MyERPPermissions.Projects.Default, L("Permission:Projects"));
        projectsPermission.AddChild(MyERPPermissions.Projects.Create, L("Permission:Projects.Create"));
        projectsPermission.AddChild(MyERPPermissions.Projects.Edit, L("Permission:Projects.Edit"));
        projectsPermission.AddChild(MyERPPermissions.Projects.Delete, L("Permission:Projects.Delete"));

        var assetsPermission = myGroup.AddPermission(MyERPPermissions.Assets.Default, L("Permission:Assets"));
        assetsPermission.AddChild(MyERPPermissions.Assets.Create, L("Permission:Assets.Create"));
        assetsPermission.AddChild(MyERPPermissions.Assets.Edit, L("Permission:Assets.Edit"));
        assetsPermission.AddChild(MyERPPermissions.Assets.Delete, L("Permission:Assets.Delete"));
        assetsPermission.AddChild(MyERPPermissions.Assets.Submit, L("Permission:Assets.Submit"));

        var mfgPermission = myGroup.AddPermission(MyERPPermissions.Manufacturing.Default, L("Permission:Manufacturing"));
        mfgPermission.AddChild(MyERPPermissions.Manufacturing.Create, L("Permission:Manufacturing.Create"));
        mfgPermission.AddChild(MyERPPermissions.Manufacturing.Edit, L("Permission:Manufacturing.Edit"));
        mfgPermission.AddChild(MyERPPermissions.Manufacturing.Delete, L("Permission:Manufacturing.Delete"));

        var ppPermission = myGroup.AddPermission(MyERPPermissions.ProductionPlans.Default, L("Permission:ProductionPlans"));
        ppPermission.AddChild(MyERPPermissions.ProductionPlans.Create, L("Permission:ProductionPlans.Create"));
        ppPermission.AddChild(MyERPPermissions.ProductionPlans.Edit, L("Permission:ProductionPlans.Edit"));
        ppPermission.AddChild(MyERPPermissions.ProductionPlans.Delete, L("Permission:ProductionPlans.Delete"));
        ppPermission.AddChild(MyERPPermissions.ProductionPlans.Submit, L("Permission:ProductionPlans.Submit"));
        ppPermission.AddChild(MyERPPermissions.ProductionPlans.Cancel, L("Permission:ProductionPlans.Cancel"));

        var materialRequestsPermission = myGroup.AddPermission(MyERPPermissions.MaterialRequests.Default, L("Permission:MaterialRequests"));
        materialRequestsPermission.AddChild(MyERPPermissions.MaterialRequests.Create, L("Permission:MaterialRequests.Create"));
        materialRequestsPermission.AddChild(MyERPPermissions.MaterialRequests.Edit, L("Permission:MaterialRequests.Edit"));
        materialRequestsPermission.AddChild(MyERPPermissions.MaterialRequests.Delete, L("Permission:MaterialRequests.Delete"));
        materialRequestsPermission.AddChild(MyERPPermissions.MaterialRequests.Submit, L("Permission:MaterialRequests.Submit"));
        materialRequestsPermission.AddChild(MyERPPermissions.MaterialRequests.Cancel, L("Permission:MaterialRequests.Cancel"));

        var issuesPermission = myGroup.AddPermission(MyERPPermissions.Issues.Default, L("Permission:Issues"));
        issuesPermission.AddChild(MyERPPermissions.Issues.Create, L("Permission:Issues.Create"));
        issuesPermission.AddChild(MyERPPermissions.Issues.Edit, L("Permission:Issues.Edit"));
        issuesPermission.AddChild(MyERPPermissions.Issues.Delete, L("Permission:Issues.Delete"));

        var budgetsPermission = myGroup.AddPermission(MyERPPermissions.Budgets.Default, L("Permission:Budgets"));
        budgetsPermission.AddChild(MyERPPermissions.Budgets.Create, L("Permission:Budgets.Create"));
        budgetsPermission.AddChild(MyERPPermissions.Budgets.Edit, L("Permission:Budgets.Edit"));
        budgetsPermission.AddChild(MyERPPermissions.Budgets.Delete, L("Permission:Budgets.Delete"));
        budgetsPermission.AddChild(MyERPPermissions.Budgets.Submit, L("Permission:Budgets.Submit"));
        budgetsPermission.AddChild(MyERPPermissions.Budgets.Cancel, L("Permission:Budgets.Cancel"));

        var qiPermission = myGroup.AddPermission(MyERPPermissions.QualityInspections.Default, L("Permission:QualityInspections"));
        qiPermission.AddChild(MyERPPermissions.QualityInspections.Create, L("Permission:QualityInspections.Create"));
        qiPermission.AddChild(MyERPPermissions.QualityInspections.Edit, L("Permission:QualityInspections.Edit"));
        qiPermission.AddChild(MyERPPermissions.QualityInspections.Delete, L("Permission:QualityInspections.Delete"));
        qiPermission.AddChild(MyERPPermissions.QualityInspections.Submit, L("Permission:QualityInspections.Submit"));

        var srPermission = myGroup.AddPermission(MyERPPermissions.StockReconciliations.Default, L("Permission:StockReconciliations"));
        srPermission.AddChild(MyERPPermissions.StockReconciliations.Create, L("Permission:StockReconciliations.Create"));
        srPermission.AddChild(MyERPPermissions.StockReconciliations.Edit, L("Permission:StockReconciliations.Edit"));
        srPermission.AddChild(MyERPPermissions.StockReconciliations.Delete, L("Permission:StockReconciliations.Delete"));
        srPermission.AddChild(MyERPPermissions.StockReconciliations.Submit, L("Permission:StockReconciliations.Submit"));
        srPermission.AddChild(MyERPPermissions.StockReconciliations.Cancel, L("Permission:StockReconciliations.Cancel"));

        var lcvPermission = myGroup.AddPermission(MyERPPermissions.LandedCostVouchers.Default, L("Permission:LandedCostVouchers"));
        lcvPermission.AddChild(MyERPPermissions.LandedCostVouchers.Create, L("Permission:LandedCostVouchers.Create"));
        lcvPermission.AddChild(MyERPPermissions.LandedCostVouchers.Edit, L("Permission:LandedCostVouchers.Edit"));
        lcvPermission.AddChild(MyERPPermissions.LandedCostVouchers.Delete, L("Permission:LandedCostVouchers.Delete"));
        lcvPermission.AddChild(MyERPPermissions.LandedCostVouchers.Submit, L("Permission:LandedCostVouchers.Submit"));
        lcvPermission.AddChild(MyERPPermissions.LandedCostVouchers.Cancel, L("Permission:LandedCostVouchers.Cancel"));

        var loyaltyPermission = myGroup.AddPermission(MyERPPermissions.LoyaltyPrograms.Default, L("Permission:LoyaltyPrograms"));
        loyaltyPermission.AddChild(MyERPPermissions.LoyaltyPrograms.Create, L("Permission:LoyaltyPrograms.Create"));
        loyaltyPermission.AddChild(MyERPPermissions.LoyaltyPrograms.Edit, L("Permission:LoyaltyPrograms.Edit"));
        loyaltyPermission.AddChild(MyERPPermissions.LoyaltyPrograms.Delete, L("Permission:LoyaltyPrograms.Delete"));

        var scorecardPermission = myGroup.AddPermission(MyERPPermissions.SupplierScorecards.Default, L("Permission:SupplierScorecards"));
        scorecardPermission.AddChild(MyERPPermissions.SupplierScorecards.Create, L("Permission:SupplierScorecards.Create"));
        scorecardPermission.AddChild(MyERPPermissions.SupplierScorecards.Edit, L("Permission:SupplierScorecards.Edit"));
        scorecardPermission.AddChild(MyERPPermissions.SupplierScorecards.Delete, L("Permission:SupplierScorecards.Delete"));

        var shippingPermission = myGroup.AddPermission(MyERPPermissions.ShippingRules.Default, L("Permission:ShippingRules"));
        shippingPermission.AddChild(MyERPPermissions.ShippingRules.Create, L("Permission:ShippingRules.Create"));
        shippingPermission.AddChild(MyERPPermissions.ShippingRules.Edit, L("Permission:ShippingRules.Edit"));
        shippingPermission.AddChild(MyERPPermissions.ShippingRules.Delete, L("Permission:ShippingRules.Delete"));

        var spPermission = myGroup.AddPermission(MyERPPermissions.SalesPersons.Default, L("Permission:SalesPersons"));
        spPermission.AddChild(MyERPPermissions.SalesPersons.Create, L("Permission:SalesPersons.Create"));
        spPermission.AddChild(MyERPPermissions.SalesPersons.Edit, L("Permission:SalesPersons.Edit"));
        spPermission.AddChild(MyERPPermissions.SalesPersons.Delete, L("Permission:SalesPersons.Delete"));

        // Company Restrictions — manager-level only (per ERPNext PR #57383: permlevel 1)
        var crPermission = myGroup.AddPermission(MyERPPermissions.CompanyRestrictions.Default, L("Permission:CompanyRestrictions"));
        crPermission.AddChild(MyERPPermissions.CompanyRestrictions.Manage, L("Permission:CompanyRestrictions.Manage"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MyERPResource>(name);
    }
}
