using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

public class UpstreamParityBatch20261026Part6Tests
{
    // --- ERPNext PR #61267 / commit 96be98d7d6: Protect clearance dates earlier than cheque/reference date ---

    [Fact]
    public void PaymentEntry_SetClearanceDate_EarlierThanReferenceDate_ThrowsClearanceDateBeforeChequeDate()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var referenceDate = new DateTime(2026, 6, 20);
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, postingDate,
            500m, Guid.NewGuid(), Guid.NewGuid())
        {
            ReferenceDate = referenceDate
        };

        // Clearance date after posting date (June 16), but before cheque/reference date (June 20)
        var invalidClearanceDate = new DateTime(2026, 6, 16);
        var ex = Should.Throw<BusinessException>(() => pe.SetClearanceDate(invalidClearanceDate));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ClearanceDateBeforeChequeDate);
    }

    [Fact]
    public void PaymentEntry_SetClearanceDate_OnOrAfterReferenceDate_Succeeds()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var referenceDate = new DateTime(2026, 6, 20);
        var pe = new PaymentEntry(
            Guid.NewGuid(), Guid.NewGuid(), PaymentType.Receive, postingDate,
            500m, Guid.NewGuid(), Guid.NewGuid())
        {
            ReferenceDate = referenceDate
        };

        var validClearanceDate = new DateTime(2026, 6, 20);
        pe.SetClearanceDate(validClearanceDate);
        pe.ClearanceDate.ShouldBe(validClearanceDate);

        // Can clear (reversible)
        pe.SetClearanceDate(null);
        pe.ClearanceDate.ShouldBeNull();
    }

    // --- ERPNext PR #61970 / commit ee94f177ae: Selling Settings HideTaxId flag ---

    [Fact]
    public async Task ErpSettingsAppService_SellingGroup_IncludesHideTaxId()
    {
        var settingProvider = Substitute.For<ISettingProvider>();
        var settingManager = Substitute.For<ISettingManager>();

        settingProvider.GetOrNullAsync(MyERPSettings.Selling.HideTaxId)
            .Returns(Task.FromResult<string?>("false"));

        var appService = new ErpSettingsAppService(settingProvider, settingManager);
        var group = await appService.GetGroupAsync("Selling");

        group.ShouldContainKey(MyERPSettings.Selling.HideTaxId);
    }
}
