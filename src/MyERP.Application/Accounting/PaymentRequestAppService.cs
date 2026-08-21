using System;
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

        var pr = new PaymentRequest(GuidGenerator.Create(), input.CompanyId,
            input.ReferenceDoctype, input.ReferenceId, input.PartyId, input.PartyType,
            input.GrandTotal, CurrentTenant.Id)
        {
            PartyName = input.PartyName, Currency = input.Currency,
            EmailTo = input.EmailTo, Subject = input.Subject, Message = input.Message,
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
}
