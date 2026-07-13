using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Handles the cancel-and-amend workflow for documents.
/// Per ERPNext: cancelled docs can be amended to create a corrected version.
/// Per DO-NOT: cannot amend documents with submitted dependents (must cancel children first).
/// The amendment creates a new Draft copy with AmendedFrom link and incremented suffix.
/// </summary>
public class DocumentAmendmentService : DomainService
{
    /// <summary>
    /// Generates the amended document number by appending/incrementing the suffix.
    /// E.g., "SO-001" → "SO-001-1", "SO-001-1" → "SO-001-2"
    /// </summary>
    public string GenerateAmendedNumber(string originalNumber, int amendmentIndex)
    {
        // Strip existing amendment suffix if present (single digit after last dash)
        var dashIdx = originalNumber.LastIndexOf('-');
        var baseNumber = originalNumber;

        if (dashIdx > 0 && int.TryParse(originalNumber[(dashIdx + 1)..], out var existingSuffix)
            && existingSuffix > 0 && existingSuffix < 100
            && (originalNumber[(dashIdx + 1)..].Length <= 2)) // Amendment suffixes are 1-2 chars
        {
            baseNumber = originalNumber[..dashIdx];
        }

        return $"{baseNumber}-{amendmentIndex}";
    }

    /// <summary>
    /// Validates that a document can be amended.
    /// Allowed statuses: Cancelled (all documents) + Rejected (quotations/lost).
    /// </summary>
    public void ValidateCanAmend(DocumentStatus status)
    {
        if (status != DocumentStatus.Cancelled && status != DocumentStatus.Rejected)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only cancelled or rejected documents can be amended.");
        }
    }
}
