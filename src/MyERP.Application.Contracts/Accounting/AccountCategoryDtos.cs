using System;

namespace MyERP.Accounting;

public class AccountCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string RootType { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateAccountCategoryDto
{
    public string Name { get; set; } = null!;
    public string RootType { get; set; } = null!;
    public string? Description { get; set; }
}
