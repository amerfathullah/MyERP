using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Telephony;

public abstract class TelephonyAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITelephonyCallTypeAppService _callTypeAppService;
    private readonly ICallLogAppService _callLogAppService;
    private readonly IIncomingCallSettingsAppService _incomingCallSettingsAppService;

    protected TelephonyAppServiceTests()
    {
        _callTypeAppService = GetRequiredService<ITelephonyCallTypeAppService>();
        _callLogAppService = GetRequiredService<ICallLogAppService>();
        _incomingCallSettingsAppService = GetRequiredService<IIncomingCallSettingsAppService>();
    }

    [Fact]
    public async Task Telephony_Services_Should_Perform_CRUD_And_Lifecycle()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Call Type
            var callType = await _callTypeAppService.CreateAsync(new CreateUpdateTelephonyCallTypeDto
            {
                CallTypeName = "Support Tier 1",
                IsActive = true
            });
            callType.Id.ShouldNotBe(Guid.Empty);
            callType.CallTypeName.ShouldBe("Support Tier 1");

            // Call Log
            var callLog = await _callLogAppService.CreateAsync(new CreateCallLogDto
            {
                CallId = "CALL-TEST-100",
                From = "+60199998888",
                To = "+60355554444",
                CallDirection = CallDirection.Incoming,
                TelephonyCallTypeId = callType.Id,
                Status = CallStatus.Ringing
            });
            callLog.Id.ShouldNotBe(Guid.Empty);
            callLog.Status.ShouldBe(CallStatus.Ringing);

            // Lifecycle transitions
            var started = await _callLogAppService.StartCallAsync(callLog.Id);
            started.Status.ShouldBe(CallStatus.InProgress);

            var completed = await _callLogAppService.CompleteCallAsync(callLog.Id, 95, "https://calls.myerp.com/rec.mp3");
            completed.Status.ShouldBe(CallStatus.Completed);
            completed.Duration.ShouldBe(95);

            // Incoming Call Settings
            var group = Guid.NewGuid();
            var settings = await _incomingCallSettingsAppService.UpdateAsync(new UpdateIncomingCallSettingsDto
            {
                CallRouting = CallRoutingMode.Sequential,
                GreetingMessage = "Thank you for calling MyERP.",
                Schedules = new List<CreateUpdateIncomingCallScheduleDto>
                {
                    new()
                    {
                        DayOfWeek = DayOfWeek.Monday,
                        FromTime = new TimeSpan(9, 0, 0),
                        ToTime = new TimeSpan(17, 0, 0),
                        EmployeeGroupId = group
                    }
                }
            });
            settings.GreetingMessage.ShouldBe("Thank you for calling MyERP.");
            settings.Schedules.Count.ShouldBe(1);

            var activeGroup = await _incomingCallSettingsAppService.GetActiveEmployeeGroupAsync(DayOfWeek.Monday, new TimeSpan(11, 0, 0));
            activeGroup.ShouldBe(group);
        });
    }
}
