using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Tax;

public interface ILowerDeductionCertificateAppService : IApplicationService
{
    Task<LowerDeductionCertificateDto> GetAsync(Guid id);
    Task<PagedResultDto<LowerDeductionCertificateDto>> GetListAsync(GetLowerDeductionCertificateListDto input);
    Task<LowerDeductionCertificateDto> CreateAsync(CreateUpdateLowerDeductionCertificateDto input);
    Task<LowerDeductionCertificateDto> UpdateAsync(Guid id, CreateUpdateLowerDeductionCertificateDto input);
    Task DeleteAsync(Guid id);
}
