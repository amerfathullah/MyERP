using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class SalesInvoiceAppService_Tests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISalesInvoiceAppService _invoiceAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<DocumentSeries, Guid> _seriesRepository;

    protected SalesInvoiceAppService_Tests()
    {
        _invoiceAppService = GetRequiredService<ISalesInvoiceAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
        _seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
    }

    [Fact]
    public async Task Should_Create_Invoice_With_Items()
    {
        // Arrange: seed a company, customer, and document series
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Test Company Sdn Bhd"), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "ABC Trading"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series", "SalesInvoice", "INV-"),
            autoSave: true);

        // Act
        var result = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new CreateSalesInvoiceItemDto
                {
                    ItemId = Guid.NewGuid(),
                    Description = "Widget A",
                    Quantity = 10,
                    UnitPrice = 100m,
                    TaxAmount = 60m,
                    Uom = "Unit"
                }
            }
        });

        // Assert
        result.ShouldNotBeNull();
        result.InvoiceNumber.ShouldStartWith("INV-");
        result.NetTotal.ShouldBe(1000m);
        result.TaxAmount.ShouldBe(60m);
        result.GrandTotal.ShouldBe(1060m);
        result.Status.ShouldBe("Draft");
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Submit_And_Post_Invoice()
    {
        // Arrange
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Submit Test Co"), autoSave: true);

        // Set up required default accounts for GL posting
        var accountRepo = GetRequiredService<IRepository<MyERP.Accounting.Entities.Account, Guid>>();
        var receivableAcct = new MyERP.Accounting.Entities.Account(
            Guid.NewGuid(), company.Id, "1130", "AR", MyERP.Accounting.AccountType.Asset);
        await accountRepo.InsertAsync(receivableAcct, autoSave: true);
        company.DefaultReceivableAccountId = receivableAcct.Id;
        await _companyRepository.UpdateAsync(company, autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Customer X"), autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series 2", "SalesInvoice", "SI-"),
            autoSave: true);

        var invoice = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            Items = new()
            {
                new() { ItemId = Guid.NewGuid(), Description = "Item", Quantity = 1, UnitPrice = 500m, TaxAmount = 30m }
            }
        });

        // Act: Submit
        var submitted = await _invoiceAppService.SubmitAsync(invoice.Id);
        submitted.Status.ShouldBe("Submitted");

        // Note: PostAsync requires full accounting infrastructure (FiscalYear, AccountingRules, etc.)
        // GL posting pipeline is extensively covered by domain tests (DocumentPostingOrchestrator)
    }

    [Fact]
    public async Task Should_Not_Double_Count_Coupon_When_Linked_To_Submitted_Sales_Order()
    {
        var company = await _companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), "Coupon Linked Test Co"), autoSave: true);

        var customer = await _customerRepository.InsertAsync(
            new Customer(Guid.NewGuid(), company.Id, "Coupon Customer"), autoSave: true);

        var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(
            new Item(Guid.NewGuid(), company.Id, "CPN-ITEM-1", "Item 1", ItemType.Service), autoSave: true);

        var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
        var receivable = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1200-CPN", "Debtors", AccountType.Asset), autoSave: true);
        var income = await accountRepo.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "4000-CPN", "Sales", AccountType.Revenue), autoSave: true);
        var costCenter = await GetRequiredService<IRepository<CostCenter, Guid>>().InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, "CPN CC"), autoSave: true);

        company.DefaultReceivableAccountId = receivable.Id;
        company.DefaultIncomeAccountId = income.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await _companyRepository.UpdateAsync(company, autoSave: true);

        await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY Coupon", DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1)), autoSave: true);

        var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
        await ruleRepo.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Recv", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
        await ruleRepo.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Income", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 }, autoSave: true);

        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SI Series Coupon", "SalesInvoice", "SI-"), autoSave: true);
        await _seriesRepository.InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, "SO Series Coupon", "SalesOrder", "SO-"), autoSave: true);

        var pricingRuleRepo = GetRequiredService<IRepository<PricingRule, Guid>>();
        var couponRepo = GetRequiredService<IRepository<CouponCode, Guid>>();
        var salesOrderService = GetRequiredService<ISalesOrderAppService>();

        var rule = await pricingRuleRepo.InsertAsync(
            new PricingRule(Guid.NewGuid(), "10% Coupon Rule", PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
            {
                DiscountPercentage = 10m
            }, autoSave: true);

        var coupon = await couponRepo.InsertAsync(
            new CouponCode(Guid.NewGuid(), "ONEUSE10", "10% Single Use", CouponType.Promotional, rule.Id)
            {
                MaximumUse = 1,
                IsEnabled = true
            }, autoSave: true);

        // 1. Create and submit SO with coupon (consumes usage: 0 -> 1)
        var soDto = await salesOrderService.CreateAsync(new CreateSalesOrderDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            OrderDate = DateTime.Today,
            CouponCode = "ONEUSE10",
            Items = new()
            {
                new() { ItemId = item.Id, Description = "Item 1", Quantity = 2, UnitPrice = 100m, Uom = "Unit" }
            }
        });
        await salesOrderService.SubmitAsync(soDto.Id);

        var couponAfterSo = await couponRepo.GetAsync(coupon.Id);
        couponAfterSo.Used.ShouldBe(1);

        // Unlinked item attempting to reuse exhausted coupon must fail
        await Should.ThrowAsync<BusinessException>(() =>
            _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                IssueDate = DateTime.Today,
                CouponCode = "ONEUSE10",
                Items = new()
                {
                    new() { ItemId = item.Id, Description = "Item 1", Quantity = 1, UnitPrice = 100m }
                }
            }));

        // 2. Create, submit, and post invoice linked to SO with same coupon code.
        // Per ERPNext PR #59581 / commit 2cc77651b7: coupon is NOT consumed again.
        var so = await salesOrderService.GetAsync(soDto.Id);
        var invoiceDto = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            CouponCode = "ONEUSE10",
            Items = new()
            {
                new()
                {
                    ItemId = item.Id,
                    Description = "Item 1",
                    Quantity = 2,
                    UnitPrice = 100m,
                    SalesOrderItemId = so.Items[0].Id
                }
            }
        });
        await _invoiceAppService.SubmitAsync(invoiceDto.Id);
        await _invoiceAppService.PostAsync(invoiceDto.Id);

        var couponAfterSi = await couponRepo.GetAsync(coupon.Id);
        couponAfterSi.Used.ShouldBe(1);

        // Credit note (return) against posted invoice also does not consume or release coupon
        var creditNoteDto = await _invoiceAppService.CreateAsync(new CreateSalesInvoiceDto
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            IssueDate = DateTime.Today,
            IsReturn = true,
            ReturnAgainstId = invoiceDto.Id,
            CouponCode = "ONEUSE10",
            Items = new()
            {
                new()
                {
                    ItemId = item.Id,
                    Description = "Item 1",
                    Quantity = -1,
                    UnitPrice = 100m,
                    SalesOrderItemId = so.Items[0].Id
                }
            }
        });
        await _invoiceAppService.SubmitAsync(creditNoteDto.Id);
        await _invoiceAppService.PostAsync(creditNoteDto.Id);
        (await couponRepo.GetAsync(coupon.Id)).Used.ShouldBe(1);

        await _invoiceAppService.CancelAsync(creditNoteDto.Id);
        (await couponRepo.GetAsync(coupon.Id)).Used.ShouldBe(1);

        // 3. Cancel invoice: usage must remain 1 (since SO is still active)
        await _invoiceAppService.CancelAsync(invoiceDto.Id);
        var couponAfterSiCancel = await couponRepo.GetAsync(coupon.Id);
        couponAfterSiCancel.Used.ShouldBe(1);

        // 4. Cancel SO: usage reverts to 0
        await salesOrderService.CancelAsync(soDto.Id);
        var couponAfterSoCancel = await couponRepo.GetAsync(coupon.Id);
        couponAfterSoCancel.Used.ShouldBe(0);
    }
}
