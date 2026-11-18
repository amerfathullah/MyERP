using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.ShareManagement.Default)]
public class ShareReportingAppService : ApplicationService, IShareReportingAppService
{
    private readonly IRepository<Shareholder, Guid> _shareholderRepository;
    private readonly IRepository<ShareTransfer, Guid> _shareTransferRepository;
    private readonly IRepository<ShareType, Guid> _shareTypeRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public ShareReportingAppService(
        IRepository<Shareholder, Guid> shareholderRepository,
        IRepository<ShareTransfer, Guid> shareTransferRepository,
        IRepository<ShareType, Guid> shareTypeRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _shareholderRepository = shareholderRepository;
        _shareTransferRepository = shareTransferRepository;
        _shareTypeRepository = shareTypeRepository;
        _companyRepository = companyRepository;
    }

    public async Task<ShareBalanceReportDto> GetShareBalanceAsync(ShareBalanceRequestDto input)
    {
        if (input.Date == default)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select date");
        }

        var report = new ShareBalanceReportDto
        {
            Date = input.Date.Date,
            ShareholderId = input.ShareholderId,
            CompanyId = input.CompanyId,
        };

        if (!input.ShareholderId.HasValue)
            return report;

        var holder = await _shareholderRepository.FindAsync(input.ShareholderId.Value);
        if (holder == null)
            return report;

        report.ShareholderTitle = holder.Title;

        var endOfDay = input.Date.Date.AddDays(1);
        var query = (await _shareTransferRepository.GetQueryableAsync())
            .Where(t => t.Status == DocumentStatus.Submitted && t.Date < endOfDay);

        if (input.CompanyId.HasValue)
        {
            var companyId = input.CompanyId.Value;
            query = query.Where(t => t.CompanyId == companyId);
        }

        var holderId = holder.Id;
        var holderCompanyId = holder.CompanyId;
        if (holder.IsCompany)
        {
            query = query.Where(t =>
                t.ToShareholderId == holderId ||
                t.FromShareholderId == holderId ||
                ((t.TransferType == ShareTransferType.Issue || t.TransferType == ShareTransferType.Purchase) &&
                 t.CompanyId == holderCompanyId));
        }
        else
        {
            query = query.Where(t => t.ToShareholderId == holderId || t.FromShareholderId == holderId);
        }

        var transfers = await AsyncExecuter.ToListAsync(
            query.OrderBy(t => t.Date).ThenBy(t => t.CreationTime));

        var received = transfers.Where(t => IsReceived(t, holder)).ToList();
        var sent = transfers.Where(t => !IsReceived(t, holder)).ToList();

        var heldRanges = GetHeldRanges(received, sent);
        if (heldRanges.Count == 0)
            return report;

        var shareTypes = await _shareTypeRepository.GetListAsync();
        var shareTypeMap = shareTypes.ToDictionary(st => st.Id, st => st.Title);

        var grouped = new Dictionary<Guid, (int Shares, decimal Amount)>();
        foreach (var (shareTypeId, fromNo, toNo) in heldRanges)
        {
            var lastReceipt = received.LastOrDefault(r =>
                r.ShareTypeId == shareTypeId && r.FromNo <= fromNo && r.ToNo >= toNo);
            if (lastReceipt == null)
                continue;

            var blockShares = toNo - fromNo + 1;
            var blockAmount = blockShares * lastReceipt.Rate;

            if (grouped.TryGetValue(shareTypeId, out var existing))
            {
                grouped[shareTypeId] = (existing.Shares + blockShares, existing.Amount + blockAmount);
            }
            else
            {
                grouped[shareTypeId] = (blockShares, blockAmount);
            }
        }

        foreach (var (shareTypeId, totals) in grouped)
        {
            var avgRate = totals.Shares == 0 ? 0m : Math.Round(totals.Amount / totals.Shares, 4);
            report.Rows.Add(new ShareBalanceRowDto
            {
                ShareholderId = holder.Id,
                ShareholderTitle = holder.Title,
                ShareTypeId = shareTypeId,
                ShareTypeTitle = shareTypeMap.GetValueOrDefault(shareTypeId, shareTypeId.ToString()),
                NoOfShares = totals.Shares,
                AverageRate = avgRate,
                Amount = Math.Round(totals.Amount, 2),
            });
        }

