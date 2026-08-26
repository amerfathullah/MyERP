using System;
using MyERP.EDI.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.EDI;

public class EdiDomainTests
{
    [Fact]
    public void Should_Create_Valid_CodeList()
    {
        var id = Guid.NewGuid();
        var codeList = new CodeList(
            id,
            "UN/EDIFACT 1001 Document Types",
            "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2",
            "https://unece.org/trade/uncefact",
            "380",
            "D.16B",
            "UN/CEFACT",
            "UN/ECE",
            "Document name code list",
            true);

        codeList.Id.ShouldBe(id);
        codeList.Title.ShouldBe("UN/EDIFACT 1001 Document Types");
        codeList.CanonicalUri.ShouldBe("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
        codeList.DefaultCommonCode.ShouldBe("380");
        codeList.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Should_Create_Valid_CommonCode()
    {
        var id = Guid.NewGuid();
        var codeListId = Guid.NewGuid();
        var commonCode = new CommonCode(
            id,
            codeListId,
            "Commercial Invoice",
            "380",
            "Document/message claiming payment for goods or services supplied",
            "{\"category\":\"commercial\"}",
            true);

        commonCode.Id.ShouldBe(id);
        commonCode.CodeListId.ShouldBe(codeListId);
        commonCode.Title.ShouldBe("Commercial Invoice");
        commonCode.Code.ShouldBe("380");
        commonCode.AdditionalDataJson.ShouldNotBeNull();
        commonCode.AdditionalDataJson.ShouldContain("commercial");
        commonCode.IsActive.ShouldBeTrue();
    }
}
