using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Settings;
using Volo.Abp.Users;
using Xunit;

namespace MyERP.Domain.Tests.CRM;

public class UpstreamPr59907And59915Tests
{
    // =========================================================================
    // ERPNext PR #59907 / commit eb445464ca: Do Not Contact Lead Conversion
    // =========================================================================

    [Fact]
    public void Lead_ConvertToCustomer_AllowsDoNotContactStatus()
    {
        var lead = new Lead(Guid.NewGuid(), Guid.NewGuid(), "LEAD-001", "Jane", Guid.NewGuid());
        lead.MarkDoNotContact();
        var customerId = Guid.NewGuid();

        lead.ConvertToCustomer(customerId);

        lead.Status.ShouldBe(LeadStatus.Converted);
        lead.ConvertedCustomerId.ShouldBe(customerId);
    }

    // =========================================================================
    // ERPNext PR #59907 / commit b07b8053ad: Prospect duplicate lead & reader check
    // =========================================================================

    [Fact]
    public async Task ProspectAppService_AddLeadAsync_Throws_WhenLeadAlreadyInAnotherProspect()
    {
        var prospectId1 = Guid.NewGuid();
        var prospectId2 = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var prospect1 = new Prospect(prospectId1, Guid.NewGuid(), "Prospect Alpha");
        prospect1.AddLead(Guid.NewGuid(), leadId, "Lead 1", "lead1@test.com");

        var prospect2 = new Prospect(prospectId2, Guid.NewGuid(), "Prospect Beta");

        var repo = Substitute.For<IRepository<Prospect, Guid>>();
        repo.GetAsync(prospectId2).Returns(prospect2);
        repo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Prospect, object>>[]>())
            .Returns(new List<Prospect> { prospect1, prospect2 }.AsQueryable());

        var authService = Substitute.For<IAbpAuthorizationService>();
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(Task.FromResult(AuthorizationResult.Success()));
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(Task.FromResult(AuthorizationResult.Success()));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(Guid.NewGuid());
        currentUser.IsAuthenticated.Returns(true);

        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(Guid.NewGuid());

        var lazySp = Substitute.For<IAbpLazyServiceProvider>();
        lazySp.LazyGetService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetRequiredService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetService(typeof(IAuthorizationService)).Returns(authService);
        lazySp.LazyGetRequiredService(typeof(IAuthorizationService)).Returns(authService);

        lazySp.LazyGetService<ICurrentUser>().Returns(currentUser);
        lazySp.LazyGetRequiredService<ICurrentUser>().Returns(currentUser);
        lazySp.LazyGetService(typeof(ICurrentUser)).Returns(currentUser);
        lazySp.LazyGetRequiredService(typeof(ICurrentUser)).Returns(currentUser);

        lazySp.LazyGetService<IGuidGenerator>().Returns(guidGen);
        lazySp.LazyGetRequiredService<IGuidGenerator>().Returns(guidGen);
        lazySp.LazyGetService(typeof(IGuidGenerator)).Returns(guidGen);
        lazySp.LazyGetRequiredService(typeof(IGuidGenerator)).Returns(guidGen);

        var service = new ProspectAppService(repo)
        {
            LazyServiceProvider = lazySp
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.AddLeadAsync(prospectId2, leadId, "Lead 1", "lead1@test.com"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.LeadAlreadyInAnotherProspect);
        ex.Data["leadId"].ShouldBe(leadId);
        ex.Data["prospectName"].ShouldBe("Prospect Alpha");
    }

    // =========================================================================
    // ERPNext PR #59915 / commit 3d94050ade: CRM Settings default valid till
    // =========================================================================

