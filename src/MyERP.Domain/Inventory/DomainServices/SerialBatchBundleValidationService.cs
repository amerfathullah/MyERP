using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Validates Serial and Batch Bundle (SABB) company isolation on stock movements.
/// Per ERPNext PR #58608: validates that any bundle referenced by a transaction line
/// belongs to the same Company as the parent document.
/// </summary>
public class SerialBatchBundleValidationService : DomainService
{
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;

    public SerialBatchBundleValidationService(IRepository<SerialAndBatchBundle, Guid> bundleRepository)
    {
        _bundleRepository = bundleRepository;
    }

    /// <summary>
    /// Validates that all bundles referenced in the transaction match the expected company.
    /// </summary>
    public async Task ValidateBundleCompanyAsync(
        Guid expectedCompanyId,
        IEnumerable<Guid?> bundleIds,
        bool isInternalTransfer = false)
    {
        var validBundleIds = bundleIds
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (!validBundleIds.Any())
            return;

        var bundles = await _bundleRepository.GetListAsync(b => validBundleIds.Contains(b.Id));
        var bundleMap = bundles.ToDictionary(b => b.Id);

        foreach (var bundleId in validBundleIds)
        {
            if (bundleMap.TryGetValue(bundleId, out var bundle))
            {
                if (bundle.IsCancelled)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                        .WithData("bundleId", bundle.Id)
                        .WithData("reason", $"Serial and Batch Bundle {bundle.Id} is cancelled and cannot be used in a stock transaction");
                }

                if (bundle.CompanyId != expectedCompanyId)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyRestrictionBlocked)
                        .WithData("bundleId", bundle.Id)
                        .WithData("bundleCompanyId", bundle.CompanyId)
                        .WithData("expectedCompanyId", expectedCompanyId)
                        .WithData("reason", $"Serial and Batch Bundle {bundle.Id} belongs to company {bundle.CompanyId}, not {expectedCompanyId}");
                }
            }
        }
    }
}
