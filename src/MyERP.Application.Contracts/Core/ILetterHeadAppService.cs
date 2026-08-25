using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface ILetterHeadAppService : IApplicationService
{
    Task<LetterHeadDto> GetAsync(Guid id);
    Task<PagedResultDto<LetterHeadDto>> GetListAsync(GetLetterHeadListDto input);
    Task<LetterHeadDto> CreateAsync(CreateUpdateLetterHeadDto input);
    Task<LetterHeadDto> UpdateAsync(Guid id, CreateUpdateLetterHeadDto input);
    Task<LetterHeadDto> SetDefaultAsync(Guid id);
    Task DeleteAsync(Guid id);
}
