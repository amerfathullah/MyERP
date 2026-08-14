using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Sales;

public class InstallationNoteDto : EntityDto<Guid>
{
    public string InstallationNumber { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public DateTime InstallationDate { get; set; }
    public string Status { get; set; } = null!;
    public List<InstallationNoteItemDto> Items { get; set; } = new();
}

public class InstallationNoteItemDto
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public string? SerialNo { get; set; }
}

public class CreateInstallationNoteDto
{
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid DeliveryNoteId { get; set; }
    public DateTime InstallationDate { get; set; }
    public List<InstallationNoteItemDto> Items { get; set; } = new();
}
