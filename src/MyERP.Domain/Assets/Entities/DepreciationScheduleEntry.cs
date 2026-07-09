using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Assets.Entities;

public class DepreciationScheduleEntry : FullAuditedEntity<Guid>
{
    public Guid AssetId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public decimal DepreciationAmount { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public bool IsBooked { get; set; }
    public Guid? JournalEntryId { get; set; }

    protected DepreciationScheduleEntry() { }

    public DepreciationScheduleEntry(Guid id, Guid assetId, DateTime scheduleDate,
        decimal depreciationAmount, decimal accumulatedDepreciation)
        : base(id)
    {
        AssetId = assetId;
        ScheduleDate = scheduleDate;
        DepreciationAmount = depreciationAmount;
        AccumulatedDepreciation = accumulatedDepreciation;
    }

    public void Book(Guid journalEntryId)
    {
        IsBooked = true;
        JournalEntryId = journalEntryId;
    }
}
