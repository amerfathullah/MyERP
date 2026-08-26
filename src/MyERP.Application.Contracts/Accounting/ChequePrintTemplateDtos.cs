using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ChequePrintTemplateDto : FullAuditedEntityDto<Guid>
{
    public string BankName { get; set; } = null!;
    public ChequeSize ChequeSize { get; set; }
    public decimal StartingPositionFromTopEdge { get; set; }
    public decimal ChequeWidth { get; set; }
    public decimal ChequeHeight { get; set; }
    public string? ScannedCheque { get; set; }

    public bool IsAccountPayable { get; set; }
    public decimal AccPayDistFromTopEdge { get; set; }
    public decimal AccPayDistFromLeftEdge { get; set; }
    public string? MessageToShow { get; set; }

    public decimal DateDistFromTopEdge { get; set; }
    public decimal DateDistFromLeftEdge { get; set; }

    public decimal PayerNameFromTopEdge { get; set; }
    public decimal PayerNameFromLeftEdge { get; set; }

    public decimal AmtInWordsFromTopEdge { get; set; }
    public decimal AmtInWordsFromLeftEdge { get; set; }
    public decimal AmtInWordWidth { get; set; }
    public decimal AmtInWordsLineSpacing { get; set; }

    public decimal AmtInFiguresFromTopEdge { get; set; }
    public decimal AmtInFiguresFromLeftEdge { get; set; }

    public decimal AccNoDistFromTopEdge { get; set; }
    public decimal AccNoDistFromLeftEdge { get; set; }

    public decimal SignatoryFromTopEdge { get; set; }
    public decimal SignatoryFromLeftEdge { get; set; }

    public bool HasPrintFormat { get; set; }
}

public class CreateUpdateChequePrintTemplateDto
{
    [Required]
    [StringLength(ChequePrintTemplateConsts.MaxBankNameLength)]
    public string BankName { get; set; } = null!;

    public ChequeSize ChequeSize { get; set; } = ChequeSize.Regular;
    public decimal StartingPositionFromTopEdge { get; set; }
    public decimal ChequeWidth { get; set; } = ChequePrintTemplateConsts.DefaultChequeWidth;
    public decimal ChequeHeight { get; set; } = ChequePrintTemplateConsts.DefaultChequeHeight;

    [StringLength(ChequePrintTemplateConsts.MaxScannedChequeLength)]
    public string? ScannedCheque { get; set; }

    public bool IsAccountPayable { get; set; } = true;
    public decimal AccPayDistFromTopEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccPayDistTop;
    public decimal AccPayDistFromLeftEdge { get; set; } = ChequePrintTemplateConsts.DefaultAccPayDistLeft;

    [StringLength(ChequePrintTemplateConsts.MaxMessageToShowLength)]
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
}

public class GetChequePrintTemplateListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}

public class ChequePrintPreviewDto
{
    public string HtmlContent { get; set; } = null!;
}
