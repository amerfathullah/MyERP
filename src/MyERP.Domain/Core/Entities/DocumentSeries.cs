using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Core.Entities;

/// <summary>
/// Document numbering series configuration.
/// Maps to ERPNext's Naming Series concept.
/// Example: "INV-2026-" with CurrentNumber=42 → next document is "INV-2026-00043"
/// </summary>
public class DocumentSeries : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Friendly name (e.g., "Sales Invoice Numbering").</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Document type this series applies to (e.g., "SalesInvoice", "PurchaseOrder").</summary>
    public string DocumentType { get; set; } = null!;

    /// <summary>Prefix for generated numbers (e.g., "INV-", "PO-2026-").</summary>
    public string Prefix { get; set; } = null!;

    /// <summary>Number of digits to pad (e.g., 5 → "00001").</summary>
    public int NumberPadding { get; set; } = 5;

    /// <summary>Current counter value. Next number = CurrentNumber + 1.</summary>
    public long CurrentNumber { get; set; }

    /// <summary>Reset counter per fiscal year.</summary>
    public bool ResetOnFiscalYear { get; set; }

    public bool IsActive { get; set; } = true;

    protected DocumentSeries() { }

    public DocumentSeries(Guid id, Guid companyId, string name, string documentType, string prefix, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SetName(name);
        DocumentType = Check.NotNullOrWhiteSpace(documentType, nameof(documentType), DocumentSeriesConsts.MaxDocumentTypeLength);
        Prefix = Check.NotNullOrWhiteSpace(prefix, nameof(prefix), DocumentSeriesConsts.MaxPrefixLength);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DocumentSeriesConsts.MaxNameLength);
    }

    /// <summary>
    /// Generates the next document number and increments the counter.
    /// Must be called within a transaction/unit of work to prevent duplicates.
    /// </summary>
    public string GenerateNextNumber()
    {
        CurrentNumber++;
        return $"{Prefix}{CurrentNumber.ToString().PadLeft(NumberPadding, '0')}";
    }

    /// <summary>
    /// Generates next number with fiscal year awareness.
    /// When ResetOnFiscalYear is enabled and the year changes, counter resets to 1.
    /// Prefix is dynamically composed with the fiscal year: e.g., "SI-2026-00001"
    /// </summary>
    public string GenerateNextNumberForFiscalYear(int fiscalYear)
    {
        if (!ResetOnFiscalYear)
            return GenerateNextNumber();

        // Check if we need to reset (fiscal year changed since last generation)
        if (_lastFiscalYear != 0 && _lastFiscalYear != fiscalYear)
        {
            CurrentNumber = 0; // Reset on new FY
        }
        _lastFiscalYear = fiscalYear;

        CurrentNumber++;
        // Format: Prefix + FY + separator + padded number (e.g., "SI-2026-00001")
        return $"{Prefix}{fiscalYear}-{CurrentNumber.ToString().PadLeft(NumberPadding, '0')}";
    }

    private int _lastFiscalYear;
}
