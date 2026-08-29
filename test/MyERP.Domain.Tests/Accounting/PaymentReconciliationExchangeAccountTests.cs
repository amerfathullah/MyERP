using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

public class PaymentReconciliationExchangeAccountTests
{
    [Fact]
    public void Company_GetExchangeGainLossAccountId_ResolvesSplitAndFallbackAccounts()
    {
        var generalAcc = Guid.NewGuid();
        var gainAcc = Guid.NewGuid();
        var lossAcc = Guid.NewGuid();

        var company = new Company(Guid.NewGuid(), "Test Co")
        {
            ExchangeGainLossAccountId = generalAcc
        };

        // Fallback to general account
        company.GetExchangeGainLossAccountId(isGain: true).ShouldBe(generalAcc);
        company.GetExchangeGainLossAccountId(isGain: false).ShouldBe(generalAcc);

        // Specific gain account override
        company.ExchangeGainAccountId = gainAcc;
        company.GetExchangeGainLossAccountId(isGain: true).ShouldBe(gainAcc);
        company.GetExchangeGainLossAccountId(isGain: false).ShouldBe(generalAcc);

        // Specific loss account override
        company.ExchangeLossAccountId = lossAcc;
        company.GetExchangeGainLossAccountId(isGain: false).ShouldBe(lossAcc);
    }
}
