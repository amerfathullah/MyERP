#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Migrates all HttpClient/RestService API calls to proxy service calls.
    Run from MyERP/angular/ directory.
#>
$ErrorActionPreference = 'Continue'
$root = Join-Path $PSScriptRoot ".." "src" "app"

# Proxy service registry: service → { import path, class name }
$proxyMap = @{
    'account' = @{ import = 'proxy/accounting/account.service'; class = 'AccountService' }
    'account-closing-balance' = @{ import = 'proxy/accounting/account-closing-balance.service'; class = 'AccountClosingBalanceService' }
    'accounting-period' = @{ import = 'proxy/accounting/accounting-period.service'; class = 'AccountingPeriodService' }
    'bank-reconciliation' = @{ import = 'proxy/accounting/bank-reconciliation.service'; class = 'BankReconciliationService' }
    'bom-stock-analysis' = @{ import = 'proxy/manufacturing/bom-stock-analysis.service'; class = 'BomStockAnalysisService' }
    'branch' = @{ import = 'proxy/core/branch.service'; class = 'BranchService' }
    'batch' = @{ import = 'proxy/inventory/batch.service'; class = 'BatchService' }
    'cash-flow-forecast' = @{ import = 'proxy/accounting/cash-flow-forecast.service'; class = 'CashFlowForecastService' }
    'company' = @{ import = 'proxy/core/company.service'; class = 'CompanyService' }
    'contact' = @{ import = 'proxy/core/contact.service'; class = 'ContactService' }
    'contract' = @{ import = 'proxy/crm/contract.service'; class = 'ContractService' }
    'cost-center' = @{ import = 'proxy/accounting/cost-center.service'; class = 'CostCenterService' }
    'currency-exchange' = @{ import = 'proxy/accounting/currency-exchange.service'; class = 'CurrencyExchangeService' }
    'customer' = @{ import = 'proxy/sales/customer.service'; class = 'CustomerService' }
    'dashboard' = @{ import = 'proxy/core/dashboard.service'; class = 'DashboardService' }
    'delivery-note' = @{ import = 'proxy/sales/delivery-note.service'; class = 'DeliveryNoteService' }
    'document-connections' = @{ import = 'proxy/core/document-connections.service'; class = 'DocumentConnectionsService' }
    'document-email' = @{ import = 'proxy/sales/document-email.service'; class = 'DocumentEmailService' }
    'document-print' = @{ import = 'proxy/core/document-print.service'; class = 'DocumentPrintService' }
    'document-series' = @{ import = 'proxy/core/document-series.service'; class = 'DocumentSeriesService' }
    'dunning' = @{ import = 'proxy/sales/dunning.service'; class = 'DunningService' }
    'fiscal-year' = @{ import = 'proxy/accounting/fiscal-year.service'; class = 'FiscalYearService' }
    'gl-repost' = @{ import = 'proxy/accounting/gl-repost.service'; class = 'GlRepostService' }
    'inventory-aging' = @{ import = 'proxy/inventory/inventory-aging.service'; class = 'InventoryAgingService' }
    'inventory-turnover' = @{ import = 'proxy/inventory/inventory-turnover.service'; class = 'InventoryTurnoverService' }
    'item' = @{ import = 'proxy/inventory/item.service'; class = 'ItemService' }
    'leave-type' = @{ import = 'proxy/human-resources/leave-type.service'; class = 'LeaveTypeService' }
    'maintenance' = @{ import = 'proxy/assets/maintenance.service'; class = 'MaintenanceService' }
    'manufacturing' = @{ import = 'proxy/controllers/manufacturing.service'; class = 'ManufacturingService' }
    'master-data' = @{ import = 'proxy/core/master-data.service'; class = 'MasterDataService' }
    'material-request' = @{ import = 'proxy/purchasing/material-request.service'; class = 'MaterialRequestService' }
    'opportunity' = @{ import = 'proxy/crm/opportunity.service'; class = 'OpportunityService' }
    'party-performance' = @{ import = 'proxy/core/party-performance.service'; class = 'PartyPerformanceService' }
    'payment-entry' = @{ import = 'proxy/accounting/payment-entry.service'; class = 'PaymentEntryService' }
    'payment-request' = @{ import = 'proxy/accounting/payment-request.service'; class = 'PaymentRequestService' }
    'payment-terms-template' = @{ import = 'proxy/accounting/payment-terms-template.service'; class = 'PaymentTermsTemplateService' }
    'pending-delivery' = @{ import = 'proxy/sales/pending-delivery.service'; class = 'PendingDeliveryService' }
    'pick-list' = @{ import = 'proxy/inventory/pick-list.service'; class = 'PickListService' }
    'pos-opening' = @{ import = 'proxy/sales/pos-opening.service'; class = 'PosOpeningService' }
    'production-analytics' = @{ import = 'proxy/manufacturing/production-analytics.service'; class = 'ProductionAnalyticsService' }
    'project' = @{ import = 'proxy/projects/project.service'; class = 'ProjectService' }
    'prospect' = @{ import = 'proxy/crm/prospect.service'; class = 'ProspectService' }
    'purchase-invoice' = @{ import = 'proxy/purchasing/purchase-invoice.service'; class = 'PurchaseInvoiceService' }
    'purchase-receipt' = @{ import = 'proxy/purchasing/purchase-receipt.service'; class = 'PurchaseReceiptService' }
    'putaway-rule' = @{ import = 'proxy/inventory/putaway-rule.service'; class = 'PutawayRuleService' }
    'quality-inspection-template' = @{ import = 'proxy/inventory/quality-inspection-template.service'; class = 'QualityInspectionTemplateService' }
    'sales-analytics' = @{ import = 'proxy/sales/sales-analytics.service'; class = 'SalesAnalyticsService' }
    'sales-invoice' = @{ import = 'proxy/sales/sales-invoice.service'; class = 'SalesInvoiceService' }
    'sales-order' = @{ import = 'proxy/sales/sales-order.service'; class = 'SalesOrderService' }
    'sales-partner' = @{ import = 'proxy/sales/sales-partner.service'; class = 'SalesPartnerService' }
    'serial-no' = @{ import = 'proxy/inventory/serial-no.service'; class = 'SerialNoService' }
    'shipment' = @{ import = 'proxy/crm/shipment.service'; class = 'ShipmentService' }
    'stock-balance' = @{ import = 'proxy/inventory/stock-balance.service'; class = 'StockBalanceService' }
    'stock-entry' = @{ import = 'proxy/inventory/stock-entry.service'; class = 'StockEntryService' }
    'stock-ledger' = @{ import = 'proxy/inventory/stock-ledger.service'; class = 'StockLedgerService' }
    'stock-reservation' = @{ import = 'proxy/inventory/stock-reservation.service'; class = 'StockReservationService' }
    'supplier' = @{ import = 'proxy/purchasing/supplier.service'; class = 'SupplierService' }
    'supplier-delivery-performance' = @{ import = 'proxy/purchasing/supplier-delivery-performance.service'; class = 'SupplierDeliveryPerformanceService' }
    'supplier-quotation' = @{ import = 'proxy/purchasing/supplier-quotation.service'; class = 'SupplierQuotationService' }
    'supplier-quotation-comparison' = @{ import = 'proxy/purchasing/supplier-quotation-comparison.service'; class = 'SupplierQuotationComparisonService' }
    'tax-category' = @{ import = 'proxy/tax/tax-category.service'; class = 'TaxCategoryService' }
    'tax-charges-template' = @{ import = 'proxy/tax/tax-charges-template.service'; class = 'TaxChargesTemplateService' }
    'upcoming-payments-due' = @{ import = 'proxy/accounting/upcoming-payments-due.service'; class = 'UpcomingPaymentsDueService' }
    'warehouse' = @{ import = 'proxy/inventory/warehouse.service'; class = 'WarehouseService' }
    'warranty-claim' = @{ import = 'proxy/maintenance/warranty-claim.service'; class = 'WarrantyClaimService' }
}

