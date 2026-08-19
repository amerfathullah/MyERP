using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.CRM.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Background job that manages contract expiration, auto-renewals, and renewal reminder notifications.
/// Per ERPNext: contract.update_contract_status (daily scheduler).
/// </summary>
public class ContractExpiryJob : AsyncBackgroundJob<ContractExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<Contract, Guid> _contractRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ContractExpiryJob> _logger;

    public ContractExpiryJob(
        IRepository<Contract, Guid> contractRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IEmailSender emailSender,
        ILogger<ContractExpiryJob> logger)
    {
        _contractRepository = contractRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ContractExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("ContractExpiryJob: Checking contracts for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _contractRepository.GetQueryableAsync();
        var activeContracts = query
            .Where(c => c.CompanyId == args.CompanyId && c.Status == ContractStatus.Active)
            .ToList();

        if (!activeContracts.Any())
            return;

        var expiredCount = 0;
        var expiringContracts = new System.Collections.Generic.List<Contract>();

        foreach (var contract in activeContracts)
        {
            if (!contract.EndDate.HasValue)
                continue;

            // 1. Check if expired
            if (contract.IsExpired(asOfDate))
            {
                if (contract.IsAutoRenewal)
                {
                    // Auto-renew for another 1 year
                    contract.Renew(contract.EndDate.Value.AddYears(1));
                    _logger.LogInformation("ContractExpiryJob: Auto-renewed contract {ContractNumber} until {NewEnd}",
                        contract.ContractNumber, contract.EndDate.Value.ToString("yyyy-MM-dd"));
                }
                else
                {
                    contract.MarkInactive();
                    expiredCount++;
                }

                await _contractRepository.UpdateAsync(contract);
                continue;
            }

            // 2. Check if nearing expiration
            var reminderDays = contract.RenewalReminderDays ?? 30;
            var reminderWindow = asOfDate.AddDays(reminderDays);
            if (contract.EndDate.Value <= reminderWindow)
            {
                expiringContracts.Add(contract);
            }
        }

        // Send alert for expiring contracts
        if (expiringContracts.Any())
        {
            var usersQuery = await _userRepository.GetQueryableAsync();
            var legalOfficers = usersQuery
                .Where(u => u.Email != null && u.Email.Length > 0 && u.IsActive)
                .Take(5)
                .ToList();

            var subject = $"[CONTRACT NOTICE] {expiringContracts.Count} Active Contracts Nearing Expiration";
            var body = $@"<h3>Contract Expiration Alert</h3>
<p>There are {expiringContracts.Count} active contract(s) due to expire within their renewal notice window:</p>
<ul>
{string.Join("", expiringContracts.Select(c => $"<li><strong>{c.ContractNumber} ({c.PartyType}: {c.PartyName ?? c.PartyId.ToString()})</strong> - Expiry: {c.EndDate:yyyy-MM-dd} | Value: {c.CurrencyCode ?? "MYR"} {c.ContractValue:N2} | Auto-Renew: {(c.IsAutoRenewal ? "Yes" : "No")}</li>"))}
</ul>
<p><em>Please initiate renewal discussions or prepare closure documentation in MyERP.</em></p>";

            foreach (var user in legalOfficers)
            {
                try
                {
                    await _emailSender.SendAsync(user.Email, subject, body, isBodyHtml: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ContractExpiryJob: Failed to send contract notice to {Email}", user.Email);
                }
            }
        }

        _logger.LogInformation("ContractExpiryJob: Processed {Total} contracts ({Expired} expired, {Expiring} expiring) for company {CompanyId}",
            activeContracts.Count, expiredCount, expiringContracts.Count, args.CompanyId);
    }
}

public class ContractExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
