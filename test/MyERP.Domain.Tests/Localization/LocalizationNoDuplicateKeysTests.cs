using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace MyERP.Domain.Tests.Localization;

public class LocalizationNoDuplicateKeysTests
{
    private static string GetLocalizationDirectory()
    {
        var basePath = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(basePath, "..", "..", "..", "..", "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP"),
            Path.Combine(basePath, "..", "..", "..", "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "MyERP.Domain.Shared", "Localization", "MyERP"),
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
                return Path.GetFullPath(c);
        }

        throw new DirectoryNotFoundException("Could not find Localization/MyERP directory.");
    }

    [Fact]
    public void EnJson_MustNotContainDuplicateKeys()
    {
        var dir = GetLocalizationDirectory();
        var enJsonPath = Path.Combine(dir, "en.json");
        Assert.True(File.Exists(enJsonPath), $"en.json not found at {enJsonPath}");

        var duplicates = FindDuplicateKeys(enJsonPath);
        Assert.True(duplicates.Count == 0,
            $"Found {duplicates.Count} duplicate key(s) in en.json:\n" + string.Join("\n", duplicates));
    }

    [Fact]
    public void AllLocalizationJsonFiles_MustNotContainDuplicateKeys()
    {
        var dir = GetLocalizationDirectory();
        var jsonFiles = Directory.GetFiles(dir, "*.json");
        Assert.NotEmpty(jsonFiles);

        var allErrors = new List<string>();
        foreach (var file in jsonFiles)
        {
            var duplicates = FindDuplicateKeys(file);
            if (duplicates.Count > 0)
            {
                var fileName = Path.GetFileName(file);
                allErrors.Add($"[{fileName}]:\n  " + string.Join("\n  ", duplicates));
            }
        }

        Assert.True(allErrors.Count == 0,
            $"Duplicate keys found in localization files:\n" + string.Join("\n", allErrors));
    }

    private static List<string> FindDuplicateKeys(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var keyOccurrences = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var keyRegex = new Regex(@"^\s*""([^""]+)""\s*:", RegexOptions.Compiled);
        var inTexts = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("\"texts\":", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("\"Texts\":", StringComparison.OrdinalIgnoreCase))
            {
                inTexts = true;
                continue;
            }

            if (!inTexts)
                continue;

            var match = keyRegex.Match(line);
            if (!match.Success)
                continue;

            var key = match.Groups[1].Value;
            if (!keyOccurrences.TryGetValue(key, out var list))
            {
                list = new List<int>();
                keyOccurrences[key] = list;
            }
            list.Add(i + 1);
        }

        var result = new List<string>();
        foreach (var kvp in keyOccurrences)
        {
            if (kvp.Value.Count > 1)
            {
                result.Add($"Key \"{kvp.Key}\" duplicated on lines: {string.Join(", ", kvp.Value)}");
            }
        }

        return result;
    }
}
