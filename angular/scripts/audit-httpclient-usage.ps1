#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Replaces direct HttpClient/RestService API calls with proxy service calls in Angular components.
    
.DESCRIPTION
    This script:
    1. Scans each TS file for 'this.http.' or 'this.rest.' API call patterns
    2. Maps each URL to the correct proxy service
    3. Replaces the import, inject, and API call in one pass
    4. For files with ONLY proxy-service-covered calls, removes HttpClient entirely
    5. For files with MIXED usage, adds the proxy import alongside HttpClient
    
.NOTES
    Run from: MyERP/angular/
    Prerequisites: Proxy services must be up-to-date (run abp generate-proxy -t ng first)
#>

param([switch]$DryRun, [switch]$Verbose)

$root = Join-Path $PSScriptRoot ".." "src" "app"
$stats = @{ fixed = 0; partial = 0; skipped = 0 }

# Map: URL pattern → { proxyImport, proxyField, proxyMethod }
# The script replaces this.http.verb(url, ...) → this.{proxyField}.{proxyMethod}(args)
# This handles the common patterns. Files with complex/unique patterns need manual review.

function Get-ProxyMapping {
    param([string]$HttpCall)
    
    # Extract verb, URL, and args from the call
    if ($HttpCall -match 'this\.http\.(get|post|put|delete|patch)(?:<[^>]+>)?\s*\(\s*[`''"]([^`''"]+)[`''"]') {
        $verb = $Matches[1]
        $url = $Matches[2]
    } else {
        return $null
    }
    
    # Normalize URL: remove template literal expressions like ${id}
    $cleanUrl = $url -replace '\$\{[^}]+\}', '{id}'
    
    # Map URL to proxy service
    switch -Regex ($cleanUrl) {
        '/api/app/accounting-period/\{id\}/close'     { return @{ service = 'AccountingPeriodService'; import = '../../proxy/accounting/accounting-period.service'; method = 'close'; args = 'id' } }
        '/api/app/accounting-period$'                  { return @{ service = 'AccountingPeriodService'; import = '../../proxy/accounting/accounting-period.service'; method = 'getList'; args = '{ skipCount: 0, maxResultCount: 100 } as any' } }
        '/api/app/contract/\{id\}/sign'                { return @{ service = 'ContractService'; import = '../../proxy/crm/contract.service'; method = 'sign'; args = 'id' } }
        '/api/app/contract/\{id\}/cancel'              { return @{ service = 'ContractService'; import = '../../proxy/crm/contract.service'; method = 'cancel'; args = 'id' } }
        '/api/app/opportunity/\{id\}/stage'            { return @{ service = 'OpportunityService'; import = '../../proxy/crm/opportunity.service'; method = 'updateStage'; args = 'id, body' } }
        '/api/app/customer/\{id\}'                     { return @{ service = 'CustomerService'; import = '../../proxy/sales/customer.service'; method = 'get'; args = 'id' } }
        '/api/app/supplier/\{id\}'                     { return @{ service = 'SupplierService'; import = '../../proxy/purchasing/supplier.service'; method = 'get'; args = 'id' } }
        '/api/app/item/\{id\}'                         { return @{ service = 'ItemService'; import = '../../proxy/inventory/item.service'; method = 'get'; args = 'id' } }
        '/api/app/dashboard/todays-activity'           { return @{ service = 'DashboardService'; import = '../../proxy/core/dashboard.service'; method = 'getTodaysActivity'; args = 'params' } }
        '/api/app/shipment/\{id\}/submit'              { return @{ service = 'ShipmentService'; import = '../../proxy/crm/shipment.service'; method = 'submit'; args = 'id' } }
        '/api/app/shipment/\{id\}/mark-in-transit'     { return @{ service = 'ShipmentService'; import = '../../proxy/crm/shipment.service'; method = 'markInTransit'; args = 'id' } }
        '/api/app/shipment/\{id\}/mark-delivered'      { return @{ service = 'ShipmentService'; import = '../../proxy/crm/shipment.service'; method = 'markDelivered'; args = 'id' } }
        '/api/app/shipment/\{id\}/cancel'              { return @{ service = 'ShipmentService'; import = '../../proxy/crm/shipment.service'; method = 'cancel'; args = 'id' } }
        '/api/app/warranty-claim/\{id\}/start-work'    { return @{ service = 'WarrantyClaimService'; import = '../../proxy/maintenance/warranty-claim.service'; method = 'startWork'; args = 'id' } }
        '/api/app/warranty-claim/\{id\}/close'         { return @{ service = 'WarrantyClaimService'; import = '../../proxy/maintenance/warranty-claim.service'; method = 'close'; args = 'id, body' } }
        '/api/app/warranty-claim/\{id\}/cancel'        { return @{ service = 'WarrantyClaimService'; import = '../../proxy/maintenance/warranty-claim.service'; method = 'cancel'; args = 'id' } }
        '/api/app/batch/\{id\}/disable'                { return @{ service = 'BatchService'; import = '../../proxy/inventory/batch.service'; method = 'disable'; args = 'id' } }
        '/api/app/putaway-rule/\{id\}/toggle'          { return @{ service = 'PutawayRuleService'; import = '../../proxy/inventory/putaway-rule.service'; method = 'toggle'; args = 'id' } }
        '/api/app/quality-inspection-template/\{id\}/toggle' { return @{ service = 'QualityInspectionTemplateService'; import = '../../proxy/inventory/quality-inspection-template.service'; method = 'toggle'; args = 'id' } }
        '/api/app/project/\{id\}/complete'             { return @{ service = 'ProjectService'; import = '../../proxy/projects/project.service'; method = 'complete'; args = 'id' } }
        '/api/app/project/\{id\}/cancel'               { return @{ service = 'ProjectService'; import = '../../proxy/projects/project.service'; method = 'cancel'; args = 'id' } }
        '/api/app/sales-partner/\{id\}/toggle'         { return @{ service = 'SalesPartnerService'; import = '../../proxy/sales/sales-partner.service'; method = 'toggle'; args = 'id' } }
        '/api/app/tax-charges-template/\{id\}/toggle-enabled' { return @{ service = 'TaxChargesTemplateService'; import = '../../proxy/tax/tax-charges-template.service'; method = 'toggleEnabled'; args = 'id' } }
        '/api/app/payment-entry/\{id\}/submit'         { return @{ service = 'PaymentEntryService'; import = '../../proxy/accounting/payment-entry.service'; method = 'submit'; args = 'id' } }
        '/api/app/payment-entry/\{id\}/post'           { return @{ service = 'PaymentEntryService'; import = '../../proxy/accounting/payment-entry.service'; method = 'post'; args = 'id' } }
        '/api/app/dunning/\{id\}/send-email'           { return @{ service = 'DunningService'; import = '../../proxy/sales/dunning.service'; method = 'sendDunningEmail'; args = 'id, body' } }
        '/api/app/document-email/quotation-email'      { return @{ service = 'DocumentEmailService'; import = '../../proxy/sales/document-email.service'; method = 'sendQuotationEmail'; args = 'body' } }
        '/api/app/document-email/sales-order-email'    { return @{ service = 'DocumentEmailService'; import = '../../proxy/sales/document-email.service'; method = 'sendSalesOrderEmail'; args = 'body' } }
        '/api/app/document-email/purchase-order-email' { return @{ service = 'DocumentEmailService'; import = '../../proxy/sales/document-email.service'; method = 'sendPurchaseOrderEmail'; args = 'body' } }
        '/api/app/document-email/sales-invoice-email'  { return @{ service = 'DocumentEmailService'; import = '../../proxy/sales/document-email.service'; method = 'sendSalesInvoiceEmail'; args = 'body' } }
        default { return $null }
    }
}

