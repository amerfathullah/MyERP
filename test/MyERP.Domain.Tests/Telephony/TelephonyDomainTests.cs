using System;
using MyERP.Telephony.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Telephony;

public class TelephonyDomainTests
{
    [Fact]
    public void Should_Create_TelephonyCallType()
    {
        var id = Guid.NewGuid();
        var callType = new TelephonyCallType(id, "Sales Inquiry", true);

        callType.Id.ShouldBe(id);
        callType.CallTypeName.ShouldBe("Sales Inquiry");
        callType.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Should_Manage_CallLog_Lifecycle()
    {
        var id = Guid.NewGuid();
        var log = new CallLog(
            id,
            "CALL-001",
            "+60123456789",
            "+60388889999",
            CallDirection.Incoming,
            CallStatus.Ringing);

        log.Id.ShouldBe(id);
        log.CallId.ShouldBe("CALL-001");
        log.From.ShouldBe("+60123456789");
        log.To.ShouldBe("+60388889999");
        log.Status.ShouldBe(CallStatus.Ringing);

        log.StartCall();
        log.Status.ShouldBe(CallStatus.InProgress);

        log.CompleteCall(125, "https://storage.myerp.com/calls/rec-001.mp3");
        log.Status.ShouldBe(CallStatus.Completed);
        log.Duration.ShouldBe(125);
        log.RecordingUrl.ShouldBe("https://storage.myerp.com/calls/rec-001.mp3");
    }

    [Fact]
    public void Should_Route_Incoming_Call_By_Schedule()
    {
        var id = Guid.NewGuid();
        var settings = new IncomingCallSettings(id, CallRoutingMode.Sequential, "Welcome to MyERP");

        var groupId = Guid.NewGuid();
        settings.AddSchedule(DayOfWeek.Tuesday, new TimeSpan(8, 30, 0), new TimeSpan(17, 30, 0), groupId);

        var activeGroup = settings.GetActiveEmployeeGroup(DayOfWeek.Tuesday, new TimeSpan(10, 0, 0));
        activeGroup.ShouldBe(groupId);

        var inactiveGroup = settings.GetActiveEmployeeGroup(DayOfWeek.Tuesday, new TimeSpan(18, 0, 0));
        inactiveGroup.ShouldBeNull();
    }
}
