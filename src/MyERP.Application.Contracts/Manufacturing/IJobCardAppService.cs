using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Manufacturing;

public interface IJobCardAppService : IApplicationService
{
    Task<PagedResultDto<JobCardDto>> GetListAsync(GetJobCardListDto input);
    Task<JobCardDto> GetAsync(Guid id);
    Task<JobCardDto> CreateAsync(CreateJobCardDto input);
    Task<JobCardDto> UpdateAsync(Guid id, CreateJobCardDto input);
    Task<JobCardDto> StartAsync(Guid id);
    Task<JobCardDto> AddTimeLogAsync(Guid id, AddTimeLogDto input);
    Task<JobCardDto> CompleteAsync(Guid id);
    Task<JobCardDto> CancelAsync(Guid id);
    Task<JobCardDto> HoldAsync(Guid id);
    Task<JobCardDto> ResumeAsync(Guid id);
    Task DeleteAsync(Guid id);
}
