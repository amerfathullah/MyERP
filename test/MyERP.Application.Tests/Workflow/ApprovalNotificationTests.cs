using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Notification.Entities;
using MyERP.Workflow.DomainServices;
using MyERP.Workflow.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Workflow;

/// <summary>
/// Regression coverage for a gap found while surveying BusinessNotificationService for unwired
/// methods: NotifyApprovalNeededAsync had zero callers anywhere. Reading ApprovalWorkflowManager
/// confirmed InitiateApprovalAsync and ApproveAndAdvanceAsync both create ApprovalRequest rows but
/// never notified anyone — an approver could only find out by manually polling
/// GetPendingApprovalsAsync. Fixed by notifying the rule's directly-assigned approver, or every
/// active user holding the rule's approver role, whenever a request is created.
/// </summary>
/// <remarks>
/// Act and Assert are two separate WithUnitOfWorkAsync blocks — BusinessNotificationService's
/// Notify* methods insert without autoSave: true (deliberately, since they're normally called
/// mid-way through a larger unit of work), so reading back within the SAME still-open UoW that
/// created them sees nothing until that UoW's automatic SaveChanges runs at block completion.
/// </remarks>
public abstract class ApprovalNotificationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task InitiateApprovalAsync_DirectUserApprover_NotifiesThatUser()
    {
        var approverId = Guid.NewGuid();
        const string docType = "ApprovalNotifTestDocDirect";

        await WithUnitOfWorkAsync(async () =>
        {
            var ruleRepository = GetRequiredService<IRepository<ApprovalRule, Guid>>();
            var manager = GetRequiredService<ApprovalWorkflowManager>();

            await ruleRepository.InsertAsync(
                new ApprovalRule(Guid.NewGuid(), docType, "Direct Approver Rule", level: 1)
                {
                    ApproverUserId = approverId,
                    IsActive = true,
                }, autoSave: true);

            var required = await manager.InitiateApprovalAsync(
                docType, Guid.NewGuid(), requestedByUserId: Guid.NewGuid());
            required.ShouldBeTrue();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var notifications = await notificationRepository.GetListAsync(
                n => n.UserId == approverId && n.SourceDocumentType == docType);
            notifications.ShouldNotBeEmpty();
        });
    }

    [Fact]
    public async Task InitiateApprovalAsync_RoleBasedApprover_NotifiesEveryActiveUserInThatRole()
    {
        const string docType = "ApprovalNotifTestDocRole";
        Guid userId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var ruleRepository = GetRequiredService<IRepository<ApprovalRule, Guid>>();
            var roleRepository = GetRequiredService<IRepository<IdentityRole, Guid>>();
            var userRepository = GetRequiredService<IIdentityUserRepository>();
            var manager = GetRequiredService<ApprovalWorkflowManager>();

            var roleName = $"ApprovalNotifTestRole{Guid.NewGuid():N}"[..24];
            var role = await roleRepository.InsertAsync(new IdentityRole(Guid.NewGuid(), roleName), autoSave: true);

            var user = new IdentityUser(Guid.NewGuid(), $"approver-{Guid.NewGuid():N}", $"approver-{Guid.NewGuid():N}@test.local");
            user.AddRole(role.Id);
            await userRepository.InsertAsync(user, autoSave: true);
            userId = user.Id;

            await ruleRepository.InsertAsync(
                new ApprovalRule(Guid.NewGuid(), docType, "Role-Based Approver Rule", level: 1)
                {
                    ApproverRoleName = roleName,
                    IsActive = true,
                }, autoSave: true);

            await manager.InitiateApprovalAsync(docType, Guid.NewGuid(), requestedByUserId: Guid.NewGuid());
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var notifications = await notificationRepository.GetListAsync(
                n => n.UserId == userId && n.SourceDocumentType == docType);
            notifications.ShouldNotBeEmpty();
        });
    }
}
