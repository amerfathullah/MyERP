using System;

namespace MyERP.Core;

public class HierarchyNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
}

public class CreateHierarchyNodeDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public bool IsGroup { get; set; }
    public Guid? ManagerId { get; set; }
}
