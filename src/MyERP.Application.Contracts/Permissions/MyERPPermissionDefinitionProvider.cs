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

        var repostAccountingLedgerPermission = myGroup.AddPermission(MyERPPermissions.RepostAccountingLedger.Default, L("Permission:RepostAccountingLedger"));
        repostAccountingLedgerPermission.AddChild(MyERPPermissions.RepostAccountingLedger.Create, L("Permission:RepostAccountingLedger.Create"));
        repostAccountingLedgerPermission.AddChild(MyERPPermissions.RepostAccountingLedger.Submit, L("Permission:RepostAccountingLedger.Submit"));
        repostAccountingLedgerPermission.AddChild(MyERPPermissions.RepostAccountingLedger.Cancel, L("Permission:RepostAccountingLedger.Cancel"));

        var processPaymentReconciliationPermission = myGroup.AddPermission(MyERPPermissions.ProcessPaymentReconciliation.Default, L("Permission:ProcessPaymentReconciliation"));
        processPaymentReconciliationPermission.AddChild(MyERPPermissions.ProcessPaymentReconciliation.Create, L("Permission:ProcessPaymentReconciliation.Create"));
        processPaymentReconciliationPermission.AddChild(MyERPPermissions.ProcessPaymentReconciliation.Submit, L("Permission:ProcessPaymentReconciliation.Submit"));
        processPaymentReconciliationPermission.AddChild(MyERPPermissions.ProcessPaymentReconciliation.Cancel, L("Permission:ProcessPaymentReconciliation.Cancel"));

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

        var paymentOrdersPermission = myGroup.AddPermission(MyERPPermissions.PaymentOrders.Default, L("Permission:PaymentOrders"));
        paymentOrdersPermission.AddChild(MyERPPermissions.PaymentOrders.Create, L("Permission:PaymentOrders.Create"));
        paymentOrdersPermission.AddChild(MyERPPermissions.PaymentOrders.Edit, L("Permission:PaymentOrders.Edit"));
        paymentOrdersPermission.AddChild(MyERPPermissions.PaymentOrders.Delete, L("Permission:PaymentOrders.Delete"));
        paymentOrdersPermission.AddChild(MyERPPermissions.PaymentOrders.Submit, L("Permission:PaymentOrders.Submit"));
        paymentOrdersPermission.AddChild(MyERPPermissions.PaymentOrders.Cancel, L("Permission:PaymentOrders.Cancel"));

        var unreconcilePaymentsPermission = myGroup.AddPermission(MyERPPermissions.UnreconcilePayments.Default, L("Permission:UnreconcilePayments"));
        unreconcilePaymentsPermission.AddChild(MyERPPermissions.UnreconcilePayments.Create, L("Permission:UnreconcilePayments.Create"));
        unreconcilePaymentsPermission.AddChild(MyERPPermissions.UnreconcilePayments.Submit, L("Permission:UnreconcilePayments.Submit"));
        unreconcilePaymentsPermission.AddChild(MyERPPermissions.UnreconcilePayments.Cancel, L("Permission:UnreconcilePayments.Cancel"));

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

        var mpsPermission = myGroup.AddPermission(MyERPPermissions.MasterProductionSchedules.Default, L("Permission:MasterProductionSchedules"));
        mpsPermission.AddChild(MyERPPermissions.MasterProductionSchedules.Create, L("Permission:MasterProductionSchedules.Create"));
        mpsPermission.AddChild(MyERPPermissions.MasterProductionSchedules.Edit, L("Permission:MasterProductionSchedules.Edit"));
        mpsPermission.AddChild(MyERPPermissions.MasterProductionSchedules.Delete, L("Permission:MasterProductionSchedules.Delete"));
        mpsPermission.AddChild(MyERPPermissions.MasterProductionSchedules.Submit, L("Permission:MasterProductionSchedules.Submit"));
        mpsPermission.AddChild(MyERPPermissions.MasterProductionSchedules.Cancel, L("Permission:MasterProductionSchedules.Cancel"));

        var salesForecastsPermission = myGroup.AddPermission(MyERPPermissions.SalesForecasts.Default, L("Permission:SalesForecasts"));
        salesForecastsPermission.AddChild(MyERPPermissions.SalesForecasts.Create, L("Permission:SalesForecasts.Create"));
        salesForecastsPermission.AddChild(MyERPPermissions.SalesForecasts.Edit, L("Permission:SalesForecasts.Edit"));
        salesForecastsPermission.AddChild(MyERPPermissions.SalesForecasts.Delete, L("Permission:SalesForecasts.Delete"));
        salesForecastsPermission.AddChild(MyERPPermissions.SalesForecasts.Submit, L("Permission:SalesForecasts.Submit"));
        salesForecastsPermission.AddChild(MyERPPermissions.SalesForecasts.Cancel, L("Permission:SalesForecasts.Cancel"));

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

        var slaPermission = myGroup.AddPermission(MyERPPermissions.ServiceLevelAgreements.Default, L("Permission:ServiceLevelAgreements"));
        slaPermission.AddChild(MyERPPermissions.ServiceLevelAgreements.Create, L("Permission:ServiceLevelAgreements.Create"));
        slaPermission.AddChild(MyERPPermissions.ServiceLevelAgreements.Edit, L("Permission:ServiceLevelAgreements.Edit"));
        slaPermission.AddChild(MyERPPermissions.ServiceLevelAgreements.Delete, L("Permission:ServiceLevelAgreements.Delete"));

        var issuePrioritiesPermission = myGroup.AddPermission(MyERPPermissions.IssuePriorities.Default, L("Permission:IssuePriorities"));
        issuePrioritiesPermission.AddChild(MyERPPermissions.IssuePriorities.Create, L("Permission:IssuePriorities.Create"));
        issuePrioritiesPermission.AddChild(MyERPPermissions.IssuePriorities.Edit, L("Permission:IssuePriorities.Edit"));
        issuePrioritiesPermission.AddChild(MyERPPermissions.IssuePriorities.Delete, L("Permission:IssuePriorities.Delete"));

        var issueTypesPermission = myGroup.AddPermission(MyERPPermissions.IssueTypes.Default, L("Permission:IssueTypes"));
        issueTypesPermission.AddChild(MyERPPermissions.IssueTypes.Create, L("Permission:IssueTypes.Create"));
        issueTypesPermission.AddChild(MyERPPermissions.IssueTypes.Edit, L("Permission:IssueTypes.Edit"));
        issueTypesPermission.AddChild(MyERPPermissions.IssueTypes.Delete, L("Permission:IssueTypes.Delete"));

        var supportSettingsPermission = myGroup.AddPermission(MyERPPermissions.SupportSettings.Default, L("Permission:SupportSettings"));
        supportSettingsPermission.AddChild(MyERPPermissions.SupportSettings.Edit, L("Permission:SupportSettings.Edit"));

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

        var shareManagementPermission = myGroup.AddPermission(MyERPPermissions.ShareManagement.Default, L("Permission:ShareManagement"));
        shareManagementPermission.AddChild(MyERPPermissions.ShareManagement.Create, L("Permission:ShareManagement.Create"));
        shareManagementPermission.AddChild(MyERPPermissions.ShareManagement.Edit, L("Permission:ShareManagement.Edit"));
        shareManagementPermission.AddChild(MyERPPermissions.ShareManagement.Delete, L("Permission:ShareManagement.Delete"));
        shareManagementPermission.AddChild(MyERPPermissions.ShareManagement.Submit, L("Permission:ShareManagement.Submit"));
        shareManagementPermission.AddChild(MyERPPermissions.ShareManagement.Cancel, L("Permission:ShareManagement.Cancel"));

        var promoSchemePermission = myGroup.AddPermission(MyERPPermissions.PromotionalSchemes.Default, L("Permission:PromotionalSchemes"));
        promoSchemePermission.AddChild(MyERPPermissions.PromotionalSchemes.Create, L("Permission:PromotionalSchemes.Create"));
        promoSchemePermission.AddChild(MyERPPermissions.PromotionalSchemes.Edit, L("Permission:PromotionalSchemes.Edit"));
        promoSchemePermission.AddChild(MyERPPermissions.PromotionalSchemes.Delete, L("Permission:PromotionalSchemes.Delete"));

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

        // Sales Partners
        var salesPartnerPermission = myGroup.AddPermission(MyERPPermissions.SalesPartners.Default, L("Permission:SalesPartners"));
        salesPartnerPermission.AddChild(MyERPPermissions.SalesPartners.Create, L("Permission:SalesPartners.Create"));
        salesPartnerPermission.AddChild(MyERPPermissions.SalesPartners.Edit, L("Permission:SalesPartners.Edit"));
        salesPartnerPermission.AddChild(MyERPPermissions.SalesPartners.Delete, L("Permission:SalesPartners.Delete"));

        // Warranty Claims
        var warrantyPermission = myGroup.AddPermission(MyERPPermissions.WarrantyClaims.Default, L("Permission:WarrantyClaims"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Create, L("Permission:WarrantyClaims.Create"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Edit, L("Permission:WarrantyClaims.Edit"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Delete, L("Permission:WarrantyClaims.Delete"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.StartWork, L("Permission:WarrantyClaims.StartWork"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Close, L("Permission:WarrantyClaims.Close"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Reopen, L("Permission:WarrantyClaims.Reopen"));
        warrantyPermission.AddChild(MyERPPermissions.WarrantyClaims.Cancel, L("Permission:WarrantyClaims.Cancel"));

        // Maintenance Schedules
        var schedulePermission = myGroup.AddPermission(MyERPPermissions.MaintenanceSchedules.Default, L("Permission:MaintenanceSchedules"));
        schedulePermission.AddChild(MyERPPermissions.MaintenanceSchedules.Create, L("Permission:MaintenanceSchedules.Create"));
        schedulePermission.AddChild(MyERPPermissions.MaintenanceSchedules.Edit, L("Permission:MaintenanceSchedules.Edit"));
        schedulePermission.AddChild(MyERPPermissions.MaintenanceSchedules.Delete, L("Permission:MaintenanceSchedules.Delete"));
        schedulePermission.AddChild(MyERPPermissions.MaintenanceSchedules.Submit, L("Permission:MaintenanceSchedules.Submit"));

        // Maintenance Visits
        var visitPermission = myGroup.AddPermission(MyERPPermissions.MaintenanceVisits.Default, L("Permission:MaintenanceVisits"));
        visitPermission.AddChild(MyERPPermissions.MaintenanceVisits.Create, L("Permission:MaintenanceVisits.Create"));
        visitPermission.AddChild(MyERPPermissions.MaintenanceVisits.Edit, L("Permission:MaintenanceVisits.Edit"));
        visitPermission.AddChild(MyERPPermissions.MaintenanceVisits.Delete, L("Permission:MaintenanceVisits.Delete"));
        visitPermission.AddChild(MyERPPermissions.MaintenanceVisits.Submit, L("Permission:MaintenanceVisits.Submit"));

        // Warehouse Accounts
        var warehouseAccountPermission = myGroup.AddPermission(MyERPPermissions.WarehouseAccounts.Default, L("Permission:WarehouseAccounts"));
        warehouseAccountPermission.AddChild(MyERPPermissions.WarehouseAccounts.Create, L("Permission:WarehouseAccounts.Create"));
        warehouseAccountPermission.AddChild(MyERPPermissions.WarehouseAccounts.Edit, L("Permission:WarehouseAccounts.Edit"));
        warehouseAccountPermission.AddChild(MyERPPermissions.WarehouseAccounts.Delete, L("Permission:WarehouseAccounts.Delete"));

        // Bank Guarantees
        var bgPermission = myGroup.AddPermission(MyERPPermissions.BankGuarantees.Default, L("Permission:BankGuarantees"));
        bgPermission.AddChild(MyERPPermissions.BankGuarantees.Create, L("Permission:BankGuarantees.Create"));
        bgPermission.AddChild(MyERPPermissions.BankGuarantees.Edit, L("Permission:BankGuarantees.Edit"));
        bgPermission.AddChild(MyERPPermissions.BankGuarantees.Delete, L("Permission:BankGuarantees.Delete"));
        bgPermission.AddChild(MyERPPermissions.BankGuarantees.Submit, L("Permission:BankGuarantees.Submit"));
        bgPermission.AddChild(MyERPPermissions.BankGuarantees.Cancel, L("Permission:BankGuarantees.Cancel"));

        // Customs Tariff Numbers
        var ctnPermission = myGroup.AddPermission(MyERPPermissions.CustomsTariffNumbers.Default, L("Permission:CustomsTariffNumbers"));
        ctnPermission.AddChild(MyERPPermissions.CustomsTariffNumbers.Create, L("Permission:CustomsTariffNumbers.Create"));
        ctnPermission.AddChild(MyERPPermissions.CustomsTariffNumbers.Edit, L("Permission:CustomsTariffNumbers.Edit"));
        ctnPermission.AddChild(MyERPPermissions.CustomsTariffNumbers.Delete, L("Permission:CustomsTariffNumbers.Delete"));

        // Manufacturers
        var mfrPermission = myGroup.AddPermission(MyERPPermissions.Manufacturers.Default, L("Permission:Manufacturers"));
        mfrPermission.AddChild(MyERPPermissions.Manufacturers.Create, L("Permission:Manufacturers.Create"));
        mfrPermission.AddChild(MyERPPermissions.Manufacturers.Edit, L("Permission:Manufacturers.Edit"));
        mfrPermission.AddChild(MyERPPermissions.Manufacturers.Delete, L("Permission:Manufacturers.Delete"));

        // Item Manufacturers
        var imPermission = myGroup.AddPermission(MyERPPermissions.ItemManufacturers.Default, L("Permission:ItemManufacturers"));
        imPermission.AddChild(MyERPPermissions.ItemManufacturers.Create, L("Permission:ItemManufacturers.Create"));
        imPermission.AddChild(MyERPPermissions.ItemManufacturers.Edit, L("Permission:ItemManufacturers.Edit"));
        imPermission.AddChild(MyERPPermissions.ItemManufacturers.Delete, L("Permission:ItemManufacturers.Delete"));

        // Item Alternatives
        var iaPermission = myGroup.AddPermission(MyERPPermissions.ItemAlternatives.Default, L("Permission:ItemAlternatives"));
        iaPermission.AddChild(MyERPPermissions.ItemAlternatives.Create, L("Permission:ItemAlternatives.Create"));
        iaPermission.AddChild(MyERPPermissions.ItemAlternatives.Edit, L("Permission:ItemAlternatives.Edit"));
        iaPermission.AddChild(MyERPPermissions.ItemAlternatives.Delete, L("Permission:ItemAlternatives.Delete"));

        // Party Specific Items
        var psiPermission = myGroup.AddPermission(MyERPPermissions.PartySpecificItems.Default, L("Permission:PartySpecificItems"));
        psiPermission.AddChild(MyERPPermissions.PartySpecificItems.Create, L("Permission:PartySpecificItems.Create"));
        psiPermission.AddChild(MyERPPermissions.PartySpecificItems.Edit, L("Permission:PartySpecificItems.Edit"));
        psiPermission.AddChild(MyERPPermissions.PartySpecificItems.Delete, L("Permission:PartySpecificItems.Delete"));

        // Delivery Trips
        var dtPermission = myGroup.AddPermission(MyERPPermissions.DeliveryTrips.Default, L("Permission:DeliveryTrips"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Create, L("Permission:DeliveryTrips.Create"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Edit, L("Permission:DeliveryTrips.Edit"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Delete, L("Permission:DeliveryTrips.Delete"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Schedule, L("Permission:DeliveryTrips.Schedule"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Transit, L("Permission:DeliveryTrips.Transit"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Complete, L("Permission:DeliveryTrips.Complete"));
        dtPermission.AddChild(MyERPPermissions.DeliveryTrips.Cancel, L("Permission:DeliveryTrips.Cancel"));


        var amPermission = myGroup.AddPermission(MyERPPermissions.AssetMaintenances.Default, L("Permission:AssetMaintenances"));
        amPermission.AddChild(MyERPPermissions.AssetMaintenances.Create, L("Permission:AssetMaintenances.Create"));
        amPermission.AddChild(MyERPPermissions.AssetMaintenances.Edit, L("Permission:AssetMaintenances.Edit"));
        amPermission.AddChild(MyERPPermissions.AssetMaintenances.Delete, L("Permission:AssetMaintenances.Delete"));

        var amlPermission = myGroup.AddPermission(MyERPPermissions.AssetMaintenanceLogs.Default, L("Permission:AssetMaintenanceLogs"));
        amlPermission.AddChild(MyERPPermissions.AssetMaintenanceLogs.Create, L("Permission:AssetMaintenanceLogs.Create"));
        amlPermission.AddChild(MyERPPermissions.AssetMaintenanceLogs.Edit, L("Permission:AssetMaintenanceLogs.Edit"));
        amlPermission.AddChild(MyERPPermissions.AssetMaintenanceLogs.Delete, L("Permission:AssetMaintenanceLogs.Delete"));
        amlPermission.AddChild(MyERPPermissions.AssetMaintenanceLogs.Complete, L("Permission:AssetMaintenanceLogs.Complete"));
        amlPermission.AddChild(MyERPPermissions.AssetMaintenanceLogs.Cancel, L("Permission:AssetMaintenanceLogs.Cancel"));

        var qgPermission = myGroup.AddPermission(MyERPPermissions.QualityGoals.Default, L("Permission:QualityGoals"));
        qgPermission.AddChild(MyERPPermissions.QualityGoals.Create, L("Permission:QualityGoals.Create"));
        qgPermission.AddChild(MyERPPermissions.QualityGoals.Edit, L("Permission:QualityGoals.Edit"));
        qgPermission.AddChild(MyERPPermissions.QualityGoals.Delete, L("Permission:QualityGoals.Delete"));

        var qrPermission = myGroup.AddPermission(MyERPPermissions.QualityReviews.Default, L("Permission:QualityReviews"));
        qrPermission.AddChild(MyERPPermissions.QualityReviews.Create, L("Permission:QualityReviews.Create"));
        qrPermission.AddChild(MyERPPermissions.QualityReviews.Edit, L("Permission:QualityReviews.Edit"));
        qrPermission.AddChild(MyERPPermissions.QualityReviews.Delete, L("Permission:QualityReviews.Delete"));

        var qpPermission = myGroup.AddPermission(MyERPPermissions.QualityProcedures.Default, L("Permission:QualityProcedures"));
        qpPermission.AddChild(MyERPPermissions.QualityProcedures.Create, L("Permission:QualityProcedures.Create"));
        qpPermission.AddChild(MyERPPermissions.QualityProcedures.Edit, L("Permission:QualityProcedures.Edit"));
        qpPermission.AddChild(MyERPPermissions.QualityProcedures.Delete, L("Permission:QualityProcedures.Delete"));

        var qaPermission = myGroup.AddPermission(MyERPPermissions.QualityActions.Default, L("Permission:QualityActions"));
        qaPermission.AddChild(MyERPPermissions.QualityActions.Create, L("Permission:QualityActions.Create"));
        qaPermission.AddChild(MyERPPermissions.QualityActions.Edit, L("Permission:QualityActions.Edit"));
        qaPermission.AddChild(MyERPPermissions.QualityActions.Delete, L("Permission:QualityActions.Delete"));

        var ncPermission = myGroup.AddPermission(MyERPPermissions.NonConformances.Default, L("Permission:NonConformances"));
        ncPermission.AddChild(MyERPPermissions.NonConformances.Create, L("Permission:NonConformances.Create"));
        ncPermission.AddChild(MyERPPermissions.NonConformances.Edit, L("Permission:NonConformances.Edit"));
        ncPermission.AddChild(MyERPPermissions.NonConformances.Delete, L("Permission:NonConformances.Delete"));

        var qmPermission = myGroup.AddPermission(MyERPPermissions.QualityMeetings.Default, L("Permission:QualityMeetings"));
        qmPermission.AddChild(MyERPPermissions.QualityMeetings.Create, L("Permission:QualityMeetings.Create"));
        qmPermission.AddChild(MyERPPermissions.QualityMeetings.Edit, L("Permission:QualityMeetings.Edit"));
        qmPermission.AddChild(MyERPPermissions.QualityMeetings.Delete, L("Permission:QualityMeetings.Delete"));

        var qfPermission = myGroup.AddPermission(MyERPPermissions.QualityFeedbacks.Default, L("Permission:QualityFeedbacks"));
        qfPermission.AddChild(MyERPPermissions.QualityFeedbacks.Create, L("Permission:QualityFeedbacks.Create"));
        qfPermission.AddChild(MyERPPermissions.QualityFeedbacks.Edit, L("Permission:QualityFeedbacks.Edit"));
        qfPermission.AddChild(MyERPPermissions.QualityFeedbacks.Delete, L("Permission:QualityFeedbacks.Delete"));

        var acPermission = myGroup.AddPermission(MyERPPermissions.AssetCategories.Default, L("Permission:AssetCategories"));
        acPermission.AddChild(MyERPPermissions.AssetCategories.Create, L("Permission:AssetCategories.Create"));
        acPermission.AddChild(MyERPPermissions.AssetCategories.Edit, L("Permission:AssetCategories.Edit"));
        acPermission.AddChild(MyERPPermissions.AssetCategories.Delete, L("Permission:AssetCategories.Delete"));

        var locPermission = myGroup.AddPermission(MyERPPermissions.Locations.Default, L("Permission:Locations"));
        locPermission.AddChild(MyERPPermissions.Locations.Create, L("Permission:Locations.Create"));
        locPermission.AddChild(MyERPPermissions.Locations.Edit, L("Permission:Locations.Edit"));
        locPermission.AddChild(MyERPPermissions.Locations.Delete, L("Permission:Locations.Delete"));

        var vehiclePermission = myGroup.AddPermission(MyERPPermissions.Vehicles.Default, L("Permission:Vehicles"));
        vehiclePermission.AddChild(MyERPPermissions.Vehicles.Create, L("Permission:Vehicles.Create"));
        vehiclePermission.AddChild(MyERPPermissions.Vehicles.Edit, L("Permission:Vehicles.Edit"));
        vehiclePermission.AddChild(MyERPPermissions.Vehicles.Delete, L("Permission:Vehicles.Delete"));

        var driverPermission = myGroup.AddPermission(MyERPPermissions.Drivers.Default, L("Permission:Drivers"));
        driverPermission.AddChild(MyERPPermissions.Drivers.Create, L("Permission:Drivers.Create"));
        driverPermission.AddChild(MyERPPermissions.Drivers.Edit, L("Permission:Drivers.Edit"));
        driverPermission.AddChild(MyERPPermissions.Drivers.Delete, L("Permission:Drivers.Delete"));

        var licenseCategoryPermission = myGroup.AddPermission(MyERPPermissions.DrivingLicenseCategories.Default, L("Permission:DrivingLicenseCategories"));
        licenseCategoryPermission.AddChild(MyERPPermissions.DrivingLicenseCategories.Create, L("Permission:DrivingLicenseCategories.Create"));
        licenseCategoryPermission.AddChild(MyERPPermissions.DrivingLicenseCategories.Edit, L("Permission:DrivingLicenseCategories.Edit"));
        licenseCategoryPermission.AddChild(MyERPPermissions.DrivingLicenseCategories.Delete, L("Permission:DrivingLicenseCategories.Delete"));

        var movPermission = myGroup.AddPermission(MyERPPermissions.AssetMovements.Default, L("Permission:AssetMovements"));
        movPermission.AddChild(MyERPPermissions.AssetMovements.Create, L("Permission:AssetMovements.Create"));
        movPermission.AddChild(MyERPPermissions.AssetMovements.Edit, L("Permission:AssetMovements.Edit"));
        movPermission.AddChild(MyERPPermissions.AssetMovements.Delete, L("Permission:AssetMovements.Delete"));

        var arPermission = myGroup.AddPermission(MyERPPermissions.AssetRepairs.Default, L("Permission:AssetRepairs"));
        arPermission.AddChild(MyERPPermissions.AssetRepairs.Create, L("Permission:AssetRepairs.Create"));
        arPermission.AddChild(MyERPPermissions.AssetRepairs.Edit, L("Permission:AssetRepairs.Edit"));
        arPermission.AddChild(MyERPPermissions.AssetRepairs.Delete, L("Permission:AssetRepairs.Delete"));

        var capPermission = myGroup.AddPermission(MyERPPermissions.AssetCapitalizations.Default, L("Permission:AssetCapitalizations"));
        capPermission.AddChild(MyERPPermissions.AssetCapitalizations.Create, L("Permission:AssetCapitalizations.Create"));
        capPermission.AddChild(MyERPPermissions.AssetCapitalizations.Edit, L("Permission:AssetCapitalizations.Edit"));
        capPermission.AddChild(MyERPPermissions.AssetCapitalizations.Delete, L("Permission:AssetCapitalizations.Delete"));

        var avaPermission = myGroup.AddPermission(MyERPPermissions.AssetValueAdjustments.Default, L("Permission:AssetValueAdjustments"));
        avaPermission.AddChild(MyERPPermissions.AssetValueAdjustments.Create, L("Permission:AssetValueAdjustments.Create"));
        avaPermission.AddChild(MyERPPermissions.AssetValueAdjustments.Edit, L("Permission:AssetValueAdjustments.Edit"));
        avaPermission.AddChild(MyERPPermissions.AssetValueAdjustments.Delete, L("Permission:AssetValueAdjustments.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<MyERPResource>(name);
    }
}
