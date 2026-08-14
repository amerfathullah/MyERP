using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class CreateUpdateCustomsTariffNumberDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    [StringLength(CustomsTariffNumberConsts.MaxTariffNumberLength)]
    public string TariffNumber { get; set; } = null!;

    [StringLength(CustomsTariffNumberConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}
