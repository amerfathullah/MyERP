namespace MyERP.HumanResources;

public static class EmployeeConsts
{
    public const int MaxNameLength = 256;
    public const int MaxEmployeeIdLength = 20;
    public const int MaxIcNumberLength = 20;
    public const int MaxPassportLength = 20;
    public const int MaxPhoneLength = 20;
    public const int MaxEmailLength = 256;
    public const int MaxAddressLength = 500;
    public const int MaxBankNameLength = 100;
    public const int MaxBankAccountLength = 30;
    public const int MaxDesignationLength = 100;
    public const int MaxDepartmentLength = 100;
    public const int MaxEpfNumberLength = 20;
    public const int MaxSocsoNumberLength = 20;
    public const int MaxTaxNumberLength = 20;
}

public enum EmploymentStatus
{
    Active = 0,
    Probation = 1,
    OnLeave = 2,
    Resigned = 3,
    Terminated = 4
}

public enum ContributionType
{
    EPF = 0,
    SOCSO = 1,
    EIS = 2,
    PCB = 3
}

public enum CitizenshipType
{
    Malaysian = 0,
    PermanentResident = 1,
    ForeignWorker = 2
}
