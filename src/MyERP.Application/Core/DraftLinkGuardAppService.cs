using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

/// <summary>
/// Exposes draft link guard functionality via API.
/// Per ERPNext PR #57299: warns users when a draft follow-up document already exists
/// (e.g., a draft DN exists for this SO) to prevent accidental duplicates.
/// 
/// This is an ADVISORY service — it does NOT block conversion.
/// The frontend shows a warning dialog and lets the user decide: edit existing or create new.
/// </summary>
[Authorize]
public class DraftLinkGuardAppService : ApplicationService, IDraftLinkGuardAppService
{
    private readonly DraftLinkGuardService _guardService;

    public DraftLinkGuardAppService(DraftLinkGuardService guardService)
    {
        _guardService = guardService;
    }

    public async Task<List<DraftLinkDto>> GetExistingDraftsAsync(
        string sourceDocType, Guid sourceId, string targetDocType)
    {
        var drafts = await _guardService.GetExistingDraftsAsync(sourceDocType, sourceId, targetDocType);

        return drafts.Select(d => new DraftLinkDto
        {
            DocumentId = d.DocumentId,
            DocumentNumber = d.DocumentNumber,
            DocumentType = d.DocumentType,
            Url = ResolveDocumentUrl(d.DocumentType, d.DocumentId)
        }).ToList();
    }

    private static string? ResolveDocumentUrl(string docType, Guid docId)
    {
        return docType switch
        {
            "DeliveryNote" => $"/sales/delivery-notes/{docId}",
            "SalesInvoice" => $"/sales/invoices/{docId}",
            "PurchaseReceipt" => $"/purchasing/receipts/{docId}",
            "PurchaseInvoice" => $"/purchasing/invoices/{docId}",
            "StockEntry" => $"/inventory/stock-entries/{docId}",
            _ => null
        };
    }
}
