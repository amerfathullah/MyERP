using System;

namespace MyERP.Authors;

[Serializable]
public class AuthorExcelDownloadTokenCacheItem
{
    public string Token { get; set; } = string.Empty;
}
