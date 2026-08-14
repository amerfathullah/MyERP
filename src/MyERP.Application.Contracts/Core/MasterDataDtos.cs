using System;

namespace MyERP.Core;

public class ItemGroupLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class ModeOfPaymentLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
}

public class CostCenterLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsGroup { get; set; }
    public Guid? ParentId { get; set; }
}

public class PaymentTermsLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}
