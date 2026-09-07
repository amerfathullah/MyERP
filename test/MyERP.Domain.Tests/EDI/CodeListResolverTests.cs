using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.EDI.DomainServices;
using MyERP.EDI.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.EDI;

public class CodeListResolverTests
{
    [Fact]
    public void NaturalVersionComparer_Should_Order_Integers_Correctly()
    {
        var comparer = new NaturalVersionComparer();
        var versions = new List<string> { "10", "1", "20", "2", "3" };
        var sorted = versions.OrderBy(v => v, comparer).ToList();

        sorted.ShouldBe(new[] { "1", "2", "3", "10", "20" });
    }

    [Fact]
    public void NaturalVersionComparer_Should_Order_IsoDates_Correctly()
    {
        var comparer = new NaturalVersionComparer();
        var versions = new List<string> { "2020-11-05", "2019-12-31", "2020-01-01" };
        var sorted = versions.OrderBy(v => v, comparer).ToList();

        sorted.ShouldBe(new[] { "2019-12-31", "2020-01-01", "2020-11-05" });
    }

    [Fact]
    public void NaturalVersionComparer_Should_Order_Dotted_And_Alphanumeric_Correctly()
    {
        var comparer = new NaturalVersionComparer();
        var versions = new List<string> { "v1.10", "v1.2", "D.16B", "D.16A" };
        var sorted = versions.OrderBy(v => v, comparer).ToList();

        sorted.ShouldBe(new[] { "D.16A", "D.16B", "v1.2", "v1.10" });
    }

    [Fact]
    public void NaturalVersionComparer_Should_Handle_Nulls()
    {
        var comparer = new NaturalVersionComparer();
        comparer.Compare(null, null).ShouldBe(0);
        comparer.Compare(null, "1").ShouldBe(-1);
        comparer.Compare("1", null).ShouldBe(1);
    }

    [Fact]
    public async Task ResolveCodeListAsync_Should_Resolve_By_Guid_Id()
    {
        var id = Guid.NewGuid();
        var entity = new CodeList(id, "Test CodeList", null, null, "DEF", "1.0", null, null, null, true);

        var repo = Substitute.For<IRepository<CodeList, Guid>>();
        repo.FindAsync(id).Returns(Task.FromResult<CodeList?>(entity));

        var resolver = new CodeListResolver(repo);
        var result = await resolver.ResolveCodeListAsync(id.ToString());

        result.ShouldNotBeNull();
        result.Id.ShouldBe(id);
        result.Title.ShouldBe("Test CodeList");
    }

    [Fact]
    public async Task ResolveCodeListAsync_Should_Give_Precedence_To_Title()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var uri = "urn:test:code-list";

        var entity1 = new CodeList(id1, uri, uri, null, "DEF1", "1.0", null, null, null, true);
        var entity2 = new CodeList(id2, "Other Title", uri, null, "DEF2", "2.0", null, null, null, true);

        var repo = Substitute.For<IRepository<CodeList, Guid>>();
        var data = new List<CodeList> { entity1, entity2 }.AsQueryable();
        repo.GetQueryableAsync().Returns(Task.FromResult(data));

        var resolver = new CodeListResolver(repo);

        // When searching by uri which matches entity1's Title exactly, entity1 must be returned
        var result = await resolver.ResolveCodeListAsync(uri);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(id1);
    }

    [Fact]
    public async Task ResolveCodeListAsync_Should_Resolve_CanonicalUri_To_Latest_Version()
    {
        var idV1 = Guid.NewGuid();
        var idV2 = Guid.NewGuid();
        var idV10 = Guid.NewGuid();
        var uri = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

        var v1 = new CodeList(idV1, "UBL Invoice v1", uri, null, "100", "1.0", null, null, null, true);
        var v2 = new CodeList(idV2, "UBL Invoice v2", uri, null, "200", "2.0", null, null, null, true);
        var v10 = new CodeList(idV10, "UBL Invoice v10", uri, null, "380", "10.0", null, null, null, true);

        var repo = Substitute.For<IRepository<CodeList, Guid>>();
        var data = new List<CodeList> { v1, v10, v2 }.AsQueryable();
        repo.GetQueryableAsync().Returns(Task.FromResult(data));

        var resolver = new CodeListResolver(repo);
        var result = await resolver.ResolveCodeListAsync(uri);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(idV10);
        result.Version.ShouldBe("10.0");
        result.DefaultCommonCode.ShouldBe("380");
    }

    [Fact]
    public async Task ResolveCodeListAsync_Should_Ignore_Inactive_For_CanonicalUri()
    {
        var idActive = Guid.NewGuid();
        var idInactive = Guid.NewGuid();
        var uri = "urn:test:inactive";

        var active = new CodeList(idActive, "Active V1", uri, null, "1", "1.0", null, null, null, true);
        var inactive = new CodeList(idInactive, "Inactive V2", uri, null, "2", "2.0", null, null, null, false);

        var repo = Substitute.For<IRepository<CodeList, Guid>>();
        var data = new List<CodeList> { active, inactive }.AsQueryable();
        repo.GetQueryableAsync().Returns(Task.FromResult(data));

        var resolver = new CodeListResolver(repo);
        var result = await resolver.ResolveCodeListAsync(uri);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(idActive);
        result.Version.ShouldBe("1.0");
    }

    [Fact]
    public async Task GetDefaultCodeAsync_Should_Return_DefaultCommonCode()
    {
        var id = Guid.NewGuid();
        var uri = "urn:test:default";
        var cl = new CodeList(id, "CL", uri, null, "DEFAULT_CODE", "1.0", null, null, null, true);

        var repo = Substitute.For<IRepository<CodeList, Guid>>();
        repo.GetQueryableAsync().Returns(Task.FromResult(new List<CodeList> { cl }.AsQueryable()));

        var resolver = new CodeListResolver(repo);
        var defaultCode = await resolver.GetDefaultCodeAsync(uri);

        defaultCode.ShouldBe("DEFAULT_CODE");
    }
}
