using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Periodic "Email Digest" — a scheduled summary email covering open sales orders,
/// overdue invoices, and low-stock items. Maps to ERPNext's Email Digest doctype.
/// Sending itself reuses DocumentEmailService.SendCustomEmailAsync (no new email
/// transport code needed) — this service only computes the digest content and settings.
/// </summary>
[Authorize(MyERPPermissions.Settings.Default)]
public class EmailDigestAppService : ApplicationService, IEmailDigestAppService
{
    private readonly IRepository<EmailDigestSettings, Guid> _settingsRepository;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly DocumentEmailService _emailService;

    public EmailDigestAppService(
        IRepository<EmailDigestSettings, Guid> settingsRepository,
        IRepository<SalesOrder, Guid> salesOrderRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Bin, Guid> binRepository,
        DocumentEmailService emailService)
    {
        _settingsRepository = settingsRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _itemRepository = itemRepository;
        _binRepository = binRepository;
        _emailService = emailService;
    }

    public async Task<EmailDigestSettingsDto> GetSettingsAsync(GetEmailDigestSettingsInput input)
    {
        var settings = await FindAsync(input.CompanyId);
        return ToDto(input.CompanyId, settings);
    }

    [Authorize(MyERPPermissions.Settings.Edit)]
    public async Task<EmailDigestSettingsDto> UpdateSettingsAsync(UpdateEmailDigestSettingsDto input)
    {
        var settings = await FindAsync(input.CompanyId);

        if (settings == null)
        {
            settings = new EmailDigestSettings(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id);
            await ApplyAsync(settings, input);
            await _settingsRepository.InsertAsync(settings);
        }
        else
        {
            await ApplyAsync(settings, input);
            await _settingsRepository.UpdateAsync(settings);
        }

        return ToDto(input.CompanyId, settings);
    }

    /// <summary>
    /// Computes the digest content and emails it to every configured recipient immediately —
    /// the manual "Send Now" trigger. Also updates LastSentAt.
    /// </summary>
    [Authorize(MyERPPermissions.Settings.Edit)]
    public async Task<EmailDigestSendResultDto> SendNowAsync(SendEmailDigestNowInput input)
    {
        var settings = await FindAsync(input.CompanyId);
        var recipients = (settings?.Recipients ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var includeSo = settings?.IncludeOpenSalesOrders ?? true;
        var includeOverdue = settings?.IncludeOverdueInvoices ?? true;
        var includeLowStock = settings?.IncludeLowStockItems ?? true;

        var openSoCount = 0;
        if (includeSo)
        {
            var soQuery = await _salesOrderRepository.GetQueryableAsync();
            openSoCount = soQuery.Count(so => so.CompanyId == input.CompanyId
                && so.Status != Core.DocumentStatus.Draft
                && so.Status != Core.DocumentStatus.Cancelled
                && so.Status != Core.DocumentStatus.Completed);
        }

        var overdueCount = 0;
        var overdueAmount = 0m;
        if (includeOverdue)
        {
            var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
            var candidates = siQuery
                .Where(si => si.CompanyId == input.CompanyId && si.Status == Core.DocumentStatus.Posted && !si.IsReturn)
                .ToList();
            var overdue = candidates.Where(si => si.IsOverdue).ToList();
            overdueCount = overdue.Count;
            overdueAmount = overdue.Sum(si => si.OutstandingAmount);
        }

        var lowStockCount = 0;
        if (includeLowStock)
        {
            var itemQuery = await _itemRepository.GetQueryableAsync();
            var items = itemQuery
                .Where(i => i.CompanyId == input.CompanyId && i.IsActive && i.MaintainStock && i.ReorderLevel > 0)
                .ToList();

            if (items.Count > 0)
            {
                var itemIds = items.Select(i => i.Id).ToList();
                var binQuery = await _binRepository.GetQueryableAsync();
                var stockByItem = binQuery
                    .Where(b => itemIds.Contains(b.ItemId))
                    .GroupBy(b => b.ItemId)
                    .Select(g => new { ItemId = g.Key, Qty = g.Sum(b => b.ActualQty) })
                    .ToDictionary(x => x.ItemId, x => x.Qty);

                lowStockCount = items.Count(i =>
                    (stockByItem.TryGetValue(i.Id, out var qty) ? qty : 0) <= i.ReorderLevel);
            }
        }

        var result = new EmailDigestSendResultDto
        {
            RecipientCount = recipients.Count,
            OpenSalesOrderCount = openSoCount,
            OverdueInvoiceCount = overdueCount,
            OverdueInvoiceAmount = overdueAmount,
            LowStockItemCount = lowStockCount,
        };

        if (recipients.Count > 0)
        {
            var body = BuildDigestHtml(result, includeSo, includeOverdue, includeLowStock);
            foreach (var recipient in recipients)
            {
                await _emailService.SendCustomEmailAsync(recipient, "MyERP — Email Digest", body);
            }
        }

        if (settings != null)
        {
            settings.LastSentAt = DateTime.UtcNow;
            await _settingsRepository.UpdateAsync(settings);
        }

        return result;
    }

    private static string BuildDigestHtml(EmailDigestSendResultDto r, bool includeSo, bool includeOverdue, bool includeLowStock)
    {
        var sections = "";
        if (includeSo) sections += $"<li>Open Sales Orders: <strong>{r.OpenSalesOrderCount}</strong></li>";
        if (includeOverdue) sections += $"<li>Overdue Invoices: <strong>{r.OverdueInvoiceCount}</strong> (RM {r.OverdueInvoiceAmount:N2})</li>";
        if (includeLowStock) sections += $"<li>Low Stock Items: <strong>{r.LowStockItemCount}</strong></li>";
        return $"<p>Here is your ERP summary:</p><ul>{sections}</ul>";
    }

    private async Task<EmailDigestSettings?> FindAsync(Guid companyId)
    {
        var query = await _settingsRepository.GetQueryableAsync();
        return query.FirstOrDefault(s => s.CompanyId == companyId);
    }

    private Task ApplyAsync(EmailDigestSettings settings, UpdateEmailDigestSettingsDto input)
    {
        settings.IsEnabled = input.IsEnabled;
        settings.Frequency = input.Frequency;
        settings.Recipients = input.Recipients;
        settings.IncludeOpenSalesOrders = input.IncludeOpenSalesOrders;
        settings.IncludeOverdueInvoices = input.IncludeOverdueInvoices;
        settings.IncludeLowStockItems = input.IncludeLowStockItems;
        return Task.CompletedTask;
    }

    private static EmailDigestSettingsDto ToDto(Guid companyId, EmailDigestSettings? settings)
    {
        if (settings == null)
            return new EmailDigestSettingsDto
            {
                CompanyId = companyId,
                Frequency = EmailDigestFrequency.Weekly,
                IncludeOpenSalesOrders = true,
                IncludeOverdueInvoices = true,
                IncludeLowStockItems = true,
            };

        return new EmailDigestSettingsDto
        {
            CompanyId = settings.CompanyId,
            IsEnabled = settings.IsEnabled,
            Frequency = settings.Frequency,
            Recipients = settings.Recipients,
            IncludeOpenSalesOrders = settings.IncludeOpenSalesOrders,
            IncludeOverdueInvoices = settings.IncludeOverdueInvoices,
            IncludeLowStockItems = settings.IncludeLowStockItems,
            LastSentAt = settings.LastSentAt,
        };
    }
}
