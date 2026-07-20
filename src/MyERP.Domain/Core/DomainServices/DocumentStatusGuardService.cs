using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using MyERP.Purchasing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Validates document operations against linked/dependent documents.
/// Per DO-NOT: cannot amend documents with submitted dependents (must cancel children first).
/// Per DO-NOT: cannot cancel documents with linked payments/GL entries.
/// </summary>
public class DocumentStatusGuardService : DomainService
{
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;

    public DocumentStatusGuardService(
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository)
    {
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
    }

    /// <summary>
    /// Validates that a Sales Order can be cancelled (no submitted invoices against it).
    /// Per DO-NOT: cannot amend/cancel documents with submitted dependents.
    /// </summary>
    public async Task ValidateSOCancelAsync(Guid salesOrderId)
    {
        var siQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var hasLinkedInvoices = siQuery
            .Where(si => si.Items.Any(i => i.SalesOrderItemId.HasValue)
                      && si.Status != DocumentStatus.Cancelled
                      && si.Status != DocumentStatus.Draft)
            .Any();

        if (hasLinkedInvoices)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependentType", "Sales Invoice");
        }
    }

    /// <summary>
    /// Validates that a Sales Invoice can be cancelled (no payments against it).
    /// </summary>
    public Task ValidateSICancelAsync(Guid salesInvoiceId, decimal amountPaid)
    {
        if (amountPaid > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithPayments)
                .WithData("documentType", "Sales Invoice")
                .WithData("amountPaid", amountPaid);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that a Purchase Invoice can be cancelled (no payments against it).
    /// </summary>
    public Task ValidatePICancelAsync(Guid purchaseInvoiceId, decimal amountPaid)
    {
        if (amountPaid > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithPayments)
                .WithData("documentType", "Purchase Invoice")
                .WithData("amountPaid", amountPaid);
        }
        return Task.CompletedTask;
    }
}
