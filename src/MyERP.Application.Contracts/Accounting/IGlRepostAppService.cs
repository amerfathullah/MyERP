using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IGlRepostAppService : IApplicationService
{
    Task<GlRepostResultDto> RepostAsync(RepostGlDto input);
    Task<GlRepostResultDto> RepostBatchAsync(RepostBatchGlDto input);
}
