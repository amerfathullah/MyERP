using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

public class DocumentNumberGenerator : DomainService, IDocumentNumberGenerator
{
    private readonly IRepository<DocumentSeries, Guid> _repository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public DocumentNumberGenerator(
        IRepository<DocumentSeries, Guid> repository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _repository = repository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// Generates the next document number. Retries up to 3 times on concurrency conflicts.
    /// When the series has ResetOnFiscalYear=true, includes the fiscal year and resets counter on year change.
    /// postingDate: when provided, used for fiscal year resolution (backdated documents).
    /// </summary>
    public async Task<string> GenerateAsync(string documentType, Guid companyId, DateTime? postingDate = null)
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

                string nextNumber;
                if (series.ResetOnFiscalYear)
                {
                    var currentFy = await GetCurrentFiscalYearAsync(companyId, postingDate);
                    nextNumber = series.GenerateNextNumberForFiscalYear(currentFy);
                }
                else
                {
                    nextNumber = series.GenerateNextNumber();
                }

                await _repository.UpdateAsync(series);
                return nextNumber;
            }
            catch (Exception ex) when (attempt < maxRetries - 1 && IsConcurrencyException(ex))
            {
                await Task.Delay(10 * (attempt + 1));
            }
        }

        throw new BusinessException(MyERPDomainErrorCodes.CompanyNameAlreadyExists)
            .WithData("documentType", documentType);
    }

    private async Task<int> GetCurrentFiscalYearAsync(Guid companyId, DateTime? postingDate = null)
    {
        var targetDate = postingDate ?? DateTime.UtcNow.Date;
        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == companyId &&
            f.StartDate <= targetDate &&
            f.EndDate >= targetDate);

        // If no FY found, fall back to calendar year
        return fy != null ? fy.StartDate.Year : targetDate.Year;
    }

    private static bool IsConcurrencyException(Exception ex)
    {
        return ex.GetType().Name.Contains("DbUpdateConcurrency")
            || ex.GetType().Name.Contains("DbUpdateException")
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true;
    }
}