        report.Rows = report.Rows.OrderBy(r => r.ShareTypeTitle).ToList();
        report.TotalNoOfShares = report.Rows.Sum(r => r.NoOfShares);
        report.TotalAmount = Math.Round(report.Rows.Sum(r => r.Amount), 2);
        return report;
    }

    public async Task<ShareLedgerReportDto> GetShareLedgerAsync(ShareLedgerRequestDto input)
    {
        if (input.Date == default)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Please select date");
        }

        var report = new ShareLedgerReportDto
        {
            Date = input.Date.Date,
            ShareholderId = input.ShareholderId,
            CompanyId = input.CompanyId,
        };

        if (!input.ShareholderId.HasValue)
            return report;

        var holder = await _shareholderRepository.FindAsync(input.ShareholderId.Value);
        if (holder == null)
            return report;

        report.ShareholderTitle = holder.Title;

        var endOfDay = input.Date.Date.AddDays(1);
        var holderId = holder.Id;
        var query = (await _shareTransferRepository.GetQueryableAsync())
            .Where(t =>
                t.Status == DocumentStatus.Submitted &&
                t.Date < endOfDay &&
                (t.FromShareholderId == holderId || t.ToShareholderId == holderId));

        if (input.CompanyId.HasValue)
        {
            var companyId = input.CompanyId.Value;
            query = query.Where(t => t.CompanyId == companyId);
        }

        var transfers = await AsyncExecuter.ToListAsync(
            query.OrderBy(t => t.Date).ThenBy(t => t.CreationTime));

        if (transfers.Count == 0)
            return report;

        var shareholderIds = transfers
            .SelectMany(t => new[] { t.FromShareholderId, t.ToShareholderId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var shareholders = await _shareholderRepository.GetListAsync(s => shareholderIds.Contains(s.Id));
        var shareholderMap = shareholders.ToDictionary(s => s.Id, s => s.Title);

        var shareTypes = await _shareTypeRepository.GetListAsync();
        var shareTypeMap = shareTypes.ToDictionary(st => st.Id, st => st.Title);

        var companyIds = transfers.Select(t => t.CompanyId).Distinct().ToList();
        var companies = await _companyRepository.GetListAsync(c => companyIds.Contains(c.Id));
        var companyMap = companies.ToDictionary(c => c.Id, c => c.Name);

        foreach (var transfer in transfers)
        {
            var transferTypeLabel = FormatTransferType(transfer, holderId, shareholderMap);
            report.Rows.Add(new ShareLedgerRowDto
            {
                ShareholderId = holder.Id,
                ShareholderTitle = holder.Title,
                Date = transfer.Date,
                TransferType = transferTypeLabel,
                RawTransferType = transfer.TransferType,
                ShareTypeId = transfer.ShareTypeId,
                ShareTypeTitle = shareTypeMap.GetValueOrDefault(transfer.ShareTypeId, transfer.ShareTypeId.ToString()),
                FromNo = transfer.FromNo,
                ToNo = transfer.ToNo,
                NoOfShares = transfer.NoOfShares,
                Rate = transfer.Rate,
                Amount = transfer.Amount,
                CompanyId = transfer.CompanyId,
                CompanyName = companyMap.GetValueOrDefault(transfer.CompanyId, string.Empty),
                ShareTransferId = transfer.Id,
            });
        }

        report.TotalNoOfShares = report.Rows.Sum(r => r.NoOfShares);
        report.TotalAmount = Math.Round(report.Rows.Sum(r => r.Amount), 2);
        return report;
    }

    private static bool IsReceived(ShareTransfer transfer, Shareholder holder)
    {
        if (holder.IsCompany && transfer.TransferType == ShareTransferType.Issue)
            return true;

        return transfer.ToShareholderId == holder.Id;
    }

    /// <summary>
    /// Computes share number ranges received more often than sent, split at every transfer boundary.
    /// Sweep-line ensures order independence across backdated or out-of-order submitted transfers.
    /// </summary>
    private static List<(Guid ShareTypeId, int FromNo, int ToNo)> GetHeldRanges(
        List<ShareTransfer> received,
        List<ShareTransfer> sent)
    {
        var changes = new SortedDictionary<(Guid ShareTypeId, int ShareNo), int>(
            Comparer<(Guid ShareTypeId, int ShareNo)>.Create((a, b) =>
            {
                var cmp = a.ShareTypeId.CompareTo(b.ShareTypeId);
                return cmp != 0 ? cmp : a.ShareNo.CompareTo(b.ShareNo);
            }));

        void AddDelta(Guid shareTypeId, int shareNo, int delta)
        {
            var key = (shareTypeId, shareNo);
            changes[key] = changes.GetValueOrDefault(key) + delta;
        }

        foreach (var t in received)
        {
            AddDelta(t.ShareTypeId, t.FromNo, 1);
            AddDelta(t.ShareTypeId, t.ToNo + 1, -1);
        }

        foreach (var t in sent)
        {
            AddDelta(t.ShareTypeId, t.FromNo, -1);
            AddDelta(t.ShareTypeId, t.ToNo + 1, 1);
        }

        var heldRanges = new List<(Guid ShareTypeId, int FromNo, int ToNo)>();
        if (changes.Count < 2)
            return heldRanges;

        var keys = changes.Keys.ToList();
        var heldCount = 0;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var current = keys[i];
            var next = keys[i + 1];
            heldCount += changes[current];
            if (heldCount > 0 && current.ShareTypeId == next.ShareTypeId)
            {
                heldRanges.Add((current.ShareTypeId, current.ShareNo, next.ShareNo - 1));
            }
        }

        return heldRanges;
    }

    private static string FormatTransferType(
        ShareTransfer transfer,
        Guid holderId,
        Dictionary<Guid, string> shareholderMap)
    {
        if (transfer.TransferType != ShareTransferType.Transfer)
            return transfer.TransferType.ToString();

        if (transfer.FromShareholderId == holderId)
        {
            var counterparty = transfer.ToShareholderId.HasValue
                ? shareholderMap.GetValueOrDefault(transfer.ToShareholderId.Value, transfer.ToShareholderId.Value.ToString())
                : string.Empty;
            return $"Transfer to {counterparty}";
        }

        var source = transfer.FromShareholderId.HasValue
            ? shareholderMap.GetValueOrDefault(transfer.FromShareholderId.Value, transfer.FromShareholderId.Value.ToString())
            : string.Empty;
        return $"Transfer from {source}";
    }
}
