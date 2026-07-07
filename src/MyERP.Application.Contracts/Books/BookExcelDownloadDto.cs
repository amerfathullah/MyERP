namespace MyERP.Books;

public class BookExcelDownloadDto
{
    public string DownloadToken { get; set; } = string.Empty;

    public string? Sorting { get; set; }
}
