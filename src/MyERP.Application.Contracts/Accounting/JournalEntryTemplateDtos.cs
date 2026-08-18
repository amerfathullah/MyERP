using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class JournalEntryTemplateLineDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public bool IsDebit { get; set; }
    public decimal DefaultAmount { get; set; }
    public string? PartyType { get; set; }
    public string? Description { get; set; }
}

public class JournalEntryTemplateDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string TemplateName { get; set; } = null!;
    public JournalEntryVoucherType VoucherType { get; set; }
    public bool IsActive { get; set; }
    public List<JournalEntryTemplateLineDto> Lines { get; set; } = new();
}

public class CreateJournalEntryTemplateLineDto
{
    [Required]
    public Guid AccountId { get; set; }
    public bool IsDebit { get; set; }
    public decimal DefaultAmount { get; set; }
    public string? PartyType { get; set; }
    public string? Description { get; set; }
}

public class CreateUpdateJournalEntryTemplateDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(200)]
    public string TemplateName { get; set; } = null!;

    public JournalEntryVoucherType VoucherType { get; set; } = JournalEntryVoucherType.JournalEntry;
    public bool IsActive { get; set; } = true;

    [MinLength(1)]
    public List<CreateJournalEntryTemplateLineDto> Lines { get; set; } = new();
}

public class GetJournalEntryTemplateListDto : PagedAndSortedResultRequestDto
{
    public Guid? CompanyId { get; set; }
}

public interface IJournalEntryTemplateAppService : IApplicationService
{
    Task<JournalEntryTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<JournalEntryTemplateDto>> GetListAsync(GetJournalEntryTemplateListDto input);
    Task<JournalEntryTemplateDto> CreateAsync(CreateUpdateJournalEntryTemplateDto input);
    Task<JournalEntryTemplateDto> UpdateAsync(Guid id, CreateUpdateJournalEntryTemplateDto input);
    Task DeleteAsync(Guid id);
}
