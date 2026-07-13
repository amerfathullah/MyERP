using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Validates fiscal year closure rules.
/// Per DO-NOT: Skip sequential period closing enforcement (previous FY must be closed first).
/// Ensures FY closure is performed in chronological order.
/// </summary>
public class FiscalYearCloseService : DomainService
{
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public FiscalYearCloseService(IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// Validates that a fiscal year can be closed (all prior FYs must be closed first).
    /// Per DO-NOT: sequential period closing enforcement is mandatory.
    /// </summary>
    public async Task ValidateCanCloseAsync(Guid fiscalYearId)
    {
        var fy = await _fiscalYearRepository.GetAsync(fiscalYearId);
        var query = await _fiscalYearRepository.GetQueryableAsync();

        // Check for any unclosed FY that starts before this one
        var priorUnclosed = query
            .Where(f => f.CompanyId == fy.CompanyId
                     && f.StartDate < fy.StartDate
                     && !f.IsClosed
                     && f.Id != fy.Id)
            .Any();

        if (priorUnclosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.PriorFiscalYearNotClosed)
                .WithData("fiscalYear", fy.Name);
        }
    }

    /// <summary>
    /// Closes a fiscal year after validation.
    /// </summary>
    public async Task CloseAsync(Guid fiscalYearId)
    {
        await ValidateCanCloseAsync(fiscalYearId);

        var fy = await _fiscalYearRepository.GetAsync(fiscalYearId);
        fy.IsClosed = true;
        await _fiscalYearRepository.UpdateAsync(fy);
    }
}
