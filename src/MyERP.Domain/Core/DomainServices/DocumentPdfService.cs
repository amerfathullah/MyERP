using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Volo.Abp.DependencyInjection;

namespace MyERP.Core.DomainServices;

/// <summary>
/// PDF generation service for document printing.
/// Renders structured document data directly to PDF bytes via QuestPDF (pure .NET, no
/// external binaries/services). Called by DocumentPrintAppService for the "Download PDF"
/// action on document detail pages.
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

public class DocumentPdfService : IDocumentPdfService, ITransientDependency
{
    private static readonly string AccentColor = Colors.Blue.Darken2;
    private static readonly string MutedColor = Colors.Grey.Darken1;
    private static readonly string BorderColor = Colors.Grey.Lighten1;
    private static readonly string HeaderFillColor = Colors.Grey.Lighten4;

    public Task<byte[]> GenerateSalesInvoicePdfAsync(SalesInvoicePdfData d)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Element(header =>
                {
                    header.Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(d.CompanyName).FontSize(16).Bold().FontColor(AccentColor);
                            if (!string.IsNullOrEmpty(d.CompanyTin)) col.Item().Text($"TIN: {d.CompanyTin}").FontSize(8).FontColor(MutedColor);
                            if (!string.IsNullOrEmpty(d.CompanySst)) col.Item().Text($"SST: {d.CompanySst}").FontSize(8).FontColor(MutedColor);
                            if (!string.IsNullOrEmpty(d.CompanyAddress)) col.Item().Text(d.CompanyAddress).FontSize(8).FontColor(MutedColor);
                            if (!string.IsNullOrEmpty(d.CompanyPhone)) col.Item().Text($"Tel: {d.CompanyPhone}").FontSize(8).FontColor(MutedColor);
                        });
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().AlignRight().Text(d.IsReturn ? "CREDIT NOTE" : "TAX INVOICE").FontSize(14).Bold();
                            col.Item().AlignRight().Text(d.InvoiceNumber).FontSize(11).SemiBold();
                            col.Item().AlignRight().Text($"Date: {d.IssueDate:dd/MM/yyyy}").FontSize(9);
                            if (d.DueDate.HasValue) col.Item().AlignRight().Text($"Due: {d.DueDate:dd/MM/yyyy}").FontSize(9);
                        });
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().PaddingTop(8).Column(party =>
                    {
                        party.Item().Text("BILL TO").FontSize(8).FontColor(MutedColor);
                        party.Item().Text(d.CustomerName).Bold();
                        if (!string.IsNullOrEmpty(d.CustomerTin)) party.Item().Text($"TIN: {d.CustomerTin}").FontSize(9);
                        if (!string.IsNullOrEmpty(d.CustomerAddress)) party.Item().Text(d.CustomerAddress).FontSize(9);
                    });

                    col.Item().Element(c => ComposeItemsTable(c, d.Items, d.Currency));

                    col.Item().AlignRight().Width(220).Column(totals =>
                    {
                        AddTotalRow(totals, "Net Total", d.Currency, d.NetTotal);
                        if (d.DiscountAmount > 0) AddTotalRow(totals, "Discount", d.Currency, -d.DiscountAmount);
                        if (d.TaxAmount > 0) AddTotalRow(totals, "Tax", d.Currency, d.TaxAmount);
                        AddGrandTotalRow(totals, d.Currency, d.GrandTotal);
                    });

                    if (!string.IsNullOrEmpty(d.Notes))
                        col.Item().BorderTop(1).BorderColor(BorderColor).PaddingTop(6).Text(d.Notes).FontSize(9);
                    if (!string.IsNullOrEmpty(d.BankDetails))
                        col.Item().Column(bank =>
                        {
                            bank.Item().Text("BANK DETAILS").FontSize(8).FontColor(MutedColor);
                            bank.Item().Text(d.BankDetails).FontSize(9);
                        });

                    col.Item().AlignCenter().PaddingTop(10).Text("Thank you for your business.").Italic().FontColor(MutedColor);
                });

                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GeneratePurchaseOrderPdfAsync(PurchaseOrderPdfData d)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(d.CompanyName).FontSize(16).Bold().FontColor(AccentColor);
                        if (!string.IsNullOrEmpty(d.CompanyAddress)) col.Item().Text(d.CompanyAddress).FontSize(8).FontColor(MutedColor);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("PURCHASE ORDER").FontSize(14).Bold();
                        col.Item().AlignRight().Text(d.OrderNumber).FontSize(11).SemiBold();
                        col.Item().AlignRight().Text($"Date: {d.OrderDate:dd/MM/yyyy}").FontSize(9);
                        if (d.ExpectedDeliveryDate.HasValue) col.Item().AlignRight().Text($"Expected: {d.ExpectedDeliveryDate:dd/MM/yyyy}").FontSize(9);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().PaddingTop(8).Column(party =>
                    {
                        party.Item().Text("SUPPLIER").FontSize(8).FontColor(MutedColor);
                        party.Item().Text(d.SupplierName).Bold();
                        if (!string.IsNullOrEmpty(d.SupplierAddress)) party.Item().Text(d.SupplierAddress).FontSize(9);
                    });

                    col.Item().Element(c => ComposeItemsTable(c, d.Items, d.Currency));

                    col.Item().AlignRight().Width(220).Column(totals => AddGrandTotalRow(totals, d.Currency, d.GrandTotal, "Total"));

                    if (!string.IsNullOrEmpty(d.Terms))
                        col.Item().Column(terms =>
                        {
                            terms.Item().Text("TERMS & CONDITIONS").FontSize(8).FontColor(MutedColor);
                            terms.Item().Text(d.Terms).FontSize(9);
                        });
                });

                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateDeliveryNotePdfAsync(DeliveryNotePdfData d)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text(d.CompanyName).FontSize(16).Bold().FontColor(AccentColor);
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("DELIVERY NOTE").FontSize(14).Bold();
                        col.Item().AlignRight().Text(d.DeliveryNumber).FontSize(11).SemiBold();
                        col.Item().AlignRight().Text($"Date: {d.PostingDate:dd/MM/yyyy}").FontSize(9);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().PaddingTop(8).Column(party =>
                    {
                        party.Item().Text("DELIVER TO").FontSize(8).FontColor(MutedColor);
                        party.Item().Text(d.CustomerName).Bold();
                        if (!string.IsNullOrEmpty(d.ShippingAddress)) party.Item().Text(d.ShippingAddress).FontSize(9);
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(30);
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h, "#");
                            HeaderCell(h, "Description");
                            HeaderCell(h, "Qty", HorizontalAlignment.Right);
                            HeaderCell(h, "UOM", HorizontalAlignment.Right);
                        });

                        for (int i = 0; i < d.Items.Count; i++)
                        {
                            var item = d.Items[i];
                            BodyCell(table, (i + 1).ToString(), HorizontalAlignment.Center);
                            BodyCell(table, item.Description);
                            BodyCell(table, item.Quantity.ToString("N2"), HorizontalAlignment.Right);
                            BodyCell(table, item.Uom, HorizontalAlignment.Right);
                        }
                    });

                    if (!string.IsNullOrEmpty(d.TransporterInfo))
                        col.Item().Text($"Transporter: {d.TransporterInfo}").FontSize(9);
                });

                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateQuotationPdfAsync(QuotationPdfData d)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(d.CompanyName).FontSize(16).Bold().FontColor(AccentColor);
                        if (!string.IsNullOrEmpty(d.CompanyAddress)) col.Item().Text(d.CompanyAddress).FontSize(8).FontColor(MutedColor);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("QUOTATION").FontSize(14).Bold();
                        col.Item().AlignRight().Text(d.QuotationNumber).FontSize(11).SemiBold();
                        col.Item().AlignRight().Text($"Date: {d.TransactionDate:dd/MM/yyyy}").FontSize(9);
                        if (d.ValidTill.HasValue) col.Item().AlignRight().Text($"Valid Till: {d.ValidTill:dd/MM/yyyy}").FontSize(9);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().PaddingTop(8).Column(party =>
                    {
                        party.Item().Text("TO").FontSize(8).FontColor(MutedColor);
                        party.Item().Text(d.PartyName).Bold();
                    });

                    col.Item().Element(c => ComposeItemsTable(c, d.Items, d.Currency));

                    col.Item().AlignRight().Width(220).Column(totals =>
                    {
                        AddTotalRow(totals, "Net Total", d.Currency, d.NetTotal);
                        if (d.TaxAmount > 0) AddTotalRow(totals, "Tax", d.Currency, d.TaxAmount);
                        AddGrandTotalRow(totals, d.Currency, d.GrandTotal);
                    });

                    if (!string.IsNullOrEmpty(d.Terms))
                        col.Item().Column(terms =>
                        {
                            terms.Item().Text("TERMS & CONDITIONS").FontSize(8).FontColor(MutedColor);
                            terms.Item().Text(d.Terms).FontSize(9);
                        });
                });

                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateSalesOrderPdfAsync(SalesOrderPdfData d)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(d.CompanyName).FontSize(16).Bold().FontColor(AccentColor);
                        if (!string.IsNullOrEmpty(d.CompanyAddress)) col.Item().Text(d.CompanyAddress).FontSize(8).FontColor(MutedColor);
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignRight().Text("SALES ORDER").FontSize(14).Bold();
                        col.Item().AlignRight().Text(d.OrderNumber).FontSize(11).SemiBold();
                        col.Item().AlignRight().Text($"Date: {d.OrderDate:dd/MM/yyyy}").FontSize(9);
                        if (d.DeliveryDate.HasValue) col.Item().AlignRight().Text($"Delivery: {d.DeliveryDate:dd/MM/yyyy}").FontSize(9);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().PaddingTop(8).Column(party =>
                    {
                        party.Item().Text("CUSTOMER").FontSize(8).FontColor(MutedColor);
                        party.Item().Text(d.CustomerName).Bold();
                        if (!string.IsNullOrEmpty(d.CustomerAddress)) party.Item().Text(d.CustomerAddress).FontSize(9);
                    });

                    col.Item().Element(c => ComposeItemsTable(c, d.Items, d.Currency));

                    col.Item().AlignRight().Width(220).Column(totals =>
                    {
                        AddTotalRow(totals, "Net Total", d.Currency, d.NetTotal);
                        if (d.TaxAmount > 0) AddTotalRow(totals, "Tax", d.Currency, d.TaxAmount);
                        AddGrandTotalRow(totals, d.Currency, d.GrandTotal);
                    });

                    if (!string.IsNullOrEmpty(d.Terms))
                        col.Item().Column(terms =>
                        {
                            terms.Item().Text("TERMS & CONDITIONS").FontSize(8).FontColor(MutedColor);
                            terms.Item().Text(d.Terms).FontSize(9);
                        });
                });

                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateFromTemplateAsync(string templateName, Dictionary<string, object> data)
    {
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                ConfigurePage(page);
                page.Header().Text(templateName).FontSize(14).Bold();
                page.Content().Column(col =>
                {
                    foreach (var kvp in data)
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(140).Text(kvp.Key).SemiBold();
                            row.RelativeItem().Text(kvp.Value?.ToString() ?? "");
                        });
                    }
                });
                ComposeFooter(page);
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(30);
        page.DefaultTextStyle(x => x.FontSize(9));
    }

    private static void ComposeFooter(PageDescriptor page)
    {
        page.Footer().AlignCenter().Text(x =>
        {
            x.CurrentPageNumber();
            x.Span(" / ");
            x.TotalPages();
        });
    }

    private static void ComposeItemsTable(IContainer container, List<PdfLineItem> items, string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30);
                c.RelativeColumn(4);
                c.RelativeColumn(1);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
            });

            table.Header(h =>
            {
                HeaderCell(h, "#");
                HeaderCell(h, "Description");
                HeaderCell(h, "Qty", HorizontalAlignment.Right);
                HeaderCell(h, "Rate", HorizontalAlignment.Right);
                HeaderCell(h, "Amount", HorizontalAlignment.Right);
            });

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                BodyCell(table, (i + 1).ToString(), HorizontalAlignment.Center);
                BodyCell(table, item.Description);
                BodyCell(table, item.Quantity.ToString("N2"), HorizontalAlignment.Right);
                BodyCell(table, item.Rate.ToString("N2"), HorizontalAlignment.Right);
                BodyCell(table, item.Amount.ToString("N2"), HorizontalAlignment.Right);
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor h, string text, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var cell = h.Cell().Background(HeaderFillColor).Border(1).BorderColor(BorderColor).Padding(4);
        ApplyAligned(cell, text, alignment, bold: true, fontSize: 8);
    }

    private static void BodyCell(TableDescriptor table, string text, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var cell = table.Cell().Border(1).BorderColor(BorderColor).Padding(4);
        ApplyAligned(cell, text, alignment, bold: false, fontSize: 9);
    }

    private static void ApplyAligned(IContainer container, string text, HorizontalAlignment alignment, bool bold, int fontSize)
    {
        var aligned = alignment switch
        {
            HorizontalAlignment.Right => container.AlignRight(),
            HorizontalAlignment.Center => container.AlignCenter(),
            _ => container,
        };
        var span = aligned.Text(text).FontSize(fontSize);
        if (bold) span.Bold();
    }

    private static void AddTotalRow(ColumnDescriptor totals, string label, string currency, decimal amount)
    {
        totals.Item().Row(row =>
        {
            row.RelativeItem().Text(label);
            row.RelativeItem().AlignRight().Text($"{currency} {amount:N2}");
        });
    }

    private static void AddGrandTotalRow(ColumnDescriptor totals, string currency, decimal amount, string label = "Grand Total")
    {
        totals.Item().BorderTop(2).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(label).Bold().FontSize(11);
            row.RelativeItem().AlignRight().Text($"{currency} {amount:N2}").Bold().FontSize(11);
        });
    }
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
