namespace MyERP.ImportExport;

public enum ImportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    PartialSuccess = 4
}

public enum ExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}

public static class ImportJobConsts
{
    public const int MaxFileNameLength = 256;
    public const int MaxEntityTypeLength = 64;
    public const int MaxErrorMessageLength = 2048;
}
