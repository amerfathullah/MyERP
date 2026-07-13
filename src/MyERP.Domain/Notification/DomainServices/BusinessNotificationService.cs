using System;
using System.Threading.Tasks;
using MyERP.Notification.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace MyERP.Notification.DomainServices;

/// <summary>
/// Business event notification service — creates persistent notifications
/// for key ERP events that require user attention.
/// 
/// Called from AppServices and domain event handlers when business-critical
/// events occur (auto-reorder, credit limit warning, document approval needed, etc.)
/// </summary>
public class BusinessNotificationService : DomainService
{
    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IGuidGenerator _guidGenerator;

    public BusinessNotificationService(
        IRepository<AppNotification, Guid> notificationRepository,
        IGuidGenerator guidGenerator)
    {
        _notificationRepository = notificationRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Notify when auto-reorder creates a Material Request.
    /// Target: Purchase managers / stock controllers.
    /// </summary>
    public async Task NotifyAutoReorderAsync(Guid userId, string itemName, decimal reorderQty, Guid mrId, Guid? tenantId = null)
    {
        var notification = new AppNotification(
            _guidGenerator.Create(), userId, $"Auto-Reorder: {itemName}", tenantId)
        {
            Body = $"Stock has dropped below reorder level. A Material Request for {reorderQty} units has been automatically created.",
            Severity = NotificationSeverity.Warning,
            ActionUrl = $"/purchasing/material-requests/{mrId}",
            SourceDocumentType = "MaterialRequest",
            SourceDocumentId = mrId,
        };

        await _notificationRepository.InsertAsync(notification);
    }

    /// <summary>
    /// Notify when a document requires approval.
    /// Target: The assigned approver.
    /// </summary>
    public async Task NotifyApprovalNeededAsync(Guid userId, string documentType, string documentNumber, Guid documentId, Guid? tenantId = null)
    {
        var notification = new AppNotification(
            _guidGenerator.Create(), userId, $"Approval Required: {documentType} {documentNumber}", tenantId)
        {
            Body = $"A {documentType} ({documentNumber}) has been submitted and requires your approval.",
            Severity = NotificationSeverity.Info,
            ActionUrl = GetDocumentUrl(documentType, documentId),
            SourceDocumentType = documentType,
            SourceDocumentId = documentId,
        };

        await _notificationRepository.InsertAsync(notification);
    }

    /// <summary>
    /// Notify when credit limit is approaching (80%+ utilization).
    /// Target: Sales managers / credit controllers.
    /// </summary>
    public async Task NotifyCreditLimitWarningAsync(Guid userId, string customerName, decimal creditLimit, decimal outstanding, Guid? tenantId = null)
    {
        var utilization = creditLimit > 0 ? Math.Round(outstanding / creditLimit * 100, 1) : 0;
        var notification = new AppNotification(
            _guidGenerator.Create(), userId, $"Credit Limit Warning: {customerName}", tenantId)
        {
            Body = $"Customer '{customerName}' has utilized {utilization}% of their credit limit (Outstanding: {outstanding:N2}, Limit: {creditLimit:N2}).",
            Severity = NotificationSeverity.Warning,
            SourceDocumentType = "Customer",
        };

        await _notificationRepository.InsertAsync(notification);
    }

    /// <summary>
    /// Notify when a payment is received.
    /// Target: Account receivable staff / sales person.
    /// </summary>
    public async Task NotifyPaymentReceivedAsync(Guid userId, string customerName, decimal amount, string currency, Guid paymentId, Guid? tenantId = null)
    {
        var notification = new AppNotification(
            _guidGenerator.Create(), userId, $"Payment Received: {currency} {amount:N2}", tenantId)
        {
            Body = $"Payment of {currency} {amount:N2} received from {customerName}.",
            Severity = NotificationSeverity.Success,
            ActionUrl = $"/accounting/payment-entries/{paymentId}",
            SourceDocumentType = "PaymentEntry",
            SourceDocumentId = paymentId,
        };

        await _notificationRepository.InsertAsync(notification);
    }

    /// <summary>
    /// Notify when stock falls below safety stock level (critical).
    /// Target: Warehouse managers.
    /// </summary>
    public async Task NotifyLowStockAsync(Guid userId, string itemName, decimal currentQty, decimal safetyStock, Guid? tenantId = null)
    {
        var notification = new AppNotification(
            _guidGenerator.Create(), userId, $"Critical Low Stock: {itemName}", tenantId)
        {
            Body = $"Item '{itemName}' is below safety stock level. Current qty: {currentQty:N0}, Safety stock: {safetyStock:N0}.",
            Severity = NotificationSeverity.Error,
            SourceDocumentType = "Item",
        };

        await _notificationRepository.InsertAsync(notification);
    }

    private static string GetDocumentUrl(string documentType, Guid id) => documentType switch
    {
        "SalesOrder" => $"/sales/orders/{id}",
        "PurchaseOrder" => $"/purchasing/orders/{id}",
        "SalesInvoice" => $"/sales/invoices/{id}",
        "PurchaseInvoice" => $"/purchasing/invoices/{id}",
        "ExpenseClaim" => $"/hr/expense-claims/{id}",
        _ => $"/workflow/pending"
    };
}
