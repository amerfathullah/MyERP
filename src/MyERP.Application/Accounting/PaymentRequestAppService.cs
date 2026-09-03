using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentRequestAppService : ApplicationService, IPaymentRequestAppService
{
    private readonly IRepository<PaymentRequest, Guid> _repository;
    public PaymentRequestAppService(IRepository<PaymentRequest, Guid> repository) => _repository = repository;

    public async Task<PagedResultDto<PaymentRequestDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
             query = query.Where(x => x.PartyName != null && x.PartyName.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<PaymentRequestStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var items = query.OrderByDescending(p => p.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<PaymentRequestDto>(totalCount, items.Select(x => ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(x)).ToList());
    }

    public async Task<PaymentRequestDto> GetAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentRequestDto> CreateAsync(CreatePaymentRequestDto input)
    {
        if (input.GrandTotal <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "GrandTotal");
        }

        decimal maxPayable = decimal.MaxValue;
        bool isAlreadyPaid = false;

        // Per ERPNext PR #46626 / commit 913c60d77b: correct payment request amount
        // Outstanding amount / order balance validation
        if (string.Equals(input.ReferenceDoctype, "SalesInvoice", StringComparison.OrdinalIgnoreCase))
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var si = await siRepo.FindAsync(input.ReferenceId);
            if (si != null)
            {
                if (si.OutstandingAmount <= 0)
                {
                    isAlreadyPaid = true;
                }
                maxPayable = si.OutstandingAmount;
            }
        }
        else if (string.Equals(input.ReferenceDoctype, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var pi = await piRepo.FindAsync(input.ReferenceId);
            if (pi != null)
            {
                if (pi.OutstandingAmount <= 0)
                {
                    isAlreadyPaid = true;
                }
                maxPayable = pi.OutstandingAmount;
            }
        }
        else if (string.Equals(input.ReferenceDoctype, "SalesOrder", StringComparison.OrdinalIgnoreCase))
        {
            var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesOrder, Guid>>();
            var so = await soRepo.FindAsync(input.ReferenceId);
            if (so != null)
            {
                // Per ERPNext commit b570d97b4d: convert advance amount based on transaction currency
                var advanceAmount = so.AdvancePaid;
                if (so.ExchangeRate > 0 && so.ExchangeRate != 1m && !string.Equals(so.CurrencyCode, input.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    advanceAmount = Math.Round(so.AdvancePaid / so.ExchangeRate, 2);
                }
                var remainingOrderAmount = so.GrandTotal - advanceAmount;
                if (remainingOrderAmount <= 0)
                {
                    isAlreadyPaid = true;
                }
                maxPayable = Math.Max(0, remainingOrderAmount);
            }
        }
        else if (string.Equals(input.ReferenceDoctype, "PurchaseOrder", StringComparison.OrdinalIgnoreCase))
        {
            var poRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseOrder, Guid>>();
            var po = await poRepo.FindAsync(input.ReferenceId);
            if (po != null)
            {
                // Per ERPNext commit b570d97b4d: convert advance amount based on transaction currency
                var advanceAmount = po.AdvancePaid;
                if (po.ExchangeRate > 0 && po.ExchangeRate != 1m && !string.Equals(po.CurrencyCode, input.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    advanceAmount = Math.Round(po.AdvancePaid / po.ExchangeRate, 2);
                }
                var remainingOrderAmount = po.GrandTotal - advanceAmount;
                if (remainingOrderAmount <= 0)
                {
                    isAlreadyPaid = true;
                }
                maxPayable = Math.Max(0, remainingOrderAmount);
            }
        }

        if (isAlreadyPaid)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvoiceAlreadySettled);
        }

        if (maxPayable != decimal.MaxValue)
        {
            var existingPrQuery = await _repository.GetQueryableAsync();
            var existingPrAmount = existingPrQuery
                .Where(p => p.ReferenceDoctype == input.ReferenceDoctype
                            && p.ReferenceId == input.ReferenceId
                            && p.Status != PaymentRequestStatus.Cancelled
                            && p.Status != PaymentRequestStatus.Paid)
                .Sum(p => p.OutstandingAmount);

            var remainingAllowed = maxPayable - existingPrAmount;
            if (remainingAllowed <= 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.PaymentRequestAlreadyCreated);
            }

            if (input.GrandTotal > remainingAllowed)
            {
                input.GrandTotal = remainingAllowed;
            }
        }

        var isSubscription = input.IsASubscription;
        var subscriptionId = input.SubscriptionId;

        if (!isSubscription && input.ReferenceDoctype == "SalesInvoice")
        {
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var si = await siRepo.FindAsync(input.ReferenceId);
            if (si?.SubscriptionId != null)
            {
                isSubscription = true;
                subscriptionId = si.SubscriptionId;
            }
        }
        else if (!isSubscription && input.ReferenceDoctype == "PurchaseInvoice")
        {
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var pi = await piRepo.FindAsync(input.ReferenceId);
            if (pi?.SubscriptionId != null)
            {
                isSubscription = true;
                subscriptionId = pi.SubscriptionId;
            }
        }
        else if (!isSubscription && input.ReferenceDoctype == "Subscription")
        {
            isSubscription = true;
            subscriptionId = input.ReferenceId;
        }

        var pr = new PaymentRequest(GuidGenerator.Create(), input.CompanyId,
            input.ReferenceDoctype, input.ReferenceId, input.PartyId, input.PartyType,
            input.GrandTotal, CurrentTenant.Id)
        {
            PartyName = input.PartyName, Currency = input.Currency,
            EmailTo = input.EmailTo, Subject = input.Subject, Message = input.Message,
            IsASubscription = isSubscription,
            SubscriptionId = subscriptionId,
        };
        await _repository.InsertAsync(pr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PaymentRequest", pr.Id,
            "Created", pr.CompanyId,
            pr.PartyName ?? pr.Id.ToString()[..8], "Draft", "Draft",
            CurrentUser.Id,
            $"Payment request created for party '{pr.PartyName}' with amount {pr.GrandTotal:C}", CurrentTenant.Id));

        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentRequestDto> SubmitAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        pr.Submit();
        await _repository.UpdateAsync(pr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PaymentRequest", pr.Id,
            "Submitted", pr.CompanyId,
            pr.PartyName ?? pr.Id.ToString()[..8], "Draft", "Requested",
            CurrentUser.Id,
            $"Payment request submitted for party '{pr.PartyName}'", CurrentTenant.Id));

        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Submit)]
    public async Task<PaymentRequestDto> CancelAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        pr.Cancel();
        await _repository.UpdateAsync(pr);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PaymentRequest", pr.Id,
            "Cancelled", pr.CompanyId,
            pr.PartyName ?? pr.Id.ToString()[..8], "Requested", "Cancelled",
            CurrentUser.Id,
            $"Payment request cancelled for party '{pr.PartyName}'", CurrentTenant.Id));

        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    /// <summary>
    /// Creates, submits, and posts a real Payment Entry for this request's full outstanding amount,
    /// then marks the request Paid. Closes the gap where <see cref="PaymentRequest.MarkPaid"/> existed
    /// with zero callers anywhere — a submitted Payment Request had no path to ever reach Paid status,
    /// regardless of whether the customer/supplier actually paid. Full-payment only (no partial/
    /// multi-PE-per-request tracking) — a deliberate MVP scope-down, matching how <c>WriteOffAsync</c>
    /// elsewhere in this codebase also reuses Company defaults instead of a fully general account
    /// picker.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<PaymentRequestDto> PayAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);

        if (pr.Status != PaymentRequestStatus.Initiated)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only an Initiated Payment Request can create a Payment Entry.");

        if (pr.OutstandingAmount <= 0)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "OutstandingAmount");

        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.Company, Guid>>();
        var company = await companyRepo.GetAsync(pr.CompanyId);

        if (!company.DefaultBankAccountId.HasValue)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                .WithData("reason", "No default bank account configured. Set Default Bank Account in Company settings.");

        var isReceive = pr.PaymentRequestType == "Inward";
        Guid partyAccountId;
        if (isReceive)
        {
            if (!company.DefaultReceivableAccountId.HasValue)
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                    .WithData("reason", "No default receivable account configured. Set Default Receivable Account in Company settings.");
            partyAccountId = company.DefaultReceivableAccountId.Value;
        }
        else
        {
            if (!company.DefaultPayableAccountId.HasValue)
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DefaultAccountNotConfigured)
                    .WithData("reason", "No default payable account configured. Set Default Payable Account in Company settings.");
            partyAccountId = company.DefaultPayableAccountId.Value;
        }

        // Same Receive/Pay -> PaidFrom/PaidTo convention as the Angular payment-entry form's own
        // resolveAccounts() (Receive: PaidFrom=Receivable, PaidTo=Bank; Pay: PaidFrom=Bank, PaidTo=Payable).
        var input = new CreatePaymentEntryDto
        {
            CompanyId = pr.CompanyId,
            PaymentType = isReceive ? PaymentType.Receive : PaymentType.Pay,
            PostingDate = DateTime.UtcNow.Date,
            PaidAmount = pr.OutstandingAmount,
            PaidFromAccountId = isReceive ? partyAccountId : company.DefaultBankAccountId.Value,
            PaidToAccountId = isReceive ? company.DefaultBankAccountId.Value : partyAccountId,
            PartyType = pr.PartyType,
            PartyId = pr.PartyId,
            ReferenceNumber = pr.ReferenceNumber,
            PaymentCurrency = pr.Currency,
        };

        if (pr.ReferenceDoctype is "SalesInvoice" or "PurchaseInvoice")
        {
            input.AgainstInvoiceId = pr.ReferenceId;
            input.AgainstInvoiceType = pr.ReferenceDoctype;
        }
        else if (pr.ReferenceDoctype is "SalesOrder" or "PurchaseOrder")
        {
            input.AgainstOrderId = pr.ReferenceId;
            input.AgainstOrderType = pr.ReferenceDoctype;
        }

        var peService = LazyServiceProvider.LazyGetRequiredService<IPaymentEntryAppService>();
        var pe = await peService.CreateAsync(input);
        await peService.SubmitAsync(pe.Id);
        await peService.PostAsync(pe.Id);

        pr.OutstandingAmount = 0;
        pr.MarkPaid(pe.Id);
        await _repository.UpdateAsync(pr, autoSave: true);

        var activityLogRepo2 = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo2.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "PaymentRequest", pr.Id,
            "Paid", pr.CompanyId,
            pr.PartyName ?? pr.Id.ToString()[..8], "Requested", "Paid",
            CurrentUser.Id,
            $"Payment request settled via Payment Entry {pe.PaymentNumber ?? pe.Id.ToString()[..8]}", CurrentTenant.Id));

        return ObjectMapper.Map<PaymentRequest, PaymentRequestDto>(pr);
    }

    /// <summary>
    /// Resends payment link email for an initiated Payment Request (Gotcha #6012).
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Default)]
    public async Task<ResendPaymentEmailResultDto> ResendPaymentEmailAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        if (pr.Status != PaymentRequestStatus.Initiated)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot resend email for a payment request that is not in Initiated status.");
        }

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator?.Create() ?? Guid.NewGuid(), "PaymentRequest", pr.Id,
                "ResentEmail", pr.CompanyId,
                pr.PartyName ?? pr.Id.ToString()[..8], "Initiated", "Initiated",
                CurrentUser?.Id,
                $"Payment link email resent to {pr.EmailTo ?? pr.PartyName}", CurrentTenant?.Id));
        }

        return new ResendPaymentEmailResultDto
        {
            Success = true,
            Message = $"Payment link email successfully resent to {pr.EmailTo ?? pr.PartyName}",
            SentTo = pr.EmailTo
        };
    }

    /// <summary>
    /// Gets comprehensive summary metrics and capability flags for Payment Request (Gotcha #6012).
    /// </summary>
    public async Task<PaymentRequestSummaryDto> GetSummaryAsync(Guid id)
    {
        var pr = await _repository.GetAsync(id);
        return new PaymentRequestSummaryDto
        {
            Id = pr.Id,
            PaymentRequestType = pr.PaymentRequestType,
            ReferenceDoctype = pr.ReferenceDoctype,
            ReferenceId = pr.ReferenceId,
            PartyType = pr.PartyType,
            PartyId = pr.PartyId,
            PartyName = pr.PartyName,
            GrandTotal = pr.GrandTotal,
            OutstandingAmount = pr.OutstandingAmount,
            Currency = pr.Currency,
            Status = (int)pr.Status,
            StatusName = pr.Status.ToString(),
            PaymentUrl = pr.PaymentUrl,
            PaymentGateway = pr.PaymentGateway,
            PaymentEntryId = pr.PaymentEntryId,
            CanPay = pr.Status == PaymentRequestStatus.Initiated && pr.OutstandingAmount > 0,
            CanResendEmail = pr.Status == PaymentRequestStatus.Initiated,
            CanCancel = pr.Status is PaymentRequestStatus.Draft or PaymentRequestStatus.Initiated
        };
    }

    /// <summary>
    /// Resolves subscription details for a document (SalesInvoice, PurchaseInvoice, Subscription) per PR #58438.
    /// Requires read permission on the reference document.
    /// </summary>
    public async Task<List<PaymentRequestSubscriptionPlanDto>> GetSubscriptionDetailsAsync(string referenceDoctype, Guid referenceId)
    {
        Guid? subscriptionId = null;

        if (string.Equals(referenceDoctype, "SalesInvoice", StringComparison.OrdinalIgnoreCase))
        {
            await AuthorizationService.CheckAsync(MyERPPermissions.SalesInvoices.Default);
            var siRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
            var si = await siRepo.FindAsync(referenceId);
            subscriptionId = si?.SubscriptionId;
        }
        else if (string.Equals(referenceDoctype, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase))
        {
            await AuthorizationService.CheckAsync(MyERPPermissions.PurchaseInvoices.Default);
            var piRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
            var pi = await piRepo.FindAsync(referenceId);
            subscriptionId = pi?.SubscriptionId;
        }
        else if (string.Equals(referenceDoctype, "Subscription", StringComparison.OrdinalIgnoreCase))
        {
            await AuthorizationService.CheckAsync(MyERPPermissions.Subscriptions.Default);
            subscriptionId = referenceId;
        }

        if (!subscriptionId.HasValue)
        {
            return new List<PaymentRequestSubscriptionPlanDto>();
        }

        var subRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.Subscription, Guid>>();
        var sub = await subRepo.FindAsync(subscriptionId.Value);
        if (sub == null || !sub.Plans.Any())
        {
            return new List<PaymentRequestSubscriptionPlanDto>();
        }

        return sub.Plans.Select(p => new PaymentRequestSubscriptionPlanDto
        {
            PlanId = p.Id,
            ItemId = p.ItemId,
            ItemName = p.ItemName,
            Qty = p.Qty,
            Rate = p.Rate,
            Amount = p.Amount
        }).ToList();
    }
}
