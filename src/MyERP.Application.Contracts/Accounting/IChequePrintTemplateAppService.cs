using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IChequePrintTemplateAppService : ICrudAppService<ChequePrintTemplateDto, Guid, GetChequePrintTemplateListDto, CreateUpdateChequePrintTemplateDto, CreateUpdateChequePrintTemplateDto>
{
    Task<ChequePrintPreviewDto> GeneratePreviewAsync(Guid id);
}
