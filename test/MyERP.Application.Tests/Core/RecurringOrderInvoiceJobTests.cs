using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.BackgroundJobs;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Core;

/// <summary>
/// Regression coverage for a gap where the Auto Repeat creation form offered SalesInvoice,
/// PurchaseInvoice, JournalEntry, SalesOrder and PurchaseOrder, but only RecurringInvoiceJob
/// (SalesInvoice) and RecurringJournalEntryJob (JournalEntry) ever existed and were enqueued by
/// NightlyProcessingWorker — PurchaseInvoice/SalesOrder/PurchaseOrder auto-repeats saved
/// successfully but silently never generated anything. Added RecurringPurchaseInvoiceJob,
/// RecurringSalesOrderJob and RecurringPurchaseOrderJob mirroring RecurringInvoiceJob's pattern.
/// </summary>
public abstract class RecurringOrderInvoiceJobTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task RecurringPurchaseOrderJob_CreatesDraftCopy_AndAdvancesSchedule()
    {
        Guid companyId = default, autoRepeatId = default, templateId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var company = await companyRepository.InsertAsync(new Core.Entities.Company(Guid.NewGuid(), "Recurring PO Test Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Recurring PO Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Inventory.Entities.Item(Guid.NewGuid(), company.Id, "RPO-1", "Recurring PO Item", Inventory.ItemType.Goods), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PO Series", "PurchaseOrder", "RPO-"), autoSave: true);

            var template = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-TEMPLATE-1", DateTime.Today.AddMonths(-1));
            template.AddItem(item.Id, "Recurring PO Item", 5m, 50m, 0m);
            template.Submit();
            await poRepository.InsertAsync(template, autoSave: true);

            var autoRepeat = new AutoRepeat(Guid.NewGuid(), company.Id, "PurchaseOrder", template.Id,
                RepeatFrequency.Monthly, DateTime.Today, DateTime.Today.AddYears(1));
            await autoRepeatRepository.InsertAsync(autoRepeat, autoSave: true);

            companyId = company.Id;
            autoRepeatId = autoRepeat.Id;
            templateId = template.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<RecurringPurchaseOrderJob>();
            await job.ExecuteAsync(new RecurringPurchaseOrderJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var poQuery = await poRepository.GetQueryableAsync();
            var generated = poQuery.Where(po => po.CompanyId == companyId && po.Id != templateId).ToList();
            generated.Count.ShouldBe(1);
            generated[0].Status.ShouldBe(DocumentStatus.Draft);
            generated[0].SupplierId.ShouldBe((await poRepository.GetAsync(templateId)).SupplierId);
            generated[0].Items.Count.ShouldBe(1);
            generated[0].Items[0].Quantity.ShouldBe(5m);

            var reloadedRepeat = await autoRepeatRepository.GetAsync(autoRepeatId);
            reloadedRepeat.GeneratedCount.ShouldBe(1);
            reloadedRepeat.NextScheduleDate.ShouldBeGreaterThan(DateTime.Today);
        });
    }

    [Fact]
    public async Task RecurringSalesOrderJob_CreatesDraftCopy_AndAdvancesSchedule()
    {
        Guid companyId = default, autoRepeatId = default, templateId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Sales.Entities.Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var company = await companyRepository.InsertAsync(new Core.Entities.Company(Guid.NewGuid(), "Recurring SO Test Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Sales.Entities.Customer(Guid.NewGuid(), company.Id, "Recurring SO Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Inventory.Entities.Item(Guid.NewGuid(), company.Id, "RSO-1", "Recurring SO Item", Inventory.ItemType.Goods), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SO Series", "SalesOrder", "RSO-"), autoSave: true);

            var template = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-TEMPLATE-1", DateTime.Today.AddMonths(-1));
            template.AddItem(item.Id, "Recurring SO Item", 3m, 200m, 0m);
            template.Submit();
            await soRepository.InsertAsync(template, autoSave: true);

            var autoRepeat = new AutoRepeat(Guid.NewGuid(), company.Id, "SalesOrder", template.Id,
                RepeatFrequency.Monthly, DateTime.Today, DateTime.Today.AddYears(1));
            await autoRepeatRepository.InsertAsync(autoRepeat, autoSave: true);

            companyId = company.Id;
            autoRepeatId = autoRepeat.Id;
            templateId = template.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<RecurringSalesOrderJob>();
            await job.ExecuteAsync(new RecurringSalesOrderJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var soQuery = await soRepository.GetQueryableAsync();
            var generated = soQuery.Where(so => so.CompanyId == companyId && so.Id != templateId).ToList();
            generated.Count.ShouldBe(1);
            generated[0].Status.ShouldBe(DocumentStatus.Draft);
            generated[0].Items.Count.ShouldBe(1);
            generated[0].Items[0].Quantity.ShouldBe(3m);

            var reloadedRepeat = await autoRepeatRepository.GetAsync(autoRepeatId);
            reloadedRepeat.GeneratedCount.ShouldBe(1);
        });
    }

    [Fact]
    public async Task RecurringPurchaseInvoiceJob_CreatesDraftCopy_AndAdvancesSchedule()
    {
        Guid companyId = default, autoRepeatId = default, templateId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var piRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var company = await companyRepository.InsertAsync(new Core.Entities.Company(Guid.NewGuid(), "Recurring PI Test Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Recurring PI Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Inventory.Entities.Item(Guid.NewGuid(), company.Id, "RPI-1", "Recurring PI Item", Inventory.ItemType.Goods), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PI Series", "PurchaseInvoice", "RPI-"), autoSave: true);

            var template = new PurchaseInvoice(Guid.NewGuid(), company.Id, supplier.Id, "PI-TEMPLATE-1", DateTime.Today.AddMonths(-1));
            template.AddItem(item.Id, "Recurring PI Item", 2m, 400m, 0m);
            template.CreditToAccountId = Guid.NewGuid();
            template.Submit();
            await piRepository.InsertAsync(template, autoSave: true);

            var autoRepeat = new AutoRepeat(Guid.NewGuid(), company.Id, "PurchaseInvoice", template.Id,
                RepeatFrequency.Monthly, DateTime.Today, DateTime.Today.AddYears(1));
            await autoRepeatRepository.InsertAsync(autoRepeat, autoSave: true);

            companyId = company.Id;
            autoRepeatId = autoRepeat.Id;
            templateId = template.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<RecurringPurchaseInvoiceJob>();
            await job.ExecuteAsync(new RecurringPurchaseInvoiceJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var piRepository = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var piQuery = await piRepository.GetQueryableAsync();
            var generated = piQuery.Where(pi => pi.CompanyId == companyId && pi.Id != templateId).ToList();
            generated.Count.ShouldBe(1);
            generated[0].Status.ShouldBe(DocumentStatus.Draft);
            generated[0].Items.Count.ShouldBe(1);
            generated[0].CreditToAccountId.ShouldBe((await piRepository.GetAsync(templateId)).CreditToAccountId);

            var reloadedRepeat = await autoRepeatRepository.GetAsync(autoRepeatId);
            reloadedRepeat.GeneratedCount.ShouldBe(1);
        });
    }

    [Fact]
    public async Task RecurringPurchaseOrderJob_TemplateCancelled_AutoDisablesRepeat_NoDocumentCreated()
    {
        Guid companyId = default, autoRepeatId = default, templateId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Core.Entities.Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Inventory.Entities.Item, Guid>>();
            var poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var company = await companyRepository.InsertAsync(new Core.Entities.Company(Guid.NewGuid(), "Recurring PO Cancel Test Co"), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Recurring PO Cancel Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Inventory.Entities.Item(Guid.NewGuid(), company.Id, "RPOC-1", "Recurring PO Cancel Item", Inventory.ItemType.Goods), autoSave: true);

            var template = new PurchaseOrder(Guid.NewGuid(), company.Id, supplier.Id, "PO-TEMPLATE-CANCEL-1", DateTime.Today.AddMonths(-1));
            template.AddItem(item.Id, "Recurring PO Cancel Item", 5m, 50m, 0m);
            template.Submit();
            template.Cancel();
            await poRepository.InsertAsync(template, autoSave: true);

            var autoRepeat = new AutoRepeat(Guid.NewGuid(), company.Id, "PurchaseOrder", template.Id,
                RepeatFrequency.Monthly, DateTime.Today, DateTime.Today.AddYears(1));
            await autoRepeatRepository.InsertAsync(autoRepeat, autoSave: true);

            companyId = company.Id;
            autoRepeatId = autoRepeat.Id;
            templateId = template.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<RecurringPurchaseOrderJob>();
            await job.ExecuteAsync(new RecurringPurchaseOrderJobArgs { CompanyId = companyId, AsOfDate = DateTime.Today });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var poRepository = GetRequiredService<IRepository<PurchaseOrder, Guid>>();
            var autoRepeatRepository = GetRequiredService<IRepository<AutoRepeat, Guid>>();

            var poQuery = await poRepository.GetQueryableAsync();
            var generated = poQuery.Where(po => po.CompanyId == companyId && po.Id != templateId).ToList();
            generated.Count.ShouldBe(0);

            var reloadedRepeat = await autoRepeatRepository.GetAsync(autoRepeatId);
            reloadedRepeat.IsEnabled.ShouldBeFalse();
        });
    }
}
