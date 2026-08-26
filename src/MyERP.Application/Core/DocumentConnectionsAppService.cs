using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Inventory.Entities;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Assets.Entities;
using MyERP.Projects.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Returns linked/related documents for a given source document (connections panel).
/// Per ERPNext dashboard.py pattern: shows upstream sources + downstream creations + payments.
/// </summary>
[Authorize]
public class DocumentConnectionsAppService : ApplicationService, IDocumentConnectionsAppService
{
    public async Task<DocumentConnectionsDto> GetConnectionsAsync(string documentType, Guid documentId)
    {
        return documentType switch
        {
            "SalesInvoice" => await GetSalesInvoiceConnectionsAsync(documentId),
            "PurchaseInvoice" => await GetPurchaseInvoiceConnectionsAsync(documentId),
            "SalesOrder" => await GetSalesOrderConnectionsAsync(documentId),
            "PurchaseOrder" => await GetPurchaseOrderConnectionsAsync(documentId),
            "DeliveryNote" => await GetDeliveryNoteConnectionsAsync(documentId),
            "PurchaseReceipt" => await GetPurchaseReceiptConnectionsAsync(documentId),
            "PaymentEntry" => await GetPaymentEntryConnectionsAsync(documentId),
            "StockEntry" => await GetStockEntryConnectionsAsync(documentId),
            "WorkOrder" => await GetWorkOrderConnectionsAsync(documentId),
            "Quotation" => await GetQuotationConnectionsAsync(documentId),
            _ => new DocumentConnectionsDto()
        };
    }

