using System;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Authors;

public class CreateUpdateAuthorDto
{
    [Required]
    [StringLength(AuthorConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime BirthDate { get; set; } = DateTime.Now;

    [StringLength(AuthorConsts.MaxShortBioLength)]
    public string? ShortBio { get; set; }
}
