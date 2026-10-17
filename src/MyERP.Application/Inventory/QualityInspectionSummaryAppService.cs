using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.QualityInspections.Default)]
public class QualityInspectionSummaryAppService : ApplicationService, IQualityInspectionSummaryAppService
{
    private readonly IRepository<QualityInspection, Guid> _qiRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public QualityInspectionSummaryAppService(
        IRepository<QualityInspection, Guid> qiRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _qiRepository = qiRepository;
        _itemRepository = itemRepository;
        _userRepository = userRepository;
    }

    public async Task<QualityInspectionSummaryReportDto> GetReportAsync(QualityInspectionSummaryRequestDto input)
    {
        var qiQuery = await _qiRepository.GetQueryableAsync();

        qiQuery = qiQuery.Where(q => q.CompanyId == input.CompanyId && q.DocStatus != DocumentStatus.Cancelled);

        if (input.FromDate.HasValue)
        {
            var fromDate = input.FromDate.Value.Date;
            qiQuery = qiQuery.Where(q => q.InspectionDate >= fromDate);
        }

        if (input.ToDate.HasValue)
        {
            var toDateEnd = input.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            qiQuery = qiQuery.Where(q => q.InspectionDate <= toDateEnd);
        }

        if (input.Status.HasValue)
        {
            qiQuery = qiQuery.Where(q => q.Status == input.Status.Value);
        }

        if (input.ItemId.HasValue)
        {
            qiQuery = qiQuery.Where(q => q.ItemId == input.ItemId.Value);
        }

        if (input.InspectionType.HasValue)
        {
            qiQuery = qiQuery.Where(q => q.InspectionType == input.InspectionType.Value);
        }

        if (input.InspectedById.HasValue)
        {
            qiQuery = qiQuery.Where(q => q.CreatorId == input.InspectedById.Value);
        }

        var inspections = qiQuery
            .OrderByDescending(q => q.InspectionDate)
            .ThenByDescending(q => q.CreationTime)
            .ToList();

        var itemIds = inspections.Select(q => q.ItemId).Distinct().ToList();
        var itemMap = new Dictionary<Guid, Item>();
        if (itemIds.Count > 0)
        {
            var itemQuery = await _itemRepository.GetQueryableAsync();
            var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToList();
            itemMap = items.ToDictionary(i => i.Id, i => i);
        }

        var userIds = inspections.Where(q => q.CreatorId.HasValue).Select(q => q.CreatorId!.Value).Distinct().ToList();
        var userMap = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var userQuery = await _userRepository.GetQueryableAsync();
            var users = userQuery.Where(u => userIds.Contains(u.Id)).ToList();
            userMap = users.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.Name) ? u.UserName : $"{u.Name} ({u.UserName})");
        }

        var total = inspections.Count;
        var accepted = inspections.Count(x => x.Status == InspectionStatus.Accepted);
        var rejected = inspections.Count(x => x.Status == InspectionStatus.Rejected);
        var draft = inspections.Count(x => x.Status == InspectionStatus.Draft);
        var acceptanceRate = total > 0 ? Math.Round((decimal)accepted / total * 100m, 2) : 0m;

        var statusSummaries = new List<QualityInspectionStatusSummaryDto>();
        if (total > 0)
        {
            statusSummaries.Add(new QualityInspectionStatusSummaryDto
            {
                Status = "Accepted",
                Count = accepted,
                Percentage = Math.Round((decimal)accepted / total * 100m, 1)
            });
            statusSummaries.Add(new QualityInspectionStatusSummaryDto
            {
                Status = "Rejected",
                Count = rejected,
                Percentage = Math.Round((decimal)rejected / total * 100m, 1)
            });
            if (draft > 0)
            {
                statusSummaries.Add(new QualityInspectionStatusSummaryDto
                {
                    Status = "Draft",
                    Count = draft,
                    Percentage = Math.Round((decimal)draft / total * 100m, 1)
                });
            }
        }

        var itemSummaries = inspections
            .GroupBy(q => q.ItemId)
            .Select(g =>
            {
                var item = itemMap.GetValueOrDefault(g.Key);
                return new QualityInspectionItemSummaryDto
                {
                    ItemId = g.Key,
                    ItemCode = item?.ItemCode ?? string.Empty,
                    ItemName = item?.ItemName ?? g.First().ItemName ?? string.Empty,
                    TotalInspected = g.Count(),
                    AcceptedCount = g.Count(x => x.Status == InspectionStatus.Accepted),
                    RejectedCount = g.Count(x => x.Status == InspectionStatus.Rejected)
                };
            })
            .OrderByDescending(x => x.TotalInspected)
            .ToList();

        var rows = inspections.Select(q =>
        {
            var item = itemMap.GetValueOrDefault(q.ItemId);
            var inspectorName = q.CreatorId.HasValue && userMap.TryGetValue(q.CreatorId.Value, out var uName)
                ? uName
                : string.Empty;

            return new QualityInspectionSummaryRowDto
            {
                Id = q.Id,
                InspectionNumber = q.InspectionNumber,
                InspectionDate = q.InspectionDate,
                Status = q.Status,
                StatusName = q.Status.ToString(),
                ItemId = q.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? q.ItemName ?? string.Empty,
                SampleSize = q.SampleSize,
                InspectionType = q.InspectionType,
                InspectionTypeName = q.InspectionType.ToString(),
                ReferenceType = q.ReferenceType,
                ReferenceId = q.ReferenceId,
                InspectedById = q.CreatorId,
                InspectedByName = inspectorName,
                Remarks = q.Remarks
            };
        }).ToList();

        return new QualityInspectionSummaryReportDto
        {
            Rows = rows,
            TotalInspections = total,
            AcceptedCount = accepted,
            RejectedCount = rejected,
            DraftCount = draft,
            AcceptanceRate = acceptanceRate,
            StatusSummaries = statusSummaries,
            ItemSummaries = itemSummaries
        };
    }
}
