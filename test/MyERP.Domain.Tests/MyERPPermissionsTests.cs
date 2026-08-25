using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using Xunit;

namespace MyERP.Permissions;

public class MyERPPermissionsTests
{
    /// <summary>
    /// Every public const string field in MyERPPermissions (including nested classes) must
    /// have a unique value. Two different permission constants resolving to the same string
    /// would mean granting one silently grants the other — same collision shape as the
    /// duplicate MyERPDomainErrorCodes values found and fixed in an earlier session.
    /// </summary>
    [Fact]
    public void AllPermissionValues_AreUnique()
    {
        var duplicates = GetAllConstants(typeof(MyERPPermissions))
            .GroupBy(x => x.Value)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Name))}")
            .ToList();

        duplicates.ShouldBeEmpty(
            $"Duplicate MyERPPermissions values found (each must be unique):\n{string.Join("\n", duplicates)}");
    }

    private static IEnumerable<(string Name, string Value)> GetAllConstants(Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.FieldType == typeof(string) && field.IsLiteral)
            {
                yield return (type.FullName + "." + field.Name, (string)field.GetRawConstantValue()!);
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var entry in GetAllConstants(nested))
            {
                yield return entry;
            }
        }
    }
}
