using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Shareholder — a party (person, entity, or the company itself) that holds share balances.
/// A shareholder with <see cref="IsCompany"/> true represents the company's own treasury of
/// unissued shares, auto-created the first time a Share Transfer of type Issue is submitted.
/// Maps to ERPNext accounts/doctype/shareholder.
/// </summary>
public class Shareholder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = null!;
    public string? FolioNo { get; set; }
    public bool IsCompany { get; set; }

    private readonly List<ShareBalanceEntry> _shareBalances = new();
    public IReadOnlyList<ShareBalanceEntry> ShareBalances => _shareBalances.AsReadOnly();

    protected Shareholder() { }

    public Shareholder(Guid id, Guid companyId, string title, bool isCompany = false, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 200);
        IsCompany = isCompany;
        TenantId = tenantId;
    }

    /// <summary>
    /// Checks whether a share-number range is held by this shareholder for the given share type.
    /// Returns "Complete" (range fully covered by one entry), "Partial" (overlaps one or more
    /// entries without full coverage), or "Outside" (no overlap at all).
    /// Per ERPNext ShareTransfer.share_exists.
    /// </summary>
    public string CheckRangeOwnership(Guid shareTypeId, int fromNo, int toNo)
    {
        foreach (var entry in _shareBalances)
        {
            if (entry.ShareTypeId != shareTypeId || entry.FromNo > toNo || entry.ToNo < fromNo)
                continue;

            if (entry.FromNo <= fromNo && entry.ToNo >= toNo)
                return "Complete";
            if (entry.FromNo <= fromNo && fromNo <= entry.ToNo)
                return "Partial";
            if (entry.FromNo <= toNo && toNo <= entry.ToNo)
                return "Partial";
        }
        return "Outside";
    }

    public ShareBalanceEntry AddShareBalance(Guid shareTypeId, int fromNo, int toNo, decimal rate,
        bool isCompany = false, string? currentState = null)
    {
        var entry = new ShareBalanceEntry(Guid.NewGuid(), Id, shareTypeId, fromNo, toNo, rate, isCompany, currentState);
        _shareBalances.Add(entry);
        return entry;
    }

    /// <summary>
    /// Removes a share-number range from this shareholder's balance for the given share type,
    /// splitting any overlapping entry so only the untouched portions remain.
    /// Per ERPNext ShareTransfer.remove_shares.
    /// </summary>
    public void RemoveShareBalanceRange(Guid shareTypeId, int fromNo, int toNo)
    {
        var current = _shareBalances.ToList();
        _shareBalances.Clear();

        foreach (var entry in current)
        {
            if (entry.ShareTypeId != shareTypeId || entry.FromNo > toNo || entry.ToNo < fromNo)
            {
                _shareBalances.Add(entry);
                continue;
            }

            if (entry.FromNo <= fromNo && entry.ToNo >= toNo)
            {
                // Range fully inside this entry — split into up to two remaining pieces.
                if (entry.FromNo == fromNo)
                {
                    if (entry.ToNo != toNo)
                        AddShareBalance(shareTypeId, toNo + 1, entry.ToNo, entry.Rate, entry.IsCompany, entry.CurrentState);
                }
                else
                {
                    AddShareBalance(shareTypeId, entry.FromNo, fromNo - 1, entry.Rate, entry.IsCompany, entry.CurrentState);
                    if (entry.ToNo != toNo)
                        AddShareBalance(shareTypeId, toNo + 1, entry.ToNo, entry.Rate, entry.IsCompany, entry.CurrentState);
                }
            }
            else if (entry.FromNo >= fromNo && entry.ToNo <= toNo)
            {
                // Entry fully inside the removed range — drop it entirely.
            }
            else if (fromNo <= entry.FromNo && entry.FromNo <= toNo && entry.ToNo >= toNo)
            {
                AddShareBalance(shareTypeId, toNo + 1, entry.ToNo, entry.Rate, entry.IsCompany, entry.CurrentState);
            }
            else if (fromNo <= entry.ToNo && entry.ToNo <= toNo && entry.FromNo <= fromNo)
            {
                AddShareBalance(shareTypeId, entry.FromNo, fromNo - 1, entry.Rate, entry.IsCompany, entry.CurrentState);
            }
            else
            {
                _shareBalances.Add(entry);
            }
        }
    }
}

/// <summary>A contiguous block of shares (FromNo..ToNo) of one type held by a shareholder.</summary>
public class ShareBalanceEntry : Entity<Guid>
{
    public Guid ShareholderId { get; set; }
    public Guid ShareTypeId { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public decimal Rate { get; set; }
    public bool IsCompany { get; set; }
    public string? CurrentState { get; set; }

    public int NoOfShares => ToNo - FromNo + 1;
    public decimal Amount => Rate * NoOfShares;

    protected ShareBalanceEntry() { }

    public ShareBalanceEntry(Guid id, Guid shareholderId, Guid shareTypeId, int fromNo, int toNo,
        decimal rate, bool isCompany = false, string? currentState = null) : base(id)
    {
        ShareholderId = shareholderId;
        ShareTypeId = shareTypeId;
        FromNo = fromNo;
        ToNo = toNo;
        Rate = rate;
        IsCompany = isCompany;
        CurrentState = currentState;
    }

    public override object[] GetKeys() => new object[] { Id };
}
