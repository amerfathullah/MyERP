using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for QualityManagementAppService's Quality Meeting methods: had a fully built
/// backend (Create/Get/GetList/Close, plus real domain-level test coverage on the entity itself) but
/// zero Angular UI at all — every sibling Quality feature (Goals, Reviews, Actions, Procedures,
/// Non-Conformances) had a list+form page; Meetings had none. Added quality-meeting-list/form
/// components; this test covers the AppService layer (mapping, repository round-trip) that domain
/// tests never exercised.
/// </summary>
public abstract class QualityMeetingAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateMeetingAsync_PersistsAgendasAndMinutes()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var meetingAppService = GetRequiredService<IQualityManagementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quality Meeting Test Co"), autoSave: true);

            var created = await meetingAppService.CreateMeetingAsync(new CreateUpdateQualityMeetingDto
            {
                CompanyId = company.Id,
                MeetingDate = DateTime.UtcNow.Date,
                Chairperson = "Dr. Quality Director",
                Attendees = "QA Team, Production Lead",
                Agendas = new() { "Review Q2 Non-Conformance Pareto Chart" },
                Minutes = new()
                {
                    new CreateQualityMeetingMinutesDto { Discussion = "Discussed high defect rate on Line 3." },
                },
            });

            created.Status.ShouldBe(QualityMeetingStatus.Open);
            created.Agendas.ShouldNotBeNull();
            created.Agendas!.Count.ShouldBe(1);
            created.Agendas!.Single().Agenda.ShouldBe("Review Q2 Non-Conformance Pareto Chart");
            created.Minutes.ShouldNotBeNull();
            created.Minutes!.Single().Discussion.ShouldBe("Discussed high defect rate on Line 3.");
        });
    }

    [Fact]
    public async Task CloseMeetingAsync_TransitionsStatusToClosed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var meetingAppService = GetRequiredService<IQualityManagementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Quality Meeting Test Co 2"), autoSave: true);

            var created = await meetingAppService.CreateMeetingAsync(new CreateUpdateQualityMeetingDto
            {
                CompanyId = company.Id,
                MeetingDate = DateTime.UtcNow.Date,
                Chairperson = "Dr. Quality Director",
            });

            var closed = await meetingAppService.CloseMeetingAsync(created.Id);

            closed.Status.ShouldBe(QualityMeetingStatus.Closed);

            var reloaded = await meetingAppService.GetMeetingAsync(created.Id);
            reloaded.Status.ShouldBe(QualityMeetingStatus.Closed);
        });
    }
}
