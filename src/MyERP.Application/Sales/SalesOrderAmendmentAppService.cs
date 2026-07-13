using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Handles the cancel-and-amend workflow for Sales Orders.
/// Creates a new Draft SO copied from a cancelled SO with incremented amendment suffix.
/// Per DO-NOT: cannot amend documents with submitted dependents.
/// </summary>
[Authorize(MyERPPermissions.SalesOrders.Create)]
public class SalesOrderAmendmentAppService : ApplicationService
{
    private readonly IRepository<SalesOrder, Guid> _repository;
    private readonly DocumentAmendmentService _amendmentService;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public SalesOrderAmendmentAppService(
        IRepository<SalesOrder, Guid> repository,
        DocumentAmendmentService amendmentService,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _amendmentService = amendmentService;
        _numberGenerator = numberGenerator;
    }

    /// <summary>
    /// Creates an amended Sales Order from a cancelled one.
    /// Copies all items and settings into a new Draft SO with amendment reference.
    /// </summary>
    public async Task<Guid> AmendAsync(Guid cancelledOrderId)
    {
        var original = await _repository.GetAsync(cancelledOrderId);

        // Validate: only cancelled documents can be amended
        _amendmentService.ValidateCanAmend(original.Status);

        // Generate amended number
        var newIndex = original.AmendmentIndex + 1;
        var amendedNumber = _amendmentService.GenerateAmendedNumber(original.OrderNumber, newIndex);

        // Create the new order as a copy
        var amended = new SalesOrder(
            GuidGenerator.Create(),
            original.CompanyId,
            original.CustomerId,
            amendedNumber,
            DateTime.UtcNow, // New order date
            original.TenantId);

        amended.AmendedFromId = original.Id;
        amended.AmendmentIndex = newIndex;
        amended.DeliveryDate = original.DeliveryDate;
        amended.CustomerPoNumber = original.CustomerPoNumber;
        amended.CurrencyCode = original.CurrencyCode;
        amended.Terms = original.Terms;
        amended.Notes = $"Amended from {original.OrderNumber}";

        // Copy items
        foreach (var item in original.Items)
        {
            amended.AddItem(item.ItemId, item.Description, item.Quantity, item.UnitPrice, item.TaxAmount, item.Uom);
        }

        await _repository.InsertAsync(amended, autoSave: true);
        return amended.Id;
    }
}
