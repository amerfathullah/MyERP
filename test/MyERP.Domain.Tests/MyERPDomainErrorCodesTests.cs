using System;
using System.Linq;
using System.Reflection;
using MyERP;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

public class MyERPDomainErrorCodesTests
{
    /// <summary>
    /// Every public const string field in MyERPDomainErrorCodes (including nested classes)
    /// must have a unique value. Two constants sharing the same "MyERP:xxxxx" code collide
    /// on localization lookup — whichever en.json entry is defined last silently wins for
    /// BOTH errors, so the wrong message gets shown for one of them. This has happened for
    /// real (MyERP:05048, MyERP:01020, MyERP:03037, and 7 more found in one sweep) — this
    /// test is a permanent regression guard against a new one being introduced.
    /// </summary>
    [Fact]
    public void AllErrorCodeValues_AreUnique()
    {
        var duplicates = GetAllErrorCodeConstants(typeof(MyERPDomainErrorCodes))
            .GroupBy(x => x.Value)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Name))}")
            .ToList();

        duplicates.ShouldBeEmpty(
            $"Duplicate MyERPDomainErrorCodes values found (each must be unique):\n{string.Join("\n", duplicates)}");
    }

    private static System.Collections.Generic.IEnumerable<(string Name, string Value)> GetAllErrorCodeConstants(Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.FieldType == typeof(string) && field.IsLiteral)
            {
                yield return (field.Name, (string)field.GetRawConstantValue()!);
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var entry in GetAllErrorCodeConstants(nested))
            {
                yield return entry;
            }
        }
    }
}
