namespace MyERP.Core;

/// <summary>Result DTO for document print generation.</summary>
public class DocumentPrintResult
{
    /// <summary>Real PDF file bytes, for the "Download PDF" action. Serializes as base64 in JSON.</summary>
    public byte[] PdfBytes { get; set; } = System.Array.Empty<byte>();

    /// <summary>Suggested filename for download (includes .pdf extension).</summary>
    public string FileName { get; set; } = "";

    /// <summary>Document type label (for display).</summary>
    public string DocumentType { get; set; } = "";
}
