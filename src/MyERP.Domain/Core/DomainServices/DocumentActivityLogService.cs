using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Users;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Creates audit trail entries for business-level document state transitions.
/// Captures: Submit, Post, Cancel, Amend, Convert, PaymentReceived, WriteOff events.
/// </summary>
public class DocumentActivityLogService : DomainService
{
    private readonly IRepository<DocumentActivityLog, Guid> _repository;
    private readonly ICurrentUser _currentUser;

    public DocumentActivityLogService(
        IRepository<DocumentActivityLog, Guid> repository,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string documentType,
        Guid documentId,
        string activityType,
        Guid companyId,
        string? documentNumber = null,
        string? previousStatus = null,
        string? newStatus = null,
        string? details = null,
        Guid? tenantId = null)
    {
        var log = new DocumentActivityLog(
            GuidGenerator.Create(),
            documentType,
            documentId,
            activityType,
            companyId,
            documentNumber,
            previousStatus,
            newStatus,
            _currentUser.Id,
            details,
            tenantId);

        await _repository.InsertAsync(log);
    }

    public Task LogSubmittedAsync(string documentType, Guid documentId, Guid companyId,
        string? documentNumber = null, Guid? tenantId = null)
        => LogAsync(documentType, documentId, "Submitted", companyId, documentNumber, "Draft", "Submitted", tenantId: tenantId);

    public Task LogPostedAsync(string documentType, Guid documentId, Guid companyId,
        string? documentNumber = null, Guid? tenantId = null)
        => LogAsync(documentType, documentId, "Posted", companyId, documentNumber, "Submitted", "Posted", tenantId: tenantId);

    public Task LogCancelledAsync(string documentType, Guid documentId, Guid companyId,
        string? documentNumber = null, string? previousStatus = null, Guid? tenantId = null)
        => LogAsync(documentType, documentId, "Cancelled", companyId, documentNumber, previousStatus, "Cancelled", tenantId: tenantId);

    public Task LogConvertedAsync(string documentType, Guid documentId, Guid companyId,
        string targetType, Guid targetId, string? documentNumber = null, Guid? tenantId = null)
        => LogAsync(documentType, documentId, "Converted", companyId, documentNumber,
            details: $"Converted to {targetType} ({targetId})", tenantId: tenantId);

    public Task LogPaymentReceivedAsync(string documentType, Guid documentId, Guid companyId,
        decimal amount, string? documentNumber = null, Guid? tenantId = null)
        => LogAsync(documentType, documentId, "PaymentReceived", companyId, documentNumber,
            details: $"Amount: {amount:F2}", tenantId: tenantId);
}
