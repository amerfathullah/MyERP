using System;
using MyERP.Communication.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Communication;

public class CommunicationMediumTests
{
    [Fact]
    public void Should_Create_Valid_CommunicationMedium_And_Manage_Timeslots()
    {
        var id = Guid.NewGuid();
        var catchAllGroupId = Guid.NewGuid();
        var medium = new CommunicationMedium(
            id,
            CommunicationMediumType.Voice,
            "+60312345678",
            catchAllGroupId,
            null,
            false);

        medium.Id.ShouldBe(id);
        medium.CommunicationMediumType.ShouldBe(CommunicationMediumType.Voice);
        medium.CommunicationChannel.ShouldBe("+60312345678");
        medium.CatchAllEmployeeGroupId.ShouldBe(catchAllGroupId);
        medium.IsDisabled.ShouldBeFalse();

        var workHoursGroupId = Guid.NewGuid();
        medium.AddTimeslot(DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), workHoursGroupId);
        medium.Timeslots.Count.ShouldBe(1);

        // Test time routing: inside Monday 10:00 -> workHoursGroupId
        var routed1 = medium.GetHandlingEmployeeGroup(DayOfWeek.Monday, new TimeSpan(10, 0, 0));
        routed1.ShouldBe(workHoursGroupId);

        // Test time routing: outside Monday 19:00 -> catchAllGroupId
        var routed2 = medium.GetHandlingEmployeeGroup(DayOfWeek.Monday, new TimeSpan(19, 0, 0));
        routed2.ShouldBe(catchAllGroupId);

        // Test time routing: Tuesday 10:00 (no timeslot) -> catchAllGroupId
        var routed3 = medium.GetHandlingEmployeeGroup(DayOfWeek.Tuesday, new TimeSpan(10, 0, 0));
        routed3.ShouldBe(catchAllGroupId);
    }

    [Fact]
    public void Should_Throw_When_Timeslot_FromTime_Greater_Or_Equal_ToTime()
    {
        var medium = new CommunicationMedium(Guid.NewGuid(), CommunicationMediumType.Email);
        var groupId = Guid.NewGuid();

        Should.Throw<BusinessException>(() =>
        {
            medium.AddTimeslot(DayOfWeek.Wednesday, new TimeSpan(18, 0, 0), new TimeSpan(9, 0, 0), groupId);
        });
    }
}
