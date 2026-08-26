namespace MyERP.Accounting;

public enum ChequeSize
{
    Regular = 0,
    A4 = 1
}

public static class ChequePrintTemplateConsts
{
    public const int MaxBankNameLength = 100;
    public const int MaxMessageToShowLength = 100;
    public const int MaxScannedChequeLength = 500;

    public const decimal DefaultChequeWidth = 20.00m;
    public const decimal DefaultChequeHeight = 9.00m;
    public const decimal DefaultAccPayDistTop = 1.00m;
    public const decimal DefaultAccPayDistLeft = 9.00m;
    public const decimal DefaultDateDistTop = 1.00m;
    public const decimal DefaultDateDistLeft = 15.00m;
    public const decimal DefaultPayerNameDistTop = 2.00m;
    public const decimal DefaultPayerNameDistLeft = 3.00m;
    public const decimal DefaultAmtInWordsDistTop = 3.00m;
    public const decimal DefaultAmtInWordsDistLeft = 4.00m;
    public const decimal DefaultAmtInWordWidth = 15.00m;
    public const decimal DefaultAmtInWordsLineSpacing = 0.50m;
    public const decimal DefaultAmtInFiguresDistTop = 3.50m;
    public const decimal DefaultAmtInFiguresDistLeft = 16.00m;
    public const decimal DefaultAccNoDistTop = 5.00m;
    public const decimal DefaultAccNoDistLeft = 4.00m;
    public const decimal DefaultSignatoryDistTop = 6.00m;
    public const decimal DefaultSignatoryDistLeft = 15.00m;
}
