using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Automation;
using MyERP.Automation.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace MyERP.Automation.EventHandlers;

/// <summary>
/// Listens for domain events on documents and fires the AutomationEngine.
/// Replaces ERPNext hooks.py on_submit/on_cancel pattern with data-driven rules.
/// </summary>
public class DocumentAutomationEventHandler :
    ILocalEventHandler<EntityChangedEventData<SalesInvoice>>,
    ITransientDependency
{
    private readonly AutomationEngine _automationEngine;

    public DocumentAutomationEventHandler(AutomationEngine automationEngine)
    {
        _automationEngine = automationEngine;
    }

    public async Task HandleEventAsync(EntityChangedEventData<SalesInvoice> eventData)
    {
        var invoice = eventData.Entity;

        // Determine trigger from status
        var trigger = invoice.Status switch
        {
            Core.DocumentStatus.Submitted => AutomationTrigger.DocumentSubmitted,
            Core.DocumentStatus.Posted => AutomationTrigger.DocumentPosted,
            Core.DocumentStatus.Cancelled => AutomationTrigger.DocumentCancelled,
            _ => (AutomationTrigger?)null,
        };

        if (trigger == null) return;

        var context = new Dictionary<string, object>
        {
            ["GrandTotal"] = invoice.GrandTotal,
            ["CurrencyCode"] = invoice.CurrencyCode,
            ["CustomerId"] = invoice.CustomerId,
            ["CompanyId"] = invoice.CompanyId,
        };

        if (invoice.CreatorId.HasValue)
            context["_userId"] = invoice.CreatorId.Value;

        await _automationEngine.ExecuteAsync(
            trigger.Value,
            "SalesInvoice",
            invoice.Id,
            invoice.CompanyId,
            invoice.TenantId,
            context);
    }
}
