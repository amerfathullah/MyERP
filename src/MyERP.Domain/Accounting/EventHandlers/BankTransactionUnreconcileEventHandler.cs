using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace MyERP.Accounting.EventHandlers;

/// <summary>
/// Cancelling a Payment Entry or Journal Entry that a Bank Transaction was reconciled against
/// must delink the Bank Transaction — otherwise it keeps reporting IsReconciled=true against a
/// cancelled (GL-reversed) voucher forever, and the reconciliation statement never flags it again.
/// Per ERPNext bank_transaction.py on_cancel()/remove_from_bank_transaction().
/// </summary>
public class BankTransactionUnreconcileEventHandler :
    ILocalEventHandler<PaymentEntryCancelledEvent>,
    ILocalEventHandler<JournalEntryCancelledEvent>,
    ITransientDependency
{
    private readonly IRepository<BankTransaction, Guid> _bankTransactionRepository;

    public BankTransactionUnreconcileEventHandler(IRepository<BankTransaction, Guid> bankTransactionRepository)
    {
        _bankTransactionRepository = bankTransactionRepository;
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(PaymentEntryCancelledEvent eventData)
    {
        var query = await _bankTransactionRepository.GetQueryableAsync();
        var linked = query.Where(t => t.PaymentEntryId == eventData.PaymentEntry.Id).ToList();
        foreach (var tx in linked)
        {
            tx.Unreconcile();
            await _bankTransactionRepository.UpdateAsync(tx);
        }
    }

    [UnitOfWork]
    public virtual async Task HandleEventAsync(JournalEntryCancelledEvent eventData)
    {
        var query = await _bankTransactionRepository.GetQueryableAsync();
        var linked = query.Where(t => t.JournalEntryId == eventData.JournalEntry.Id).ToList();
        foreach (var tx in linked)
        {
            tx.Unreconcile();
            await _bankTransactionRepository.UpdateAsync(tx);
        }
    }
}
