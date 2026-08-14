namespace MyERP.Core;

/// <summary>Result DTO for document print generation.</summary>
public class DocumentPrintResult
{
    /// <summary>Full HTML content for printing (self-contained with CSS).</summary>
    public string Html { get; set; } = "";

    /// <summary>Suggested filename for download.</summary>
    public string FileName { get; set; } = "";

    /// <summary>Document type label (for display).</summary>
    public string DocumentType { get; set; } = "";
}