    private async Task<DocumentConnectionsDto> GetSalesInvoiceConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        // Payment group
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.References.Any(r => r.ReferenceType == "SalesInvoice" && r.ReferenceId == id)
                         || pe.AgainstInvoiceId == id)
            .Select(pe => new ConnectionDocumentDto
            {
                Id = pe.Id,
                DocumentNumber = pe.PaymentNumber,
                Status = pe.Status.ToString(),
                Amount = pe.PaidAmount,
                Date = pe.PostingDate,
                Route = "/accounting/payments/" + pe.Id
            }).ToList();

        // Returns
        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var returns = siQuery
            .Where(si => si.ReturnAgainstId == id)
            .Select(si => new ConnectionDocumentDto
            {
                Id = si.Id,
                DocumentNumber = si.InvoiceNumber,
                Status = si.Status.ToString(),
                Amount = si.GrandTotal,
                Date = si.IssueDate,
                Route = "/sales/invoices/" + si.Id
            }).ToList();

        // Source: SO via items
        var si = await siRepo.GetAsync(id);
        var soIds = si.Items.Where(i => i.SalesOrderItemId.HasValue)
            .Select(i => i.SalesOrderItemId!.Value).Distinct().ToList();

        var soConnections = new List<ConnectionDocumentDto>();
        if (soIds.Any())
        {
            var soItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrderItem, Guid>>();
            var soItemQuery = await soItemRepo.GetQueryableAsync();
            var linkedSoIds = soItemQuery.Where(soi => soIds.Contains(soi.Id))
                .Select(soi => soi.SalesOrderId).Distinct().ToList();

            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var soQuery = await soRepo.GetQueryableAsync();
            soConnections = soQuery.Where(so => linkedSoIds.Contains(so.Id))
                .Select(so => new ConnectionDocumentDto
                {
                    Id = so.Id,
                    DocumentNumber = so.OrderNumber,
                    Status = so.Status.ToString(),
                    Amount = so.GrandTotal,
                    Date = so.OrderDate,
                    Route = "/sales/orders/" + so.Id
                }).ToList();
        }

        // Source: DN via items
        var dnIds = si.Items.Where(i => i.DeliveryNoteItemId.HasValue)
            .Select(i => i.DeliveryNoteItemId!.Value).Distinct().ToList();

        var dnConnections = new List<ConnectionDocumentDto>();
        if (dnIds.Any())
        {
            var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnQuery = await dnRepo.GetQueryableAsync();
            // Items link to DN item IDs, we need DN IDs — query DN items to get parent IDs
            var dnItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNoteItem, Guid>>();
            var dnItemQuery = await dnItemRepo.GetQueryableAsync();
            var linkedDnIds = dnItemQuery.Where(dni => dnIds.Contains(dni.Id))
                .Select(dni => dni.DeliveryNoteId).Distinct().ToList();

            dnConnections = dnQuery.Where(dn => linkedDnIds.Contains(dn.Id))
                .Select(dn => new ConnectionDocumentDto
                {
                    Id = dn.Id,
                    DocumentNumber = dn.DeliveryNumber,
                    Status = dn.Status.ToString(),
                    Date = dn.PostingDate,
                    Route = "/sales/delivery-notes/" + dn.Id
                }).ToList();
        }

        // Build groups
        var paymentGroup = new ConnectionGroupDto { Label = "Payment", Items = new() };
        if (payments.Any())
            paymentGroup.Items.Add(new ConnectionItemDto
            {
                DocumentType = "Payment Entry",
                Count = payments.Count,
                Route = "/accounting/payments",
                Documents = payments
            });

        var referenceGroup = new ConnectionGroupDto { Label = "Reference", Items = new() };
        if (soConnections.Any())
            referenceGroup.Items.Add(new ConnectionItemDto
            {
                DocumentType = "Sales Order",
                Count = soConnections.Count,
                Route = "/sales/orders",
                Documents = soConnections
            });
        if (dnConnections.Any())
            referenceGroup.Items.Add(new ConnectionItemDto
            {
                DocumentType = "Delivery Note",
                Count = dnConnections.Count,
                Route = "/sales/delivery-notes",
                Documents = dnConnections
            });

        var returnGroup = new ConnectionGroupDto { Label = "Returns", Items = new() };
        if (returns.Any())
            returnGroup.Items.Add(new ConnectionItemDto
            {
                DocumentType = "Credit Note",
                Count = returns.Count,
                Route = "/sales/invoices",
                Documents = returns
            });

        // Timesheets billed to this SI (via TimesheetDetail rows)
        var tsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Timesheet, Guid>>();
        var tsQuery = await tsRepo.GetQueryableAsync();
        var timesheets = tsQuery.Where(ts => ts.Details.Any(d => d.SalesInvoiceId == id))
            .Select(ts => new ConnectionDocumentDto
            {
                Id = ts.Id, DocumentNumber = ts.StartDate.ToString("d") + " - " + ts.EndDate.ToString("d"),
                Status = ts.Status.ToString(), Date = ts.StartDate, Route = "/projects/timesheets/" + ts.Id
            }).ToList();

        // Dunning notices issued for this SI (via DunningOverduePayment rows)
        var dunRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Dunning, Guid>>();
        var dunQuery = await dunRepo.GetQueryableAsync();
        var dunnings = dunQuery.Where(d => d.OverduePayments.Any(op => op.SalesInvoiceId == id))
            .Select(d => new ConnectionDocumentDto
            {
                Id = d.Id, Status = d.Status.ToString(), Date = d.PostingDate, Route = "/sales/dunnings/" + d.Id
            }).ToList();

        var otherGroup = new ConnectionGroupDto { Label = "Other", Items = new() };
        if (timesheets.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Timesheet", Count = timesheets.Count, Route = "/projects/timesheets", Documents = timesheets });
        if (dunnings.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Dunning", Count = dunnings.Count, Route = "/sales/dunnings", Documents = dunnings });

        if (paymentGroup.Items.Any()) result.Groups.Add(paymentGroup);
        if (referenceGroup.Items.Any()) result.Groups.Add(referenceGroup);
        if (returnGroup.Items.Any()) result.Groups.Add(returnGroup);
        if (otherGroup.Items.Any()) result.Groups.Add(otherGroup);

        return result;
    }

    private async Task<DocumentConnectionsDto> GetPurchaseInvoiceConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.References.Any(r => r.ReferenceType == "PurchaseInvoice" && r.ReferenceId == id)
                         || pe.AgainstInvoiceId == id)
            .Select(pe => new ConnectionDocumentDto
            {
                Id = pe.Id, DocumentNumber = pe.PaymentNumber, Status = pe.Status.ToString(),
                Amount = pe.PaidAmount, Date = pe.PostingDate, Route = "/accounting/payments/" + pe.Id
            }).ToList();

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var returns = piQuery.Where(pi => pi.ReturnAgainstId == id)
            .Select(pi => new ConnectionDocumentDto
            {
                Id = pi.Id, DocumentNumber = pi.InvoiceNumber, Status = pi.Status.ToString(),
                Amount = pi.GrandTotal, Date = pi.IssueDate, Route = "/purchasing/invoices/" + pi.Id
            }).ToList();

        var pi = await piRepo.GetAsync(id);
        var poIds = pi.Items.Where(i => i.PurchaseOrderItemId.HasValue)
            .Select(i => i.PurchaseOrderItemId!.Value).Distinct().ToList();

        var poConnections = new List<ConnectionDocumentDto>();
        if (poIds.Any())
        {
            var poItemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrderItem, Guid>>();
            var poItemQuery = await poItemRepo.GetQueryableAsync();
            var linkedPoIds = poItemQuery.Where(poi => poIds.Contains(poi.Id))
                .Select(poi => poi.PurchaseOrderId).Distinct().ToList();

            var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var poQuery = await poRepo.GetQueryableAsync();
            poConnections = poQuery.Where(po => linkedPoIds.Contains(po.Id))
                .Select(po => new ConnectionDocumentDto
                {
                    Id = po.Id, DocumentNumber = po.OrderNumber, Status = po.Status.ToString(),
                    Amount = po.GrandTotal, Date = po.OrderDate, Route = "/purchasing/orders/" + po.Id
                }).ToList();
        }

        var paymentGroup = new ConnectionGroupDto { Label = "Payment", Items = new() };
        if (payments.Any())
            paymentGroup.Items.Add(new ConnectionItemDto { DocumentType = "Payment Entry", Count = payments.Count, Route = "/accounting/payments", Documents = payments });

        var refGroup = new ConnectionGroupDto { Label = "Reference", Items = new() };
        if (poConnections.Any())
            refGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Order", Count = poConnections.Count, Route = "/purchasing/orders", Documents = poConnections });

        // Linked Purchase Receipts (via PI items referencing PR items)
        var prIds = pi.Items.Where(i => i.PurchaseReceiptItemId.HasValue)
            .Select(i => i.PurchaseReceiptItemId!.Value).Distinct().ToList();
        var prConnections = new List<ConnectionDocumentDto>();
        if (prIds.Any())
        {
            var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var prQuery = await prRepo.GetQueryableAsync();
            prConnections = prQuery
                .Where(pr => pr.Items.Any(i => prIds.Contains(i.Id)))
                .Select(pr => new ConnectionDocumentDto
                {
                    Id = pr.Id, DocumentNumber = pr.ReceiptNumber, Status = pr.Status.ToString(),
                    Date = pr.PostingDate, Route = "/purchasing/receipts/" + pr.Id
                }).ToList();
        }
        if (prConnections.Any())
            refGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Receipt", Count = prConnections.Count, Route = "/purchasing/receipts", Documents = prConnections });

        var returnGroup = new ConnectionGroupDto { Label = "Returns", Items = new() };
        if (returns.Any())
            returnGroup.Items.Add(new ConnectionItemDto { DocumentType = "Debit Note", Count = returns.Count, Route = "/purchasing/invoices", Documents = returns });

        // Assets capitalized from this PI
        var assetRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Asset, Guid>>();
        var assetQuery = await assetRepo.GetQueryableAsync();
        var assets = assetQuery.Where(a => a.PurchaseInvoiceId == id)
            .Select(a => new ConnectionDocumentDto
            {
                Id = a.Id, DocumentNumber = a.AssetName, Status = a.Status.ToString(),
                Route = "/assets/" + a.Id
            }).ToList();

        // Landed Cost Vouchers that allocated charges to this PI
        var lciRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<LandedCostItem, Guid>>();
        var lciQuery = await lciRepo.GetQueryableAsync();
        var lcvIds = lciQuery.Where(lci => lci.ReceiptType == "PurchaseInvoice" && lci.ReceiptId == id)
            .Select(lci => lci.LandedCostVoucherId).Distinct().ToList();

        var lcvConnections = new List<ConnectionDocumentDto>();
        if (lcvIds.Any())
        {
            var lcvRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<LandedCostVoucher, Guid>>();
            var lcvQuery = await lcvRepo.GetQueryableAsync();
            lcvConnections = lcvQuery.Where(lcv => lcvIds.Contains(lcv.Id))
                .Select(lcv => new ConnectionDocumentDto
                {
                    Id = lcv.Id, DocumentNumber = lcv.VoucherNumber, Status = lcv.Status.ToString(),
                    Date = lcv.PostingDate, Route = "/inventory/landed-costs/" + lcv.Id
                }).ToList();
        }

        var otherGroup = new ConnectionGroupDto { Label = "Other", Items = new() };
        if (assets.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Asset", Count = assets.Count, Route = "/assets", Documents = assets });
        if (lcvConnections.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Landed Cost Voucher", Count = lcvConnections.Count, Route = "/inventory/landed-costs", Documents = lcvConnections });

        if (paymentGroup.Items.Any()) result.Groups.Add(paymentGroup);
        if (refGroup.Items.Any()) result.Groups.Add(refGroup);
        if (returnGroup.Items.Any()) result.Groups.Add(returnGroup);
        if (otherGroup.Items.Any()) result.Groups.Add(otherGroup);

        return result;
    }

    private async Task<DocumentConnectionsDto> GetSalesOrderConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        // Delivery Notes linked to this SO
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var dnQuery = await dnRepo.GetQueryableAsync();
        var dns = dnQuery.Where(dn => dn.SalesOrderId == id)
            .Select(dn => new ConnectionDocumentDto
            {
                Id = dn.Id, DocumentNumber = dn.DeliveryNumber, Status = dn.Status.ToString(),
                Date = dn.PostingDate, Route = "/sales/delivery-notes/" + dn.Id
            }).ToList();

        // Sales Invoices linked via items — matched the same way GetPurchaseOrderConnectionsAsync
        // matches Purchase Invoices to a Purchase Order below: resolve this order's own item ids,
        // then filter invoices whose items reference one of them.
        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var so = await soRepo.GetAsync(id);
        var soItemIds = so.Items.Select(i => i.Id).ToList();

        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var sis = siQuery
            .Where(si => si.Items.Any(i => i.SalesOrderItemId.HasValue && soItemIds.Contains(i.SalesOrderItemId.Value)))
            .Select(si => new ConnectionDocumentDto
            {
                Id = si.Id, DocumentNumber = si.InvoiceNumber, Status = si.Status.ToString(),
                Amount = si.GrandTotal, Date = si.IssueDate, Route = "/sales/invoices/" + si.Id
            }).ToList();

        // Payment Entries
        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.AgainstOrderId == id && pe.AgainstOrderType == "SalesOrder")
            .Select(pe => new ConnectionDocumentDto
            {
                Id = pe.Id, DocumentNumber = pe.PaymentNumber, Status = pe.Status.ToString(),
                Amount = pe.PaidAmount, Date = pe.PostingDate, Route = "/accounting/payments/" + pe.Id
            }).ToList();

        // Work Orders
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var woQuery = await woRepo.GetQueryableAsync();
        var wos = woQuery.Where(wo => wo.SalesOrderId == id)
            .Select(wo => new ConnectionDocumentDto
            {
                Id = wo.Id, DocumentNumber = wo.WorkOrderNumber, Status = wo.Status.ToString(),
                Date = wo.PlannedStartDate, Route = "/manufacturing/work-orders/" + wo.Id
            }).ToList();

        // Pick Lists
        var plRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PickList, Guid>>();
        var plQuery = await plRepo.GetQueryableAsync();
        var pickLists = plQuery.Where(pl => pl.SalesOrderId == id)
            .Select(pl => new ConnectionDocumentDto
            {
                Id = pl.Id, DocumentNumber = pl.PickListNumber, Status = pl.Status.ToString(),
                Route = "/inventory/pick-lists/" + pl.Id
            }).ToList();

        var fulfillGroup = new ConnectionGroupDto { Label = "Fulfillment", Items = new() };
        if (dns.Any())
            fulfillGroup.Items.Add(new ConnectionItemDto { DocumentType = "Delivery Note", Count = dns.Count, Route = "/sales/delivery-notes", Documents = dns });
        if (pickLists.Any())
            fulfillGroup.Items.Add(new ConnectionItemDto { DocumentType = "Pick List", Count = pickLists.Count, Route = "/inventory/pick-lists", Documents = pickLists });

        var billingGroup = new ConnectionGroupDto { Label = "Billing", Items = new() };
        if (sis.Any())
            billingGroup.Items.Add(new ConnectionItemDto { DocumentType = "Sales Invoice", Count = sis.Count, Route = "/sales/invoices", Documents = sis });

        var paymentGroup = new ConnectionGroupDto { Label = "Payment", Items = new() };
        if (payments.Any())
            paymentGroup.Items.Add(new ConnectionItemDto { DocumentType = "Payment Entry", Count = payments.Count, Route = "/accounting/payments", Documents = payments });

        var mfgGroup = new ConnectionGroupDto { Label = "Manufacturing", Items = new() };
        if (wos.Any())
            mfgGroup.Items.Add(new ConnectionItemDto { DocumentType = "Work Order", Count = wos.Count, Route = "/manufacturing/work-orders", Documents = wos });

        if (fulfillGroup.Items.Any()) result.Groups.Add(fulfillGroup);
        if (billingGroup.Items.Any()) result.Groups.Add(billingGroup);
        if (paymentGroup.Items.Any()) result.Groups.Add(paymentGroup);
        if (mfgGroup.Items.Any()) result.Groups.Add(mfgGroup);

        return result;
    }

    private async Task<DocumentConnectionsDto> GetPurchaseOrderConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var prQuery = await prRepo.GetQueryableAsync();
        var receipts = prQuery.Where(pr => pr.PurchaseOrderId == id)
            .Select(pr => new ConnectionDocumentDto
            {
                Id = pr.Id, DocumentNumber = pr.ReceiptNumber, Status = pr.Status.ToString(),
                Date = pr.PostingDate, Route = "/purchasing/receipts/" + pr.Id
            }).ToList();

        // Linked Purchase Invoices (via PI items referencing this PO's items)
        var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
        var po = await poRepo.GetAsync(id);
        var poItemIds = po.Items.Select(i => i.Id).ToList();

        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var invoices = piQuery
            .Where(pi => pi.Items.Any(i => i.PurchaseOrderItemId.HasValue && poItemIds.Contains(i.PurchaseOrderItemId.Value)))
            .Select(pi => new ConnectionDocumentDto
            {
                Id = pi.Id, DocumentNumber = pi.InvoiceNumber, Status = pi.Status.ToString(),
                Amount = pi.GrandTotal, Date = pi.IssueDate, Route = "/purchasing/invoices/" + pi.Id
            }).ToList();

        var peRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PaymentEntry, Guid>>();
        var peQuery = await peRepo.GetQueryableAsync();
        var payments = peQuery
            .Where(pe => pe.AgainstOrderId == id && pe.AgainstOrderType == "PurchaseOrder")
            .Select(pe => new ConnectionDocumentDto
            {
                Id = pe.Id, DocumentNumber = pe.PaymentNumber, Status = pe.Status.ToString(),
                Amount = pe.PaidAmount, Date = pe.PostingDate, Route = "/accounting/payments/" + pe.Id
            }).ToList();

        // Source Supplier Quotation
        var sqConnections = new List<ConnectionDocumentDto>();
        if (po.SupplierQuotationId.HasValue)
        {
            var sqRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SupplierQuotation, Guid>>();
            var sq = await sqRepo.FindAsync(po.SupplierQuotationId.Value);
            if (sq != null)
                sqConnections.Add(new ConnectionDocumentDto
                {
                    Id = sq.Id, DocumentNumber = sq.QuotationNumber, Status = sq.Status.ToString(),
                    Route = "/purchasing/supplier-quotations/" + sq.Id
                });
        }

        var receivingGroup = new ConnectionGroupDto { Label = "Receiving", Items = new() };
        if (receipts.Any())
            receivingGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Receipt", Count = receipts.Count, Route = "/purchasing/receipts", Documents = receipts });

        var billingGroup = new ConnectionGroupDto { Label = "Billing", Items = new() };
        if (invoices.Any())
            billingGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Invoice", Count = invoices.Count, Route = "/purchasing/invoices", Documents = invoices });

        var paymentGroup = new ConnectionGroupDto { Label = "Payment", Items = new() };
        if (payments.Any())
            paymentGroup.Items.Add(new ConnectionItemDto { DocumentType = "Payment Entry", Count = payments.Count, Route = "/accounting/payments", Documents = payments });

        var refGroup = new ConnectionGroupDto { Label = "Reference", Items = new() };
        if (sqConnections.Any())
            refGroup.Items.Add(new ConnectionItemDto { DocumentType = "Supplier Quotation", Count = sqConnections.Count, Route = "/purchasing/supplier-quotations", Documents = sqConnections });

        if (refGroup.Items.Any()) result.Groups.Add(refGroup);
        if (receivingGroup.Items.Any()) result.Groups.Add(receivingGroup);
        if (billingGroup.Items.Any()) result.Groups.Add(billingGroup);
        if (paymentGroup.Items.Any()) result.Groups.Add(paymentGroup);

        return result;
    }

    private async Task<DocumentConnectionsDto> GetDeliveryNoteConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        // Source SO
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var dn = await dnRepo.GetAsync(id);

        // Linked SI — matched by this DN's own item ids, same pattern as GetPurchaseOrderConnectionsAsync.
        var dnItemIds = dn.Items.Select(i => i.Id).ToList();
        var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesInvoice, Guid>>();
        var siQuery = await siRepo.GetQueryableAsync();
        var invoices = siQuery
            .Where(si => si.Items.Any(i => i.DeliveryNoteItemId.HasValue && dnItemIds.Contains(i.DeliveryNoteItemId.Value)))
            .Select(si => new ConnectionDocumentDto
            {
                Id = si.Id, DocumentNumber = si.InvoiceNumber, Status = si.Status.ToString(),
                Amount = si.GrandTotal, Date = si.IssueDate, Route = "/sales/invoices/" + si.Id
            }).ToList();

        var soConnections = new List<ConnectionDocumentDto>();
        if (dn.SalesOrderId.HasValue)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var so = await soRepo.FindAsync(dn.SalesOrderId.Value);
            if (so != null)
                soConnections.Add(new ConnectionDocumentDto
                {
                    Id = so.Id, DocumentNumber = so.OrderNumber, Status = so.Status.ToString(),
                    Amount = so.GrandTotal, Date = so.OrderDate, Route = "/sales/orders/" + so.Id
                });
        }

        var billingGroup = new ConnectionGroupDto { Label = "Billing", Items = new() };
        if (invoices.Any())
            billingGroup.Items.Add(new ConnectionItemDto { DocumentType = "Sales Invoice", Count = invoices.Count, Route = "/sales/invoices", Documents = invoices });

        var refGroup = new ConnectionGroupDto { Label = "Reference", Items = new() };
        if (soConnections.Any())
            refGroup.Items.Add(new ConnectionItemDto { DocumentType = "Sales Order", Count = soConnections.Count, Route = "/sales/orders", Documents = soConnections });

        // Quality Inspections against this DN
        var qiRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<QualityInspection, Guid>>();
        var qiQuery = await qiRepo.GetQueryableAsync();
        var inspections = qiQuery.Where(qi => qi.ReferenceType == "DeliveryNote" && qi.ReferenceId == id)
            .Select(qi => new ConnectionDocumentDto
            {
                Id = qi.Id, DocumentNumber = qi.InspectionNumber, Status = qi.DocStatus.ToString(),
                Date = qi.InspectionDate, Route = "/inventory/quality-inspections/" + qi.Id
            }).ToList();

        // Packing Slips
        var psRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PackingSlip, Guid>>();
        var psQuery = await psRepo.GetQueryableAsync();
        var packingSlips = psQuery.Where(ps => ps.DeliveryNoteId == id)
            .Select(ps => new ConnectionDocumentDto
            {
                Id = ps.Id, DocumentNumber = "Cases " + ps.FromCaseNo + "-" + ps.ToCaseNo,
                Route = "/sales/packing-slips/" + ps.Id
            }).ToList();

        // Shipments (many-to-many via ShipmentDeliveryNote)
        var shipRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Shipment, Guid>>();
        var shipQuery = await shipRepo.GetQueryableAsync();
        var shipments = shipQuery.Where(sh => sh.DeliveryNotes.Any(dnl => dnl.DeliveryNoteId == id))
            .Select(sh => new ConnectionDocumentDto
            {
                Id = sh.Id, DocumentNumber = sh.ShipmentNumber, Status = sh.Status.ToString(),
                Route = "/sales/shipments/" + sh.Id
            }).ToList();

        var otherGroup = new ConnectionGroupDto { Label = "Other", Items = new() };
        if (inspections.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Quality Inspection", Count = inspections.Count, Route = "/inventory/quality-inspections", Documents = inspections });
        if (packingSlips.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Packing Slip", Count = packingSlips.Count, Route = "/sales/packing-slips", Documents = packingSlips });
        if (shipments.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Shipment", Count = shipments.Count, Route = "/sales/shipments", Documents = shipments });

        if (refGroup.Items.Any()) result.Groups.Add(refGroup);
        if (billingGroup.Items.Any()) result.Groups.Add(billingGroup);
        if (otherGroup.Items.Any()) result.Groups.Add(otherGroup);

        return result;
    }

    private async Task<DocumentConnectionsDto> GetPurchaseReceiptConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        var prRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseReceipt, Guid>>();
        var pr = await prRepo.GetAsync(id);
        var poConnections = new List<ConnectionDocumentDto>();
        if (pr.PurchaseOrderId.HasValue)
        {
            var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var po = await poRepo.FindAsync(pr.PurchaseOrderId.Value);
            if (po != null)
                poConnections.Add(new ConnectionDocumentDto
                {
                    Id = po.Id, DocumentNumber = po.OrderNumber, Status = po.Status.ToString(),
                    Amount = po.GrandTotal, Date = po.OrderDate, Route = "/purchasing/orders/" + po.Id
                });
        }

        // Linked Purchase Invoices (via PI items referencing this PR's items) — reverse of the
        // lookup GetPurchaseInvoiceConnectionsAsync already does the other way around.
        var prItemIds = pr.Items.Select(i => i.Id).ToList();
        var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PurchaseInvoice, Guid>>();
        var piQuery = await piRepo.GetQueryableAsync();
        var invoices = piQuery
            .Where(pi => pi.Items.Any(i => i.PurchaseReceiptItemId.HasValue && prItemIds.Contains(i.PurchaseReceiptItemId.Value)))
            .Select(pi => new ConnectionDocumentDto
            {
                Id = pi.Id, DocumentNumber = pi.InvoiceNumber, Status = pi.Status.ToString(),
                Amount = pi.GrandTotal, Date = pi.IssueDate, Route = "/purchasing/invoices/" + pi.Id
            }).ToList();

        // Quality Inspections against this PR
        var qiRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<QualityInspection, Guid>>();
        var qiQuery = await qiRepo.GetQueryableAsync();
        var inspections = qiQuery.Where(qi => qi.ReferenceType == "PurchaseReceipt" && qi.ReferenceId == id)
            .Select(qi => new ConnectionDocumentDto
            {
                Id = qi.Id, DocumentNumber = qi.InspectionNumber, Status = qi.DocStatus.ToString(),
                Date = qi.InspectionDate, Route = "/inventory/quality-inspections/" + qi.Id
            }).ToList();

        // Landed Cost Vouchers that allocated charges to this PR
        var lciRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<LandedCostItem, Guid>>();
        var lciQuery = await lciRepo.GetQueryableAsync();
        var lcvIds = lciQuery.Where(lci => lci.ReceiptType == "PurchaseReceipt" && lci.ReceiptId == id)
            .Select(lci => lci.LandedCostVoucherId).Distinct().ToList();

        var lcvConnections = new List<ConnectionDocumentDto>();
        if (lcvIds.Any())
        {
            var lcvRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<LandedCostVoucher, Guid>>();
            var lcvQuery = await lcvRepo.GetQueryableAsync();
            lcvConnections = lcvQuery.Where(lcv => lcvIds.Contains(lcv.Id))
                .Select(lcv => new ConnectionDocumentDto
                {
                    Id = lcv.Id, DocumentNumber = lcv.VoucherNumber, Status = lcv.Status.ToString(),
                    Date = lcv.PostingDate, Route = "/inventory/landed-costs/" + lcv.Id
                }).ToList();
        }

        var refGroup = new ConnectionGroupDto { Label = "Reference", Items = new() };
        if (poConnections.Any())
            refGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Order", Count = poConnections.Count, Route = "/purchasing/orders", Documents = poConnections });

        var billingGroup = new ConnectionGroupDto { Label = "Billing", Items = new() };
        if (invoices.Any())
            billingGroup.Items.Add(new ConnectionItemDto { DocumentType = "Purchase Invoice", Count = invoices.Count, Route = "/purchasing/invoices", Documents = invoices });

        var otherGroup = new ConnectionGroupDto { Label = "Other", Items = new() };
        if (inspections.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Quality Inspection", Count = inspections.Count, Route = "/inventory/quality-inspections", Documents = inspections });
        if (lcvConnections.Any())
            otherGroup.Items.Add(new ConnectionItemDto { DocumentType = "Landed Cost Voucher", Count = lcvConnections.Count, Route = "/inventory/landed-costs", Documents = lcvConnections });

        if (refGroup.Items.Any()) result.Groups.Add(refGroup);
        if (billingGroup.Items.Any()) result.Groups.Add(billingGroup);
        if (otherGroup.Items.Any()) result.Groups.Add(otherGroup);
        return result;
    }

    private async Task<DocumentConnectionsDto> GetPaymentEntryConnectionsAsync(Guid id)
    {
        // PE already shows its references in the detail view
        return new DocumentConnectionsDto();
    }

    private async Task<DocumentConnectionsDto> GetStockEntryConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockEntry, Guid>>();
        var se = await seRepo.GetAsync(id);

        if (se.WorkOrderId.HasValue)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var wo = await woRepo.FindAsync(se.WorkOrderId.Value);
            if (wo != null)
            {
                var refGroup = new ConnectionGroupDto
                {
                    Label = "Reference",
                    Items = new()
                    {
                        new ConnectionItemDto
                        {
                            DocumentType = "Work Order", Count = 1, Route = "/manufacturing/work-orders",
                            Documents = new()
                            {
                                new ConnectionDocumentDto
                                {
                                    Id = wo.Id, DocumentNumber = wo.WorkOrderNumber, Status = wo.Status.ToString(),
                                    Date = wo.PlannedStartDate, Route = "/manufacturing/work-orders/" + wo.Id
                                }
                            }
                        }
                    }
                };
                result.Groups.Add(refGroup);
            }
        }

        return result;
    }

    private async Task<DocumentConnectionsDto> GetWorkOrderConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        // Stock Entries linked to this WO
        var seRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<StockEntry, Guid>>();
        var seQuery = await seRepo.GetQueryableAsync();
        var ses = seQuery.Where(se => se.WorkOrderId == id)
            .Select(se => new ConnectionDocumentDto
            {
                Id = se.Id, DocumentNumber = se.EntryNumber, Status = se.Status.ToString(),
                Date = se.PostingDate, Route = "/inventory/stock-entries/" + se.Id
            }).ToList();

        // Job Cards
        var jcRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JobCard, Guid>>();
        var jcQuery = await jcRepo.GetQueryableAsync();
        var jcs = jcQuery.Where(jc => jc.WorkOrderId == id)
            .Select(jc => new ConnectionDocumentDto
            {
                Id = jc.Id, DocumentNumber = jc.SequenceId.ToString(), Status = jc.Status.ToString(),
                Route = "/manufacturing/job-cards/" + jc.Id
            }).ToList();

        var prodGroup = new ConnectionGroupDto { Label = "Production", Items = new() };
        if (ses.Any())
            prodGroup.Items.Add(new ConnectionItemDto { DocumentType = "Stock Entry", Count = ses.Count, Route = "/inventory/stock-entries", Documents = ses });
        if (jcs.Any())
            prodGroup.Items.Add(new ConnectionItemDto { DocumentType = "Job Card", Count = jcs.Count, Route = "/manufacturing/job-cards", Documents = jcs });

        if (prodGroup.Items.Any()) result.Groups.Add(prodGroup);

        // Source SO
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var wo = await woRepo.GetAsync(id);
        if (wo.SalesOrderId.HasValue)
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
            var so = await soRepo.FindAsync(wo.SalesOrderId.Value);
            if (so != null)
            {
                var refGroup = new ConnectionGroupDto
                {
                    Label = "Reference",
                    Items = new()
                    {
                        new ConnectionItemDto
                        {
                            DocumentType = "Sales Order", Count = 1, Route = "/sales/orders",
                            Documents = new()
                            {
                                new ConnectionDocumentDto
                                {
                                    Id = so.Id, DocumentNumber = so.OrderNumber, Status = so.Status.ToString(),
                                    Amount = so.GrandTotal, Date = so.OrderDate, Route = "/sales/orders/" + so.Id
                                }
                            }
                        }
                    }
                };
                result.Groups.Add(refGroup);
            }
        }

        return result;
    }

    private async Task<DocumentConnectionsDto> GetQuotationConnectionsAsync(Guid id)
    {
        var result = new DocumentConnectionsDto();

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var soQuery = await soRepo.GetQueryableAsync();
        var orders = soQuery.Where(so => so.QuotationId == id)
            .Select(so => new ConnectionDocumentDto
            {
                Id = so.Id, DocumentNumber = so.OrderNumber, Status = so.Status.ToString(),
                Amount = so.GrandTotal, Date = so.OrderDate, Route = "/sales/orders/" + so.Id
            }).ToList();

        var conversionGroup = new ConnectionGroupDto { Label = "Conversion", Items = new() };
        if (orders.Any())
            conversionGroup.Items.Add(new ConnectionItemDto { DocumentType = "Sales Order", Count = orders.Count, Route = "/sales/orders", Documents = orders });

        if (conversionGroup.Items.Any()) result.Groups.Add(conversionGroup);
        return result;
    }
}