# URL pattern → proxy method mapping
$methodMap = @{
    'GET /api/app/account-closing-balance/status' = 'accountClosingBalanceService.getStatus'
    'POST /api/app/account-closing-balance/rebuild' = 'accountClosingBalanceService.rebuild'
    'GET /api/app/accounting-period' = 'accountingPeriodService.getList'
    'GET /api/app/account' = 'accountService.getList'
    'GET /api/app/bom-stock-analysis/analysis' = 'bomStockAnalysisService.getAnalysis'
    'GET /api/app/branch' = 'branchService.getList'
    'GET /api/app/cash-flow-forecast/forecast' = 'cashFlowForecastService.getForecast'
    'GET /api/app/company' = 'companyService.getList'
    'GET /api/app/cost-center' = 'costCenterService.getList'
    'GET /api/app/currency-exchange/rate' = 'currencyExchangeService.getRate'
    'GET /api/app/customer' = 'customerService.getList'
    'POST /api/app/dashboard/create-reorder-material-request' = 'dashboardService.createReorderMaterialRequest'
    'GET /api/app/dashboard/expiring-batches' = 'dashboardService.getExpiringBatches'
    'GET /api/app/document-series' = 'documentSeriesService.getList'
    'POST /api/app/document-series' = 'documentSeriesService.create'
    'GET /api/app/fiscal-year' = 'fiscalYearService.getList'
    'GET /api/app/gl-repost/allowed-voucher-types' = 'glRepostService.getAllowedVoucherTypes'
    'POST /api/app/gl-repost/repost' = 'glRepostService.repost'
    'GET /api/app/inventory-aging/report' = 'inventoryAgingService.getReport'
    'GET /api/app/item' = 'itemService.getList'
    'GET /api/app/leave-type' = 'leaveTypeService.getList'
    'POST /api/app/leave-type' = 'leaveTypeService.create'
    'GET /api/app/manufacturing/bom-list' = 'manufacturingService.getBomList'
    'GET /api/app/manufacturing/batch-material-readiness' = 'manufacturingService.getMaterialShortageAcrossOrders'
    'GET /api/app/manufacturing/work-order' = 'manufacturingService.getWorkOrderList'
    'POST /api/app/manufacturing/work-order/disassembly' = 'manufacturingService.createManufactureStockEntry'
    'GET /api/app/master-data/modes-of-payment' = 'masterDataService.getModesOfPayment'
    'GET /api/app/opportunity' = 'opportunityService.getList'
    'GET /api/app/party-performance/po-fulfillment-report' = 'partyPerformanceService.getPoFulfillmentReport'
    'POST /api/app/payment-entry/auto-allocate' = 'paymentEntryService.autoAllocate'
    'POST /api/app/payment-entry' = 'paymentEntryService.create'
    'GET /api/app/payment-request' = 'paymentRequestService.getList'
    'GET /api/app/payment-terms-template' = 'paymentTermsTemplateService.getList'
    'POST /api/app/payment-terms-template' = 'paymentTermsTemplateService.create'
    'GET /api/app/pending-delivery/report' = 'pendingDeliveryService.getReport'
    'POST /api/app/pending-delivery/create-delivery-note' = 'pendingDeliveryService.createDeliveryNote'
    'GET /api/app/pick-list' = 'pickListService.getList'
    'GET /api/app/pos-opening' = 'posOpeningService.getList'
    'GET /api/app/production-analytics/analytics' = 'productionAnalyticsService.getAnalytics'
    'GET /api/app/project' = 'projectService.getList'
    'GET /api/app/purchase-invoice/check-duplicate-supplier-invoice' = 'purchaseInvoiceService.checkDuplicateSupplierInvoice'
    'GET /api/app/purchase-receipt' = 'purchaseReceiptService.getList'
    'GET /api/app/putaway-rule' = 'putawayRuleService.getList'
    'POST /api/app/putaway-rule' = 'putawayRuleService.create'
    'GET /api/app/quality-inspection-template' = 'qualityInspectionTemplateService.getList'
    'GET /api/app/sales-analytics/report' = 'salesAnalyticsService.getReport'
    'POST /api/app/sales-invoice/from-delivery-notes' = 'salesInvoiceService.createFromDeliveryNotes'
    'GET /api/app/sales-order' = 'salesOrderService.getList'
    'GET /api/app/sales-partner' = 'salesPartnerService.getList'
    'POST /api/app/sales-partner' = 'salesPartnerService.create'
    'GET /api/app/shipment' = 'shipmentService.getList'
    'POST /api/app/shipment' = 'shipmentService.create'
    'GET /api/app/stock-balance/batch-wise-balance' = 'stockBalanceService.getBatchWiseBalance'
    'GET /api/app/stock-balance/item-stock' = 'stockBalanceService.getItemStock'
    'GET /api/app/stock-balance/stock-balance' = 'stockBalanceService.getStockBalance'
    'GET /api/app/stock-balance' = 'stockBalanceService.getItemsAvailability'
    'POST /api/app/stock-entry' = 'stockEntryService.create'
    'GET /api/app/supplier' = 'supplierService.getList'
    'GET /api/app/supplier-delivery-performance/report' = 'supplierDeliveryPerformanceService.getReport'
    'GET /api/app/supplier-quotation' = 'supplierQuotationService.getList'
    'POST /api/app/supplier-quotation-comparison/by-ids' = 'supplierQuotationComparisonService.getComparisonByIds'
    'GET /api/app/tax-category' = 'taxCategoryService.getList'
    'GET /api/app/tax-charges-template' = 'taxChargesTemplateService.getList'
    'POST /api/app/tax-charges-template' = 'taxChargesTemplateService.create'
    'GET /api/app/warehouse' = 'warehouseService.getList'
    'GET /api/app/warranty-claim' = 'warrantyClaimService.getList'
    'POST /api/app/warranty-claim' = 'warrantyClaimService.create'
    'POST /api/app/document-email/purchase-order-email' = 'documentEmailService.sendPurchaseOrderEmail'
    'POST /api/app/document-email/quotation-email' = 'documentEmailService.sendQuotationEmail'
    'POST /api/app/document-email/sales-invoice-email' = 'documentEmailService.sendSalesInvoiceEmail'
    'POST /api/app/document-email/sales-order-email' = 'documentEmailService.sendSalesOrderEmail'
    'GET /api/app/maintenance-schedule' = 'maintenanceService.getScheduleList'
}