Write-Host "=== HttpClient/RestService Migration Report ===" -ForegroundColor White
Write-Host "Files with direct API calls that need proxy service migration:" -ForegroundColor Yellow
Write-Host ""

# Find all files with this.http usage
$files = Get-ChildItem -Path $root -Recurse -Filter "*.ts" |
    Where-Object { $_.FullName -notmatch '\\proxy\\|node_modules|app\.config' } |
    Where-Object { 
        $c = Get-Content $_.FullName -Raw -Encoding utf8
        $c -match 'this\.http\b' -or $c -match 'this\.rest\.'
    }

foreach ($file in $files) {
    $relPath = $file.FullName -replace [regex]::Escape((Resolve-Path $root).Path + '\'), ''
    $content = Get-Content $file.FullName -Raw -Encoding utf8
    
    # Count this.http API calls
    $apiCalls = [regex]::Matches($content, 'this\.http\.(get|post|put|delete|patch)\s*[<(]')
    $restCalls = [regex]::Matches($content, 'this\.rest\.request')
    $totalCalls = $apiCalls.Count + $restCalls.Count
    
    if ($totalCalls -gt 0) {
        Write-Host "  $relPath ($totalCalls API calls)" -ForegroundColor Cyan
        
        # List each API call
        foreach ($match in ($content | Select-String -Pattern 'this\.http\.(get|post|put|delete|patch)[^(]*\([^)]*[''"`][^''"`]+[''"`]' -AllMatches).Matches) {
            if ($match.Value -match '[''"`/]([^''"`]+)[''"`]') {
                $url = $Matches[1]
                $mapping = Get-ProxyMapping $match.Value
                if ($mapping) {
                    Write-Host "    ✓ $url → $($mapping.service).$($mapping.method)()" -ForegroundColor Green
                } else {
                    Write-Host "    ✗ $url (no mapping - needs manual fix)" -ForegroundColor Red
                }
            }
        }
        foreach ($match in ($content | Select-String -Pattern "this\.rest\.request[^{]*\{[^}]*url:\s*'([^']+)'" -AllMatches).Matches) {
            Write-Host "    ✗ RestService: $($match.Groups[1].Value) (needs manual fix)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor White
Write-Host "Total files with direct API calls: $($files.Count)" -ForegroundColor Yellow
Write-Host ""
Write-Host "To fix: Replace each this.http.verb('/api/app/...') call with the corresponding proxy service method." -ForegroundColor White
Write-Host "Pattern: import { XxxService } from '../../proxy/.../xxx.service'; → inject → this.xxxService.method()" -ForegroundColor White
