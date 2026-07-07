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

    public async Task<string> GenerateAsync(string documentType, Guid companyId)
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
}
