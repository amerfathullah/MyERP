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
    /// Supports ERPNext naming tokens in Prefix:
    ///   .YYYY. = 4-digit year, .YY. = 2-digit year, .MM. = month,
    ///   .DD. = day, .FY. = fiscal year short (e.g., "2526"),
    ///   .ABBR. = company abbreviation, .#### = number padding (# count = digits).
    /// </summary>
    public string GenerateNextNumber()
    {
        CurrentNumber++;
        return ResolveTemplate(Prefix, CurrentNumber, null, null);
    }

    /// <summary>
    /// Generates next number with fiscal year awareness.
    /// When ResetOnFiscalYear is enabled and the year changes, counter resets to 1.
    /// </summary>
    public string GenerateNextNumberForFiscalYear(int fiscalYear)
    {
        if (!ResetOnFiscalYear)
            return GenerateNextNumber();

        if (_lastFiscalYear != 0 && _lastFiscalYear != fiscalYear)
        {
            CurrentNumber = 0; // Reset on new FY
        }
        _lastFiscalYear = fiscalYear;

        CurrentNumber++;
        return ResolveTemplate(Prefix, CurrentNumber, fiscalYear, null);
    }

    /// <summary>
    /// Resolves naming tokens in the template pattern.
    /// Per ERPNext settings-configuration.instructions.md:
    /// 9 tokens: FY, TFY, ABBR, MM, DD, YY, YYYY, JJJ (Julian day), WW (week number).
    /// Token format: .TOKEN. (dot-wrapped) or #### (hash = digit placeholders).
    /// </summary>
    private string ResolveTemplate(string template, long number, int? fiscalYear, string? companyAbbr)
    {
        var now = DateTime.UtcNow;
        var result = template;

        // Replace naming tokens (dot-wrapped)
        result = result.Replace(".YYYY.", now.Year.ToString("D4"));
        result = result.Replace(".YY.", (now.Year % 100).ToString("D2"));
        result = result.Replace(".MM.", now.Month.ToString("D2"));
        result = result.Replace(".DD.", now.Day.ToString("D2"));
        result = result.Replace(".JJJ.", now.DayOfYear.ToString("D3"));
        result = result.Replace(".WW.", System.Globalization.ISOWeek.GetWeekOfYear(now).ToString("D2"));

        if (fiscalYear.HasValue)
        {
            // .FY. = short fiscal year e.g., "2526" for Apr 2025 - Mar 2026
            var fyShort = $"{(fiscalYear.Value % 100):D2}{((fiscalYear.Value + 1) % 100):D2}";
            result = result.Replace(".FY.", fyShort);
            // .TFY. = two-digit fiscal year start
            result = result.Replace(".TFY.", (fiscalYear.Value % 100).ToString("D2"));
        }
        else
        {
            result = result.Replace(".FY.", now.Year.ToString("D4"));
            result = result.Replace(".TFY.", (now.Year % 100).ToString("D2"));
        }

        if (!string.IsNullOrEmpty(companyAbbr))
        {
            result = result.Replace(".ABBR.", companyAbbr);
        }

        // Handle #### hash patterns — count consecutive # characters for padding
        if (result.Contains('#'))
        {
            var hashStart = result.IndexOf('#');
            var hashEnd = hashStart;
            while (hashEnd < result.Length && result[hashEnd] == '#') hashEnd++;
            var padding = hashEnd - hashStart;
            result = result[..hashStart] + number.ToString().PadLeft(padding, '0') + result[hashEnd..];
        }
        else
        {
            // No hash pattern — append padded number at end
            result += number.ToString().PadLeft(NumberPadding, '0');
        }

        return result;
    }

    private int _lastFiscalYear;
}
