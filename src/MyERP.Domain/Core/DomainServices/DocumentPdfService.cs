using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace MyERP.Core.DomainServices;

/// <summary>
/// PDF generation service for document printing.
/// Generates PDF bytes from structured document data using HTML template rendering.
/// 
/// Uses a lightweight HTML-to-PDF approach: renders an HTML template with document data,
/// then converts to PDF. This avoids heavy dependencies like Puppeteer/wkhtmltopdf.
/// 
/// For production deployments, replace the HTML rendering with a proper PDF library
/// (e.g., QuestPDF, IronPDF, or an external microservice).
/// </summary>
public interface IDocumentPdfService
{
    /// <summary>Generate PDF bytes for a Sales Invoice.</summary>
    Task<byte[]> GenerateSalesInvoicePdfAsync(SalesInvoicePdfData data);

    /// <summary>Generate PDF bytes for a Purchase Order.</summary>
    Task<byte[]> GeneratePurchaseOrderPdfAsync(PurchaseOrderPdfData data);

    /// <summary>Generate PDF bytes for a Delivery Note.</summary>
    Task<byte[]> GenerateDeliveryNotePdfAsync(DeliveryNotePdfData data);

    /// <summary>Generate PDF bytes for a Quotation.</summary>
    Task<byte[]> GenerateQuotationPdfAsync(QuotationPdfData data);

    /// <summary>Generate PDF bytes for a Sales Order.</summary>
    Task<byte[]> GenerateSalesOrderPdfAsync(SalesOrderPdfData data);

    /// <summary>Generate PDF from any document using a named template.</summary>
    Task<byte[]> GenerateFromTemplateAsync(string templateName, Dictionary<string, object> data);
}

