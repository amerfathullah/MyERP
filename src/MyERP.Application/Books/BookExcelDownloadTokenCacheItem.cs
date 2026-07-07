using System;

namespace MyERP.Books;

[Serializable]
public class BookExcelDownloadTokenCacheItem
{
    public string Token { get; set; } = string.Empty;
}
