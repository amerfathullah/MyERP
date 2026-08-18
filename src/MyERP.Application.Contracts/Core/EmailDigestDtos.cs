using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public class EmailDigestSettingsDto
{
    public Guid CompanyId { get; set; }
    public bool IsEnabled { get; set; }
    public EmailDigestFrequency Frequency { get; set; }
    public string Recipients { get; set; } = string.Empty;
    public bool IncludeOpenSalesOrders { get; set; }
    public bool IncludeOverdueInvoices { get; set; }
    public bool IncludeLowStockItems { get; set; }
    public DateTime? LastSentAt { get; set; }
}

public class UpdateEmailDigestSettingsDto
{
    public Guid CompanyId { get; set; }
    public bool IsEnabled { get; set; }
    public EmailDigestFrequency Frequency { get; set; } = EmailDigestFrequency.Weekly;
    public string Recipients { get; set; } = string.Empty;
    public bool IncludeOpenSalesOrders { get; set; } = true;
    public bool IncludeOverdueInvoices { get; set; } = true;
    public bool IncludeLowStockItems { get; set; } = true;
}

public class GetEmailDigestSettingsInput
{
    public Guid CompanyId { get; set; }
}

public class SendEmailDigestNowInput
{
    public Guid CompanyId { get; set; }
}

public class EmailDigestSendResultDto
{
    public int RecipientCount { get; set; }
    public int OpenSalesOrderCount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal OverdueInvoiceAmount { get; set; }
    public int LowStockItemCount { get; set; }
}

public interface IEmailDigestAppService : IApplicationService
{
    Task<EmailDigestSettingsDto> GetSettingsAsync(GetEmailDigestSettingsInput input);
    Task<EmailDigestSettingsDto> UpdateSettingsAsync(UpdateEmailDigestSettingsDto input);
    Task<EmailDigestSendResultDto> SendNowAsync(SendEmailDigestNowInput input);
}