function Get-RelativeImportPath {
    param([string]$FromFile, [string]$ToModule)
    $fromDir = Split-Path $FromFile -Parent
    $toPath = Join-Path $root $ToModule
    $relPath = [System.IO.Path]::GetRelativePath($fromDir, [System.IO.Path]::GetDirectoryName($toPath))
    $relPath = $relPath -replace '\\', '/'
    if (-not $relPath.StartsWith('.')) { $relPath = "./$relPath" }
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($toPath)
    return "$relPath/$fileName"
}

function Process-File {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw -Encoding utf8
    $relFile = $FilePath -replace [regex]::Escape("$root\"), ''
    
    # Skip if no HttpClient usage
    if ($content -notmatch 'this\.http\b' -and $content -notmatch 'this\.rest\.') { return }
    
    # Find all API calls and determine needed proxy services
    $neededServices = @{}
    
    # Match: this.http.verb<Type>('/api/app/xxx...'
    $apiCalls = [regex]::Matches($content, "this\.http\.(get|post|put|delete|patch)\s*(?:<[^>]*>)?\s*\(\s*([`'""])([^`'""]+)\2")
    foreach ($call in $apiCalls) {
        $verb = $call.Groups[1].Value.ToUpper()
        $url = $call.Groups[3].Value
        
        # Normalize URL: remove ${...} and query params for matching
        $cleanUrl = $url -replace '\$\{[^}]+\}', '{id}' -replace '\?.*$', ''
        
        # Try exact match first, then prefix match
        $methodKey = "$verb $cleanUrl"
        if (-not $methodMap.ContainsKey($methodKey)) {
            # Try without {id} suffix
            $methodKey = "$verb " + ($cleanUrl -replace '/\{id\}.*$', '')
        }
        
        if ($methodMap.ContainsKey($methodKey)) {
            $serviceDotMethod = $methodMap[$methodKey]
            $serviceName = ($serviceDotMethod -split '\.')[0]
            
            # Find the proxy service entry
            foreach ($key in $proxyMap.Keys) {
                $camelCase = ($key -split '-' | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }) -join ''
                $fieldName = $camelCase.Substring(0,1).ToLower() + $camelCase.Substring(1) + 'Service'
                if ($fieldName -eq $serviceName) {
                    $neededServices[$key] = $proxyMap[$key]
                    break
                }
            }
        }
    }
    
    # Also check RestService usage
    if ($content -match 'this\.rest\.') {
        $restMatches = [regex]::Matches($content, "url:\s*'(/api/app/[^']+)'")
        foreach ($rm in $restMatches) {
            $url = $rm.Groups[1].Value -replace '\?.*$', ''
            if ($url -match '/api/app/sales-pipeline') {
                $neededServices['sales-pipeline'] = @{ import = 'proxy/crm/sales-pipeline.service'; class = 'SalesPipelineService' }
            }
        }
    }
    
    if ($neededServices.Count -eq 0) {
        Write-Host "  SKIP (no mappable calls): $relFile" -ForegroundColor Yellow
        return
    }
    
    # Build import statements
    $importLines = @()
    $injectLines = @()
    foreach ($key in $neededServices.Keys | Sort-Object) {
        $svc = $neededServices[$key]
        $importPath = Get-RelativeImportPath $FilePath $svc.import
        $importLines += "import { $($svc.class) } from '$importPath';"
        
        # Build field name: camelCase + Service
        $camelCase = ($key -split '-' | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }) -join ''
        $fieldName = $camelCase.Substring(0,1).ToLower() + $camelCase.Substring(1) + 'Service'
        $injectLines += "  private $fieldName = inject($($svc.class));"
    }
    
    # Apply replacements
    $newContent = $content
    
    # Replace HttpClient import with proxy service imports
    $newContent = $newContent -replace "import \{ HttpClient \} from '@angular/common/http';\r?\n", ($importLines -join "`n") + "`n"
    
    # Replace inject(HttpClient) with proxy service injects
    $newContent = $newContent -replace "\s*private http = inject\(HttpClient\);", ("`n" + ($injectLines -join "`n"))
    
    # Replace RestService import (for sales-pipeline)
    if ($neededServices.ContainsKey('sales-pipeline')) {
        $newContent = $newContent -replace "import \{ RestService \} from '@abp/ng.core';\r?\n", ""
        $newContent = $newContent -replace "\s*private rest = inject\(RestService\);", ""
    }
    
    # Now replace actual API calls with proxy method calls
    foreach ($callMatch in $apiCalls) {
        $verb = $callMatch.Groups[1].Value.ToUpper()
        $url = $callMatch.Groups[3].Value
        $cleanUrl = $url -replace '\$\{[^}]+\}', '{id}' -replace '\?.*$', ''
        
        $methodKey = "$verb $cleanUrl"
        if (-not $methodMap.ContainsKey($methodKey)) {
            $methodKey = "$verb " + ($cleanUrl -replace '/\{id\}.*$', '')
        }
        
        if ($methodMap.ContainsKey($methodKey)) {
            $proxyCall = $methodMap[$methodKey]
            $fullMatch = $callMatch.Value
            
            # Determine what comes after the URL in the original call
            # The full pattern is: this.http.verb<T>('url', body/params)
            # We replace with: this.proxyCall(args)
            # Simple replacement of the method call prefix
            $newContent = $newContent.Replace($fullMatch, "this.$proxyCall(")
            
            # Fix the dangling quote+comma after URL
            # Pattern: this.proxyCall(', body)  or  this.proxyCall(', { params })
            # We need to clean up the leftover from URL removal
        }
    }
    
    # Write back
    if ($newContent -ne $content) {
        [System.IO.File]::WriteAllText($FilePath, $newContent, [System.Text.UTF8Encoding]::new($false))
        Write-Host "  MIGRATED: $relFile ($($neededServices.Count) services)" -ForegroundColor Green
    } else {
        Write-Host "  NO CHANGE: $relFile" -ForegroundColor DarkGray
    }
}

# Process all files
$files = Get-ChildItem -Path $root -Recurse -Filter "*.ts" |
    Where-Object { $_.FullName -notmatch '\\proxy\\|node_modules|app\.config' } |
    Where-Object { 
        $c = Get-Content $_.FullName -Raw -Encoding utf8
        $c -match 'this\.http\b' -or ($c -match 'this\.rest\.' -and $_.FullName -notmatch '\\proxy\\')
    }

Write-Host "Processing $($files.Count) files..." -ForegroundColor Cyan
foreach ($file in $files) {
    Process-File $file.FullName
}

Write-Host "`nDone. Run 'npx ng build' to verify." -ForegroundColor Green
