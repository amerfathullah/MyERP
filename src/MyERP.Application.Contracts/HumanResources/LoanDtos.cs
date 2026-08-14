using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.HumanResources;

public class LoanDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string LoanNumber { get; set; } = null!;
    public int LoanType { get; set; }
    public int InterestMethod { get; set; }
    public int Status { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int TenureMonths { get; set; }
    public int GracePeriodMonths { get; set; }
    public decimal Emi { get; set; }
    public decimal TotalAmountRepaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DateTime? DisbursementDate { get; set; }
    public DateTime? RepaymentStartDate { get; set; }
    public LoanRepaymentScheduleDto[] Schedule { get; set; } = [];
}

public class LoanRepaymentScheduleDto
{
    public DateTime PaymentDate { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal OutstandingBalance { get; set; }
    public bool IsPaid { get; set; }
}

public class CreateLoanDto
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public int LoanType { get; set; }
    public int InterestMethod { get; set; }
    public decimal LoanAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public int TenureMonths { get; set; }
    public int GracePeriodMonths { get; set; }
}

public class DisburseLoanDto
{
    public DateTime DisbursementDate { get; set; }
    public DateTime RepaymentStartDate { get; set; }
}

public class RecordRepaymentDto
{
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
