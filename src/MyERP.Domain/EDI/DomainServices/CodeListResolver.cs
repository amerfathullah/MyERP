using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MyERP.EDI.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.EDI.DomainServices;

/// <summary>
/// Domain service for resolving EDI Code Lists by Title, Id, or Canonical URI.
/// Supports natural version ordering (integers and ISO dates).
/// Migrated from ERPNext edi/doctype/code_list (commit 5beed5f4f0).
/// </summary>
public class CodeListResolver : DomainService
{
    private readonly IRepository<CodeList, Guid> _codeListRepository;
    private readonly IRepository<CommonCode, Guid>? _commonCodeRepository;

    public CodeListResolver(
        IRepository<CodeList, Guid> codeListRepository,
        IRepository<CommonCode, Guid>? commonCodeRepository = null)
    {
        _codeListRepository = codeListRepository;
        _commonCodeRepository = commonCodeRepository;
    }

    /// <summary>
    /// Resolves a Code List for a document title/id or canonical URI.
    /// Title / Id takes precedence; canonical URI resolves to the latest version available.
    /// </summary>
    public async Task<CodeList?> ResolveCodeListAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        identifier = identifier.Trim();

        // 1. By Guid Id
        if (Guid.TryParse(identifier, out var id))
        {
            var byId = await _codeListRepository.FindAsync(id);
            if (byId != null) return byId;
        }

        var queryable = await _codeListRepository.GetQueryableAsync();

        // 2. Exact Title match takes precedence
        var byTitle = queryable.FirstOrDefault(x => x.Title == identifier);
        if (byTitle != null) return byTitle;

        // 3. Match CanonicalUri -> resolve to latest version
        var candidates = queryable
            .Where(x => x.CanonicalUri == identifier && x.IsActive)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var comparer = new NaturalVersionComparer();
        return candidates.OrderByDescending(c => c.Version, comparer).First();
    }

    /// <summary>
    /// Returns default common code string for a given code list (name, id, or canonical URI).
    /// </summary>
    public async Task<string?> GetDefaultCodeAsync(string identifier)
    {
        var codeList = await ResolveCodeListAsync(identifier);
        if (codeList == null)
            return null;

        if (!string.IsNullOrWhiteSpace(codeList.DefaultCommonCode))
            return codeList.DefaultCommonCode;

        if (_commonCodeRepository != null)
        {
            var commonCodes = await _commonCodeRepository.GetQueryableAsync();
            var defaultCommon = commonCodes.FirstOrDefault(c => c.CodeListId == codeList.Id && c.IsActive);
            return defaultCommon?.Code;
        }

        return null;
    }
}

/// <summary>
/// Natural sort comparer for version strings: integers (3 < 10) and ISO dates (2019-12-31 < 2020-01-01).
/// Matches ERPNext _version_key logic.
/// </summary>
public class NaturalVersionComparer : IComparer<string?>
{
    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var partsX = Regex.Split(x, @"(\d+)");
        var partsY = Regex.Split(y, @"(\d+)");

        var length = Math.Min(partsX.Length, partsY.Length);
        for (var i = 0; i < length; i++)
        {
            var pX = partsX[i];
            var pY = partsY[i];

            if (pX == pY) continue;

            if (long.TryParse(pX, out var numX) && long.TryParse(pY, out var numY))
            {
                var cmp = numX.CompareTo(numY);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = string.Compare(pX, pY, StringComparison.Ordinal);
                if (cmp != 0) return cmp;
            }
        }

        return partsX.Length.CompareTo(partsY.Length);
    }
}