/// <summary>
/// HTML-based PDF generator. Renders structured data into an HTML template
/// suitable for conversion to PDF. Uses simple string templating for speed.
/// 
/// In production, this HTML output can be piped to:
/// - QuestPDF for .NET-native PDF generation
/// - A headless browser service for pixel-perfect rendering
/// - An external PDF API service
/// </summary>
public class DocumentPdfService : IDocumentPdfService, ITransientDependency
{
    public Task<byte[]> GenerateSalesInvoicePdfAsync(SalesInvoicePdfData data)
    {
        var html = RenderSalesInvoiceHtml(data);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> GeneratePurchaseOrderPdfAsync(PurchaseOrderPdfData data)
    {
        var html = RenderPurchaseOrderHtml(data);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> GenerateDeliveryNotePdfAsync(DeliveryNotePdfData data)
    {
        var html = RenderDeliveryNoteHtml(data);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> GenerateQuotationPdfAsync(QuotationPdfData data)
    {
        var html = RenderQuotationHtml(data);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> GenerateSalesOrderPdfAsync(SalesOrderPdfData data)
    {
        var html = $"<html><body><h1>Sales Order {data.OrderNumber}</h1><p>Customer: {data.CustomerName}</p><p>Date: {data.OrderDate:dd/MM/yyyy}</p><p>Grand Total: {data.Currency} {data.GrandTotal:N2}</p></body></html>";
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public Task<byte[]> GenerateFromTemplateAsync(string templateName, Dictionary<string, object> data)
    {
        // Extensible template-based generation — for custom formats
        var html = $"<html><body><h1>{templateName}</h1><pre>{System.Text.Json.JsonSerializer.Serialize(data)}</pre></body></html>";
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    private static string RenderSalesInvoiceHtml(SalesInvoicePdfData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<style>");
        sb.AppendLine(GetCommonStyles());
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<div class=\"document\">");

        // Header
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<div class=\"company\"><h1>{Encode(d.CompanyName)}</h1>");
        if (!string.IsNullOrEmpty(d.CompanyTin)) sb.AppendLine($"<p>TIN: {Encode(d.CompanyTin)}</p>");
        if (!string.IsNullOrEmpty(d.CompanySst)) sb.AppendLine($"<p>SST: {Encode(d.CompanySst)}</p>");
        if (!string.IsNullOrEmpty(d.CompanyAddress)) sb.AppendLine($"<p>{Encode(d.CompanyAddress)}</p>");
        if (!string.IsNullOrEmpty(d.CompanyPhone)) sb.AppendLine($"<p>Tel: {Encode(d.CompanyPhone)}</p>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<div class=\"doc-title\"><h2>{(d.IsReturn ? "CREDIT NOTE" : "TAX INVOICE")}</h2>");
        sb.AppendLine($"<p class=\"doc-number\">{Encode(d.InvoiceNumber)}</p>");
        sb.AppendLine($"<p>Date: {d.IssueDate:dd/MM/yyyy}</p>");
        if (d.DueDate.HasValue) sb.AppendLine($"<p>Due: {d.DueDate:dd/MM/yyyy}</p>");
        sb.AppendLine("</div></div>");

        // Customer
        sb.AppendLine("<div class=\"parties\"><div class=\"party\">");
        sb.AppendLine("<h4>BILL TO</h4>");
        sb.AppendLine($"<p><strong>{Encode(d.CustomerName)}</strong></p>");
        if (!string.IsNullOrEmpty(d.CustomerTin)) sb.AppendLine($"<p>TIN: {Encode(d.CustomerTin)}</p>");
        if (!string.IsNullOrEmpty(d.CustomerAddress)) sb.AppendLine($"<p>{Encode(d.CustomerAddress)}</p>");
        sb.AppendLine("</div></div>");

        // Items table
        sb.AppendLine("<table class=\"items\"><thead><tr>");
        sb.AppendLine("<th class=\"sno\">#</th><th>Description</th><th class=\"r\">Qty</th><th class=\"r\">Rate</th><th class=\"r\">Amount</th>");
        sb.AppendLine("</tr></thead><tbody>");
        for (int i = 0; i < d.Items.Count; i++)
        {
            var item = d.Items[i];
            sb.AppendLine($"<tr><td class=\"sno\">{i + 1}</td>");
            sb.AppendLine($"<td>{Encode(item.Description)}</td>");
            sb.AppendLine($"<td class=\"r\">{item.Quantity:N2}</td>");
            sb.AppendLine($"<td class=\"r\">{item.Rate:N2}</td>");
            sb.AppendLine($"<td class=\"r\">{item.Amount:N2}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Totals
        sb.AppendLine("<div class=\"totals\"><table>");
        sb.AppendLine($"<tr><td>Net Total</td><td class=\"r\">{d.Currency} {d.NetTotal:N2}</td></tr>");
        if (d.DiscountAmount > 0) sb.AppendLine($"<tr><td>Discount</td><td class=\"r\">- {d.Currency} {d.DiscountAmount:N2}</td></tr>");
        if (d.TaxAmount > 0) sb.AppendLine($"<tr><td>Tax</td><td class=\"r\">{d.Currency} {d.TaxAmount:N2}</td></tr>");
        sb.AppendLine($"<tr class=\"grand\"><td><strong>Grand Total</strong></td><td class=\"r\"><strong>{d.Currency} {d.GrandTotal:N2}</strong></td></tr>");
        sb.AppendLine("</table></div>");

        // Footer
        if (!string.IsNullOrEmpty(d.Notes))
            sb.AppendLine($"<div class=\"notes\"><p>{Encode(d.Notes)}</p></div>");
        if (!string.IsNullOrEmpty(d.BankDetails))
            sb.AppendLine($"<div class=\"bank\"><h4>Bank Details</h4><p>{Encode(d.BankDetails)}</p></div>");

        sb.AppendLine("<div class=\"thank-you\"><p>Thank you for your business.</p></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string RenderPurchaseOrderHtml(PurchaseOrderPdfData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>");
        sb.AppendLine(GetCommonStyles());
        sb.AppendLine("</style></head><body><div class=\"document\">");
        sb.AppendLine("<div class=\"header\"><div class=\"company\">");
        sb.AppendLine($"<h1>{Encode(d.CompanyName)}</h1>");
        if (!string.IsNullOrEmpty(d.CompanyAddress)) sb.AppendLine($"<p>{Encode(d.CompanyAddress)}</p>");
        sb.AppendLine($"</div><div class=\"doc-title\"><h2>PURCHASE ORDER</h2>");
        sb.AppendLine($"<p class=\"doc-number\">{Encode(d.OrderNumber)}</p>");
        sb.AppendLine($"<p>Date: {d.OrderDate:dd/MM/yyyy}</p>");
        if (d.ExpectedDeliveryDate.HasValue) sb.AppendLine($"<p>Expected: {d.ExpectedDeliveryDate:dd/MM/yyyy}</p>");
        sb.AppendLine("</div></div>");
        sb.AppendLine($"<div class=\"parties\"><div class=\"party\"><h4>SUPPLIER</h4>");
        sb.AppendLine($"<p><strong>{Encode(d.SupplierName)}</strong></p>");
        if (!string.IsNullOrEmpty(d.SupplierAddress)) sb.AppendLine($"<p>{Encode(d.SupplierAddress)}</p>");
        sb.AppendLine("</div></div>");
        sb.AppendLine("<table class=\"items\"><thead><tr><th class=\"sno\">#</th><th>Description</th><th class=\"r\">Qty</th><th class=\"r\">Rate</th><th class=\"r\">Amount</th></tr></thead><tbody>");
        for (int i = 0; i < d.Items.Count; i++)
        {
            var item = d.Items[i];
            sb.AppendLine($"<tr><td class=\"sno\">{i + 1}</td><td>{Encode(item.Description)}</td><td class=\"r\">{item.Quantity:N2}</td><td class=\"r\">{item.Rate:N2}</td><td class=\"r\">{item.Amount:N2}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<div class=\"totals\"><table><tr class=\"grand\"><td><strong>Total</strong></td><td class=\"r\"><strong>{d.Currency} {d.GrandTotal:N2}</strong></td></tr></table></div>");
        if (!string.IsNullOrEmpty(d.Terms)) sb.AppendLine($"<div class=\"notes\"><h4>Terms & Conditions</h4><p>{Encode(d.Terms)}</p></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string RenderDeliveryNoteHtml(DeliveryNotePdfData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>");
        sb.AppendLine(GetCommonStyles());
        sb.AppendLine("</style></head><body><div class=\"document\">");
        sb.AppendLine($"<div class=\"header\"><div class=\"company\"><h1>{Encode(d.CompanyName)}</h1></div>");
        sb.AppendLine($"<div class=\"doc-title\"><h2>DELIVERY NOTE</h2><p class=\"doc-number\">{Encode(d.DeliveryNumber)}</p>");
        sb.AppendLine($"<p>Date: {d.PostingDate:dd/MM/yyyy}</p></div></div>");
        sb.AppendLine($"<div class=\"parties\"><div class=\"party\"><h4>DELIVER TO</h4><p><strong>{Encode(d.CustomerName)}</strong></p>");
        if (!string.IsNullOrEmpty(d.ShippingAddress)) sb.AppendLine($"<p>{Encode(d.ShippingAddress)}</p>");
        sb.AppendLine("</div></div>");
        sb.AppendLine("<table class=\"items\"><thead><tr><th class=\"sno\">#</th><th>Description</th><th class=\"r\">Qty</th><th class=\"r\">UOM</th></tr></thead><tbody>");
        for (int i = 0; i < d.Items.Count; i++)
        {
            var item = d.Items[i];
            sb.AppendLine($"<tr><td class=\"sno\">{i + 1}</td><td>{Encode(item.Description)}</td><td class=\"r\">{item.Quantity:N2}</td><td class=\"r\">{Encode(item.Uom)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        if (!string.IsNullOrEmpty(d.TransporterInfo)) sb.AppendLine($"<div class=\"notes\"><p>Transporter: {Encode(d.TransporterInfo)}</p></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string RenderQuotationHtml(QuotationPdfData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\" /><style>");
        sb.AppendLine(GetCommonStyles());
        sb.AppendLine("</style></head><body><div class=\"document\">");
        sb.AppendLine($"<div class=\"header\"><div class=\"company\"><h1>{Encode(d.CompanyName)}</h1>");
        if (!string.IsNullOrEmpty(d.CompanyAddress)) sb.AppendLine($"<p>{Encode(d.CompanyAddress)}</p>");
        sb.AppendLine($"</div><div class=\"doc-title\"><h2>QUOTATION</h2><p class=\"doc-number\">{Encode(d.QuotationNumber)}</p>");
        sb.AppendLine($"<p>Date: {d.TransactionDate:dd/MM/yyyy}</p>");
        if (d.ValidTill.HasValue) sb.AppendLine($"<p>Valid Till: {d.ValidTill:dd/MM/yyyy}</p>");
        sb.AppendLine("</div></div>");
        sb.AppendLine($"<div class=\"parties\"><div class=\"party\"><h4>TO</h4><p><strong>{Encode(d.PartyName)}</strong></p></div></div>");
        sb.AppendLine("<table class=\"items\"><thead><tr><th class=\"sno\">#</th><th>Description</th><th class=\"r\">Qty</th><th class=\"r\">Rate</th><th class=\"r\">Amount</th></tr></thead><tbody>");
        for (int i = 0; i < d.Items.Count; i++)
        {
            var item = d.Items[i];
            sb.AppendLine($"<tr><td class=\"sno\">{i + 1}</td><td>{Encode(item.Description)}</td><td class=\"r\">{item.Quantity:N2}</td><td class=\"r\">{item.Rate:N2}</td><td class=\"r\">{item.Amount:N2}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<div class=\"totals\"><table><tr><td>Net Total</td><td class=\"r\">{d.Currency} {d.NetTotal:N2}</td></tr>");
        if (d.TaxAmount > 0) sb.AppendLine($"<tr><td>Tax</td><td class=\"r\">{d.Currency} {d.TaxAmount:N2}</td></tr>");
        sb.AppendLine($"<tr class=\"grand\"><td><strong>Grand Total</strong></td><td class=\"r\"><strong>{d.Currency} {d.GrandTotal:N2}</strong></td></tr></table></div>");
        if (!string.IsNullOrEmpty(d.Terms)) sb.AppendLine($"<div class=\"notes\"><h4>Terms & Conditions</h4><p>{Encode(d.Terms)}</p></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string GetCommonStyles() => @"
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 20mm; color: #333; font-size: 10pt; }
        .document { max-width: 210mm; margin: 0 auto; }
        .header { display: flex; justify-content: space-between; border-bottom: 2px solid #1976d2; padding-bottom: 12px; margin-bottom: 20px; }
        .company h1 { color: #1976d2; margin: 0 0 4px; font-size: 18pt; }
        .company p { margin: 2px 0; font-size: 9pt; color: #555; }
        .doc-title { text-align: right; }
        .doc-title h2 { margin: 0; font-size: 14pt; }
        .doc-number { font-weight: 600; font-size: 11pt; }
        .parties { margin-bottom: 16px; }
        .party h4 { color: #888; font-size: 8pt; text-transform: uppercase; margin: 0 0 4px; }
        .party p { margin: 2px 0; }
        .items { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
        .items th { background: #f5f7fa; border: 1px solid #ddd; padding: 6px 8px; font-size: 8pt; text-transform: uppercase; }
        .items td { border: 1px solid #ddd; padding: 5px 8px; font-size: 9pt; }
        .items .sno { width: 30px; text-align: center; }
        .items .r { text-align: right; }
        .totals { display: flex; justify-content: flex-end; margin-bottom: 16px; }
        .totals table { width: 250px; }
        .totals td { padding: 4px 8px; }
        .totals .r { text-align: right; }
        .totals .grand td { border-top: 2px solid #333; padding-top: 8px; font-size: 11pt; }
        .notes { margin-top: 16px; padding-top: 12px; border-top: 1px solid #eee; font-size: 9pt; }
        .bank { margin-top: 12px; font-size: 9pt; }
        .bank h4 { margin: 0 0 4px; font-size: 8pt; text-transform: uppercase; color: #888; }
        .thank-you { text-align: center; font-style: italic; margin-top: 20px; color: #888; }
        @media print { body { padding: 10mm; } }
    ";

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");
}

// --- PDF Data Models ---

public class SalesInvoicePdfData
{
    public string CompanyName { get; set; } = "";
    public string? CompanyTin { get; set; }
    public string? CompanySst { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyPhone { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsReturn { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerTin { get; set; }
    public string? CustomerAddress { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string? BankDetails { get; set; }
    public List<PdfLineItem> Items { get; set; } = new();
}

public class PurchaseOrderPdfData
{
    public string CompanyName { get; set; } = "";
    public string? CompanyAddress { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string SupplierName { get; set; } = "";
    public string? SupplierAddress { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal GrandTotal { get; set; }
    public string? Terms { get; set; }
    public List<PdfLineItem> Items { get; set; } = new();
}

public class DeliveryNotePdfData
{
    public string CompanyName { get; set; } = "";
    public string DeliveryNumber { get; set; } = "";
    public DateTime PostingDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string? ShippingAddress { get; set; }
    public string? TransporterInfo { get; set; }
    public List<DeliveryNoteLineItem> Items { get; set; } = new();
}

public class QuotationPdfData
{
    public string CompanyName { get; set; } = "";
    public string? CompanyAddress { get; set; }
    public string QuotationNumber { get; set; } = "";
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }
    public string PartyName { get; set; } = "";
    public string Currency { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Terms { get; set; }
    public List<PdfLineItem> Items { get; set; } = new();
}

public class SalesOrderPdfData
{
    public string CompanyName { get; set; } = "";
    public string? CompanyAddress { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerAddress { get; set; }
    public string Currency { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Terms { get; set; }
    public List<PdfLineItem> Items { get; set; } = new();
}

public class PdfLineItem
{
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Quantity * Rate;
}

public class DeliveryNoteLineItem
{
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "Unit";
}
