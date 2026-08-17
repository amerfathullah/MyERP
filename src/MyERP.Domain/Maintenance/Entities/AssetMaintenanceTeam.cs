using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Maintenance.Entities;

/// <summary>
/// Asset Maintenance Team — named roster of employees responsible for asset maintenance,
/// referenced by <see cref="AssetMaintenance.MaintenanceTeamId"/>. Maps to ERPNext
/// assets/doctype/asset_maintenance_team.
/// </summary>
public class AssetMaintenanceTeam : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string TeamName { get; set; } = null!;
    public Guid? MaintenanceManagerId { get; set; }

    private readonly List<AssetMaintenanceTeamMember> _members = new();
    public IReadOnlyList<AssetMaintenanceTeamMember> Members => _members.AsReadOnly();

    protected AssetMaintenanceTeam() { }

    public AssetMaintenanceTeam(Guid id, Guid companyId, string teamName, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TeamName = Check.NotNullOrWhiteSpace(teamName, nameof(teamName), 140);
        TenantId = tenantId;
    }

    public void SetMembers(IEnumerable<(Guid EmployeeId, string? MaintenanceRole)> members)
    {
        _members.Clear();
        foreach (var (employeeId, role) in members)
            _members.Add(new AssetMaintenanceTeamMember(Guid.NewGuid(), Id, employeeId, role));
    }
}

public class AssetMaintenanceTeamMember : FullAuditedEntity<Guid>
{
    public Guid AssetMaintenanceTeamId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? MaintenanceRole { get; set; }

    protected AssetMaintenanceTeamMember() { }

    public AssetMaintenanceTeamMember(Guid id, Guid assetMaintenanceTeamId, Guid employeeId, string? maintenanceRole) : base(id)
    {
        AssetMaintenanceTeamId = assetMaintenanceTeamId;
        EmployeeId = employeeId;
        MaintenanceRole = maintenanceRole;
    }
}
