namespace MyERP.HumanResources;

/// <summary>
/// Status of a leave application.
/// </summary>
public enum LeaveApplicationStatus
{
    Open = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}

/// <summary>
/// Type of salary component: Earning or Deduction.
/// </summary>
public enum SalaryComponentType
{
    Earning = 0,
    Deduction = 1,
}

/// <summary>
/// Daily attendance status.
/// </summary>
public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    HalfDay = 2,
    OnLeave = 3,
}

/// <summary>
/// Employee shift assignment status.
/// </summary>
public enum ShiftAssignmentStatus
{
    Active = 0,
    Inactive = 1,
}
