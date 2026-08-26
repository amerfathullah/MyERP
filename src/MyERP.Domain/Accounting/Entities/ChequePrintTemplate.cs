using System;
using System.Globalization;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Cheque Print Template — defines visual positioning and dimensions for printing cheques.
/// Maps to ERPNext accounts/doctype/cheque_print_template.
/// </summary>
public class ChequePrintTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string BankName { get; private set; } = null!;
    public ChequeSize ChequeSize { get; set; } = ChequeSize.Regular;
    public decimal StartingPositionFromTopEdge { get; set; }
    public decimal ChequeWidth { get; set; } = ChequePrintTemplateConsts.DefaultChequeWidth;
    public decimal ChequeHeight { get; set; } = ChequePrintTemplateConsts.DefaultChequeHeight;
    public string? ScannedCheque { get; set; }

    public bool IsAccountPayable { get; set; } = true;
    public decimal AccPayDistFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccPayDistTop;
    public decimal AccPayDistFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccPayDistLeft;
    public string? MessageToShow { get; set; } = "Acc. Payee";

    public decimal DateDistFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultDateDistTop;
    public decimal DateDistFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultDateDistLeft;

    public decimal PayerNameFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultPayerNameDistTop;
    public decimal PayerNameFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultPayerNameDistLeft;

    public decimal AmtInWordsFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultAmtInWordsDistTop;
    public decimal AmtInWordsFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultAmtInWordsDistLeft;
    public decimal AmtInWordWidth { get; set; } = ChequePrintTemplateConsts.DefaultAmtInWordWidth;
    public decimal AmtInWordsLineSpacing { get; set; } = ChequePrintTemplateConsts.DefaultAmtInWordsLineSpacing;

    public decimal AmtInFiguresFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultAmtInFiguresDistTop;
    public decimal AmtInFiguresFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultAmtInFiguresDistLeft;

    public decimal AccNoDistFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccNoDistTop;
    public decimal AccNoDistFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccNoDistLeft;

    public decimal SignatoryFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultSignatoryDistTop;
    public decimal SignatoryFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultSignatoryDistLeft;

    public bool HasPrintFormat { get; set; }

    protected ChequePrintTemplate() { }

    public ChequePrintTemplate(
        Guid id,
        string bankName,
        ChequeSize chequeSize = ChequeSize.Regular,
        decimal chequeWidth = ChequePrintTemplateConsts.DefaultChequeWidth,
        decimal chequeHeight = ChequePrintTemplateConsts.DefaultChequeHeight,
        Guid? tenantId = null)
        : base(id)
    {
        SetBankName(bankName);
        ChequeSize = chequeSize;
        ChequeWidth = chequeWidth;
        ChequeHeight = chequeHeight;
        TenantId = tenantId;
    }

    public void SetBankName(string bankName)
    {
        BankName = Check.NotNullOrWhiteSpace(bankName, nameof(bankName), ChequePrintTemplateConsts.MaxBankNameLength);
    }

    /// <summary>
    /// Generates HTML Jinja template matching ERPNext create_or_update_cheque_print_format logic.
    /// </summary>
    public string GenerateHtmlTemplate()
    {
        var topEdge = (ChequeSize == ChequeSize.A4 ? StartingPositionFromTopEdge : 0.0m).ToString("0.00", CultureInfo.InvariantCulture);
        var width = ChequeWidth.ToString("0.00", CultureInfo.InvariantCulture);
        var height = ChequeHeight.ToString("0.00", CultureInfo.InvariantCulture);
        var accPayTop = AccPayDistFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var accPayLeft = AccPayDistFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var msg = !string.IsNullOrWhiteSpace(MessageToShow) ? MessageToShow : "Acc. Payee";
        var dateTop = DateDistFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var dateLeft = DateDistFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var accNoTop = AccNoDistFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var accNoLeft = AccNoDistFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var payerTop = PayerNameFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var payerLeft = PayerNameFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var wordsTop = AmtInWordsFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var wordsLeft = AmtInWordsFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var wordWidth = AmtInWordWidth.ToString("0.00", CultureInfo.InvariantCulture);
        var wordSpacing = AmtInWordsLineSpacing.ToString("0.00", CultureInfo.InvariantCulture);
        var figTop = AmtInFiguresFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var figLeft = AmtInFiguresFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var sigTop = SignatoryFromTopEdge.ToString("0.00", CultureInfo.InvariantCulture);
        var sigLeft = SignatoryFromLeftEdge.ToString("0.00", CultureInfo.InvariantCulture);

        return $@"<style>
    .print-format {{
        padding: 0px;
    }}
    @media screen {{
        .print-format {{
            padding: 0in;
        }}
    }}
</style>
<div style=""position: relative; top:{topEdge}cm"">
    <div style=""width:{width}cm;height:{height}cm;position:relative;border:1px dashed #ccc;"">
        {(IsAccountPayable ? $@"<span style=""top:{accPayTop}cm; left:{accPayLeft}cm; border-bottom: solid 1px;border-top:solid 1px; width:2.5cm;text-align: center; position: absolute;"">
            {msg}
        </span>" : "")}
        <span style=""top:{dateTop}cm; left:{dateLeft}cm; position: absolute;"">
            {{{{ reference_date }}}}
        </span>
        <span style=""top:{accNoTop}cm;left:{accNoLeft}cm; position: absolute; min-width: 6cm;"">
            {{{{ account_no }}}}
        </span>
        <span style=""top:{payerTop}cm;left: {payerLeft}cm; position: absolute; min-width: 6cm;"">
            {{{{ party_name }}}}
        </span>
        <span style=""top:{wordsTop}cm; left:{wordsLeft}cm; position: absolute; display: block; width: {wordWidth}cm; line-height:{wordSpacing}cm; word-wrap: break-word;"">
            {{{{ amount_in_words }}}}
        </span>
        <span style=""top:{figTop}cm;left: {figLeft}cm; position: absolute; min-width: 4cm;"">
            {{{{ amount_in_figures }}}}
        </span>
        <span style=""top:{sigTop}cm;left: {sigLeft}cm; position: absolute; min-width: 6cm;"">
            {{{{ company }}}}
        </span>
    </div>
</div>";
    }
}
