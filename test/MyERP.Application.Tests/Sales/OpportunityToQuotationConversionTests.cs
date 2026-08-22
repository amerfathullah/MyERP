using System;
using System.Threading.Tasks;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Real integration coverage for DocumentConversionAppService.ConvertOpportunityToQuotationAsync,
/// replacing OpportunityConversionAndIssueSlaTests' reflection-existence check and
/// Assert.True(true) placeholder. Found while auditing the Angular side for proxy methods with no
/// UI caller: convertOpportunityToQuotation was the only one of 7 methods in
/// document-conversion.service.ts never called from any component — the Opportunity detail page
/// had no "Convert to Quotation" action at all, unlike every other document conversion in this
/// module. Added the button; this test covers the backend it now actually reaches.
/// </summary>
public abstract class OpportunityToQuotationConversionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ConvertOpportunityToQuotationAsync_CopiesItemsAndMarksOpportunityConverted()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionAppService = GetRequiredService<IDocumentConversionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Opp Conversion Test Co"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Quotation Series", "Quotation", "QTN-"), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Test Customer"), autoSave: true);

            var opportunity = new Opportunity(Guid.NewGuid(), company.Id, "OPP-CONV-1", "Widget Deal")
            {
                CustomerId = customer.Id,
            };
            opportunity.Items.Add(new OpportunityItem(Guid.NewGuid(), opportunity.Id, "Widget", 5m, 100m));
            await opportunityRepository.InsertAsync(opportunity, autoSave: true);

            var quotation = await conversionAppService.ConvertOpportunityToQuotationAsync(opportunity.Id);

            quotation.Id.ShouldNotBe(Guid.Empty);
            quotation.Items.Count.ShouldBe(1);
            quotation.Items[0].Quantity.ShouldBe(5m);
            quotation.Items[0].UnitPrice.ShouldBe(100m);

            var reloadedOpp = await opportunityRepository.GetAsync(opportunity.Id);
            reloadedOpp.Status.ShouldBe(OpportunityStatus.Quotation);
        });
    }

    [Fact]
    public async Task ConvertOpportunityToQuotationAsync_AlreadyConverted_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var opportunityRepository = GetRequiredService<IRepository<Opportunity, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionAppService = GetRequiredService<IDocumentConversionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Opp Conversion Test Co 2"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Quotation Series 2", "Quotation", "QTN2-"), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Test Customer 2"), autoSave: true);

            var opportunity = new Opportunity(Guid.NewGuid(), company.Id, "OPP-CONV-2", "Gadget Deal")
            {
                CustomerId = customer.Id,
            };
            await opportunityRepository.InsertAsync(opportunity, autoSave: true);

            await conversionAppService.ConvertOpportunityToQuotationAsync(opportunity.Id);

            // Opportunity is now Status=Quotation, not Open/Replied — a second conversion attempt
            // must be rejected.
            await Should.ThrowAsync<BusinessException>(
                () => conversionAppService.ConvertOpportunityToQuotationAsync(opportunity.Id));
        });
    }
}
