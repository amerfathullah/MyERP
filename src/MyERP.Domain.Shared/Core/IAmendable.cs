using System;

namespace MyERP.Core;

/// <summary>
/// Interface for documents that support the cancel-and-amend workflow.
/// Per ERPNext: cancelled documents can be "amended" which creates a new version
/// with an incremented suffix (e.g., SI-001 → SI-001-1) linked via AmendedFrom.
/// The original remains cancelled; the amendment is a new Draft document.
/// </summary>
public interface IAmendable
{
    /// <summary>Reference to the cancelled document this was amended from.</summary>
    Guid? AmendedFromId { get; set; }

    /// <summary>Amendment index (0 = original, 1 = first amendment, etc.).</summary>
    int AmendmentIndex { get; set; }
}
