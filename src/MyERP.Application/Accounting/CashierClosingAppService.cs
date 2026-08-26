using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.CashierClosings.Default)]
public class CashierClosingAppService : MyERPAppService, ICashierClosingAppService
{
    private readonly IRepository<CashierClosing, Guid> _repository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;

    public CashierClosingAppService(
        IRepository<CashierClosing, Guid> repository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository)
    {
        _repository = repository;
        _salesInvoiceRepository = salesInvoiceRepository;
    }

    public async Task<PagedResultDto<CashierClosingDto>> GetListAsync(CashierClosingGetListInput input)
    {
        var queryable = await _repository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            queryable = queryable.Where(x => x.ClosingNumber.Contains(input.Filter) || x.UserName.Contains(input.Filter));
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.Date >= input.FromDate.Value.Date);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.Date <= input.ToDate.Value.Date);
        }

        if (input.UserId.HasValue)
        {
            queryable = queryable.Where(x => x.UserId == input.UserId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? $"{nameof(CashierClosing.Date)} desc, {nameof(CashierClosing.CreationTime)} desc" : input.Sorting;

        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount));

        var mapper = new CashierClosingMapper();
        return new PagedResultDto<CashierClosingDto>(
            totalCount,
            items.Select(x => mapper.Map(x)).ToList());
    }

    public async Task<CashierClosingDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CashierClosingMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CashierClosings.Create)]
    public async Task<CashierClosingDto> CreateAsync(CreateCashierClosingDto input)
    {
        var closingNumber = $"POS-CLO-{DateTime.UtcNow:yyyyMMdd}-{GuidGenerator.Create().ToString()[..6].ToUpperInvariant()}";
        var currentUserId = CurrentUser.Id ?? Guid.Empty;
        var currentUserName = CurrentUser.UserName ?? "Administrator";

        var entity = new CashierClosing(
            GuidGenerator.Create(),
            closingNumber,
            currentUserId,
            currentUserName,
            input.Date,
            input.FromTime,
            input.ToTime,
            input.Expense,
            input.Custody,
            input.Returns,
            tenantId: CurrentTenant.Id);

        // Fetch shift sales invoice totals
        var shiftTotals = await CalculateShiftTotalsInternalAsync(input.Date, input.FromTime, input.ToTime, currentUserId);
        entity.SetOutstandingAmount(shiftTotals.OutstandingAmount);

        if (input.Payments != null && input.Payments.Count > 0)
        {
            foreach (var p in input.Payments)
            {
                entity.AddPayment(GuidGenerator.Create(), p.ModeOfPayment, p.Amount);
            }
        }
        else
        {
            foreach (var p in shiftTotals.SuggestedPayments)
            {
                entity.AddPayment(GuidGenerator.Create(), p.ModeOfPayment, p.Amount);
            }
        }

        await _repository.InsertAsync(entity);
        return new CashierClosingMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CashierClosings.Edit)]
    public async Task<CashierClosingDto> UpdateAsync(Guid id, UpdateCashierClosingDto input)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:CashierClosing:CannotEditSubmitted", "Cannot edit a submitted Cashier Closing.");
        }

        entity.Date = input.Date.Date;
        entity.FromTime = input.FromTime;
        entity.ToTime = input.ToTime;
        entity.Expense = input.Expense;
        entity.Custody = input.Custody;
        entity.Returns = input.Returns;
        entity.ValidateTimes();

        var shiftTotals = await CalculateShiftTotalsInternalAsync(input.Date, input.FromTime, input.ToTime, entity.UserId);
        entity.SetOutstandingAmount(shiftTotals.OutstandingAmount);

        entity.ClearPayments();
        if (input.Payments != null)
        {
            foreach (var p in input.Payments)
            {
                entity.AddPayment(GuidGenerator.Create(), p.ModeOfPayment, p.Amount);
            }
        }

        await _repository.UpdateAsync(entity);
        return new CashierClosingMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CashierClosings.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:CashierClosing:CannotDeleteSubmitted", "Cannot delete a submitted Cashier Closing.");
        }
        await _repository.DeleteAsync(entity);
    }

    [Authorize(MyERPPermissions.CashierClosings.Submit)]
    public async Task<CashierClosingDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSubmitted)
        {
            throw new BusinessException("MyERP:CashierClosing:AlreadySubmitted", "Cashier Closing is already submitted.");
        }

        entity.Submit();
        await _repository.UpdateAsync(entity);
        return new CashierClosingMapper().Map(entity);
    }

    public async Task<CalculateCashierClosingTotalsResponseDto> CalculateShiftTotalsAsync(CalculateCashierClosingTotalsRequestDto input)
    {
        var userId = input.UserId ?? CurrentUser.Id ?? Guid.Empty;
        return await CalculateShiftTotalsInternalAsync(input.Date, input.FromTime, input.ToTime, userId);
    }

    private async Task<CalculateCashierClosingTotalsResponseDto> CalculateShiftTotalsInternalAsync(
        DateTime date,
        TimeSpan fromTime,
        TimeSpan toTime,
        Guid userId)
    {
        var targetDate = date.Date;
        var invoicesQuery = await _salesInvoiceRepository.GetQueryableAsync();

        var matchingInvoices = await AsyncExecuter.ToListAsync(invoicesQuery
            .Where(x => x.IssueDate == targetDate));

        if (userId != Guid.Empty)
        {
            matchingInvoices = matchingInvoices.Where(x => x.CreatorId == userId).ToList();
        }

        // Filter by time if available
        var filtered = matchingInvoices.Where(x =>
            x.CreationTime.TimeOfDay >= fromTime && x.CreationTime.TimeOfDay <= toTime).ToList();

        var outstanding = filtered.Sum(x => x.OutstandingAmount);

        var totalPaid = filtered.Sum(x => x.AmountPaid);
        var suggestedPayments = new List<CreateUpdateCashierClosingPaymentDto>();
        if (totalPaid > 0)
        {
            suggestedPayments.Add(new CreateUpdateCashierClosingPaymentDto
            {
                ModeOfPayment = "Cash",
                Amount = totalPaid
            });
        }

        var result = new CalculateCashierClosingTotalsResponseDto
        {
            OutstandingAmount = outstanding,
            SuggestedPayments = suggestedPayments
        };

        return result;
    }
}
