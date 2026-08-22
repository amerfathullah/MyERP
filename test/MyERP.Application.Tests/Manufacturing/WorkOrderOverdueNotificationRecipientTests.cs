using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.BackgroundJobs;
using MyERP.Manufacturing.Entities;
using MyERP.Notification.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for a notification-delivery bug found while surveying the Notification
/// module for a fresh backlog item: WorkOrderOverdueNotificationJob (and, identically,
/// PurchaseOrderOverdueAlertJob and UpcomingPaymentDueAlertJob) created every notification with
/// UserId = Guid.Empty — NightlyProcessingWorker's own enqueue call had a comment claiming this was
/// "Resolved at job execution time," but the job never actually resolved it. Since
/// NotificationAppService always filters by CurrentUser.GetId(), and no real user has ID
/// Guid.Empty, every notification these 3 jobs ever created was permanently invisible. Fixed by
/// reusing BatchExpiryAlertJob's already-correct ResolveRecipientsAsync convention (active users
/// with at least one role, via IIdentityUserRepository) — which itself turned out to have a second,
/// independent bug: IdentityUser.Roles comes back null (not empty) without includeDetails: true,
/// so the "working" convention actually threw on first real use too. Fixed both call sites.
/// </summary>
/// <remarks>
/// Deliberately does NOT wrap setup, job execution, and assertions in one shared
/// WithUnitOfWorkAsync — background job classes get their own UnitOfWork interception when
/// resolved through the DI container, and a background job's own transaction did not see this
/// test's just-inserted-but-uncommitted WorkOrder when both were nested inside one outer
/// WithUnitOfWorkAsync (empirically confirmed while writing this test: the job's own copy of the
/// exact same overdue-WO query returned 0 rows while an identical query run directly in the test's
/// UnitOfWork returned 1). Three independent top-level calls, each getting its own implicit
/// UnitOfWork, avoids it — matching the same class of flakiness already documented in
/// DisassemblySourceResolutionTests/StockEntryGlPostingTests for chained AppService calls.
/// </remarks>
public abstract class WorkOrderOverdueNotificationRecipientTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_NotifiesRealActiveUsers_NeverGuidEmpty()
    {
        // Background job classes get no automatic UnitOfWork wrapping (confirmed empirically:
        // calling ExecuteAsync with no ambient UnitOfWork at all threw ObjectDisposedException —
        // the queryable's deferred execution ran after its per-call implicit UoW had already
        // disposed the DbContext). So the job call needs its OWN explicit WithUnitOfWorkAsync,
        // separate from setup — sharing one outer WithUnitOfWorkAsync across setup + job execution
        // was tried and the job's query saw 0 rows for the just-inserted (but not yet actually
        // committed) WorkOrder. Three independent, sequential, REAL commits avoids both failure
        // modes.
        Guid companyId = default;
        Guid? tenantId = null;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "WO Overdue Notif Test Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-OVERDUE", "Overdue Test Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);

            var overdueWo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-OVERDUE-1", fgItem.Id, Guid.NewGuid(), quantity: 10m)
            {
                PlannedEndDate = DateTime.Today.AddDays(-10),
            };
            overdueWo.Submit();
            overdueWo.Start();
            await woRepository.InsertAsync(overdueWo, autoSave: true);

            companyId = company.Id;
            tenantId = company.TenantId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<WorkOrderOverdueNotificationJob>();
            await job.ExecuteAsync(new WorkOrderOverdueNotificationJobArgs
            {
                CompanyId = companyId,
                TenantId = tenantId,
                AsOfDate = DateTime.Today,
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var userRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();

            var notifications = await notificationRepository.GetListAsync(n => n.SourceDocumentType == "WorkOrder");
            notifications.ShouldNotBeEmpty();
            notifications.ShouldAllBe(n => n.UserId != Guid.Empty);

            // Every notified user must actually exist and be active — proves real resolution, not
            // just "any non-empty guid".
            var notifiedUserIds = notifications.Select(n => n.UserId).Distinct().ToList();
            var realActiveUsers = await userRepository.GetListAsync(u => notifiedUserIds.Contains(u.Id) && u.IsActive);
            realActiveUsers.Count.ShouldBe(notifiedUserIds.Count);
        });
    }
}
