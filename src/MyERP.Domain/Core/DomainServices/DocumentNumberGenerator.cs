using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

public class DocumentNumberGenerator : DomainService, IDocumentNumberGenerator
{
    private readonly IRepository<DocumentSeries, Guid> _repository;

    public DocumentNumberGenerator(IRepository<DocumentSeries, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Generates the next document number. Retries up to 3 times on concurrency conflicts
    /// (when two transactions increment the same DocumentSeries simultaneously).
    /// The unique DB index on document numbers is the final safety net.
    /// </summary>
    public async Task<string> GenerateAsync(string documentType, Guid companyId)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var series = await _repository.FindAsync(s =>
                    s.DocumentType == documentType &&
                    s.CompanyId == companyId &&
                    s.IsActive);

                if (series == null)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.CompanyNameAlreadyExists)
                        .WithData("documentType", documentType);
                }

                var nextNumber = series.GenerateNextNumber();
                await _repository.UpdateAsync(series);

                return nextNumber;
            }
            catch (Exception ex) when (attempt < maxRetries - 1 && IsConcurrencyException(ex))
            {
                // Retry on concurrency conflict (optimistic concurrency or unique violation)
                await Task.Delay(10 * (attempt + 1)); // Brief backoff
            }
        }

        // Should not reach here, but final attempt throws naturally
        throw new BusinessException(MyERPDomainErrorCodes.CompanyNameAlreadyExists)
            .WithData("documentType", documentType);
    }

    private static bool IsConcurrencyException(Exception ex)
    {
        // Check for EF Core concurrency exception or PostgreSQL unique violation
        return ex.GetType().Name.Contains("DbUpdateConcurrency")
            || ex.GetType().Name.Contains("DbUpdateException")
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true;
    }
}