    [Fact]
    public async Task CrmSettingsAppService_UpdateAsync_Throws_WhenValidityDaysNegative()
    {
        var repo = Substitute.For<IRepository<CrmSettings, Guid>>();
        var service = new CrmSettingsAppService(repo);

        var input = new UpdateCrmSettingsDto
        {
            DefaultQuotationValidityDays = -5,
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ErpSettingsAppService_SetAsync_Throws_WhenDefaultValidTillNegative()
    {
        var settingProvider = Substitute.For<ISettingProvider>();
        var settingManager = Substitute.For<Volo.Abp.SettingManagement.ISettingManager>();
        var service = new ErpSettingsAppService(settingProvider, settingManager);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.SetAsync(MyERPSettings.CRM.DefaultValidTill, "-10"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    // =========================================================================
    // ERPNext PR #59915: Quotation ValidUntil defaulted from CRM Settings
    // =========================================================================

    [Fact]
    public async Task ProspectAppService_AddLeadAsync_HidesOtherProspectName_WhenCannotRead()
    {
        var prospectId1 = Guid.NewGuid();
        var prospectId2 = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var prospect1 = new Prospect(prospectId1, Guid.NewGuid(), "Secret Prospect Alpha");
        prospect1.AddLead(Guid.NewGuid(), leadId, "Lead 1", "lead1@test.com");

        var prospect2 = new Prospect(prospectId2, Guid.NewGuid(), "Prospect Beta");

        var repo = Substitute.For<IRepository<Prospect, Guid>>();
        repo.GetAsync(prospectId2).Returns(prospect2);
        repo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Prospect, object>>[]>())
            .Returns(new List<Prospect> { prospect1, prospect2 }.AsQueryable());

        // Authorization service allows global Leads.Default check, but denies access to otherProspect resource
        var authService = Substitute.For<IAbpAuthorizationService>();
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<object?>(o => o == null), Arg.Any<string>())
            .Returns(Task.FromResult(AuthorizationResult.Success()));
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<object?>(o => o != null), Arg.Any<string>())
            .Returns(Task.FromResult(AuthorizationResult.Failed()));

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(Guid.NewGuid());
        currentUser.IsAuthenticated.Returns(true);

        var guidGen = Substitute.For<IGuidGenerator>();
        guidGen.Create().Returns(Guid.NewGuid());

        var lazySp = Substitute.For<IAbpLazyServiceProvider>();
        lazySp.LazyGetService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetRequiredService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetService(typeof(IAuthorizationService)).Returns(authService);
        lazySp.LazyGetRequiredService(typeof(IAuthorizationService)).Returns(authService);

        lazySp.LazyGetService<ICurrentUser>().Returns(currentUser);
        lazySp.LazyGetRequiredService<ICurrentUser>().Returns(currentUser);
        lazySp.LazyGetService(typeof(ICurrentUser)).Returns(currentUser);
        lazySp.LazyGetRequiredService(typeof(ICurrentUser)).Returns(currentUser);

        lazySp.LazyGetService<IGuidGenerator>().Returns(guidGen);
        lazySp.LazyGetRequiredService<IGuidGenerator>().Returns(guidGen);
        lazySp.LazyGetService(typeof(IGuidGenerator)).Returns(guidGen);
        lazySp.LazyGetRequiredService(typeof(IGuidGenerator)).Returns(guidGen);

        var service = new ProspectAppService(repo)
        {
            LazyServiceProvider = lazySp
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            service.AddLeadAsync(prospectId2, leadId, "Lead 1", "lead1@test.com"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.LeadAlreadyInAnotherProspect);
        ex.Data["leadId"].ShouldBe(leadId);
        ex.Data.Contains("prospectName").ShouldBeFalse();
    }

    // =========================================================================
    // ERPNext PR #60311 / commit 4eb8ba500c: Pick List Delivery Note Creation Permissions
    // =========================================================================

    [Fact]
    public async Task PickListAppService_CreateDeliveryNote_EnforcesPickListReadPermission()
    {
        var repo = Substitute.For<IRepository<MyERP.Inventory.Entities.PickList, Guid>>();
        var authService = Substitute.For<IAbpAuthorizationService>();
        // Deny StockEntries.Default
        authService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(Task.FromResult(AuthorizationResult.Failed()));

        var currentUser = Substitute.For<ICurrentUser>();
        var lazySp = Substitute.For<IAbpLazyServiceProvider>();
        lazySp.LazyGetService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetRequiredService<IAuthorizationService>().Returns(authService);
        lazySp.LazyGetService<ICurrentUser>().Returns(currentUser);
        lazySp.LazyGetRequiredService<ICurrentUser>().Returns(currentUser);

        var service = new MyERP.Inventory.PickListAppService(repo)
        {
            LazyServiceProvider = lazySp
        };

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            service.CreateDeliveryNoteFromPickListAsync(Guid.NewGuid()));
    }
}
