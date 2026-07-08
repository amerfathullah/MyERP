using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class JournalEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string? EntryNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Narration { get; set; }
    public string Status { get; set; } = null!;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
    public bool IsDebit { get; set; }
    public string? Description { get; set; }
}

public class CreateJournalEntryDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid FiscalYearId { get; set; }
    [Required] public DateTime PostingDate { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    [StringLength(500)] public string? Narration { get; set; }
    [Required][MinLength(2)] public List<CreateJournalEntryLineDto> Lines { get; set; } = new();
}

public class CreateJournalEntryLineDto
{
    [Required] public Guid AccountId { get; set; }
    [Required][Range(0.01, double.MaxValue)] public decimal Amount { get; set; }
    [Required] public bool IsDebit { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}
