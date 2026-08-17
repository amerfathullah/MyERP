using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Accounting;

public class ShareTypeDto : EntityDto<Guid>
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
}

public class CreateUpdateShareTypeDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
}

public class ShareBalanceEntryDto
{
    public Guid ShareTypeId { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public int NoOfShares { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public bool IsCompany { get; set; }
    public string? CurrentState { get; set; }
}

public class ShareholderDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public string? FolioNo { get; set; }
    public bool IsCompany { get; set; }
    public List<ShareBalanceEntryDto> ShareBalances { get; set; } = new();
}

public class CreateUpdateShareholderDto
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = null!;
    public string? FolioNo { get; set; }
}

public class ShareTransferDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public int TransferType { get; set; }
    public DateTime Date { get; set; }
    public Guid? FromShareholderId { get; set; }
    public string? FromFolioNo { get; set; }
    public Guid? ToShareholderId { get; set; }
    public string? ToFolioNo { get; set; }
    public Guid ShareTypeId { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public int NoOfShares { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public Guid EquityOrLiabilityAccountId { get; set; }
    public Guid? AssetAccountId { get; set; }
    public string? Remarks { get; set; }
    public int Status { get; set; }
}

public class CreateUpdateShareTransferDto
{
    public Guid CompanyId { get; set; }
    public MyERP.Accounting.ShareTransferType TransferType { get; set; }
    public DateTime Date { get; set; }
    public Guid? FromShareholderId { get; set; }
    public Guid? ToShareholderId { get; set; }
    public Guid ShareTypeId { get; set; }
    public int FromNo { get; set; }
    public int ToNo { get; set; }
    public decimal Rate { get; set; }
    public Guid EquityOrLiabilityAccountId { get; set; }
    public Guid? AssetAccountId { get; set; }
    public string? Remarks { get; set; }
}
