using System;
using System.Collections.Generic;
using System.Text.Json;
using MyERP.Accounting.Entities;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Parser for ERPNext verified Chart of Accounts JSON templates (PR #57279 / us_chart_of_accounts.json).
/// Converts hierarchical nested dictionary tree into flat CoaTemplateRow list with parent code linkage.
/// </summary>
public static class ChartOfAccountsTemplateParser
{
    private static readonly HashSet<string> MetadataFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "account_number", "account_type", "account_category", "is_group", "root_type", "tax_rate", "account_currency"
    };

    public static List<CoaTemplateRow> Parse(string jsonContent)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tree", out var treeElement))
        {
            throw new ArgumentException("JSON does not contain a 'tree' root element.", nameof(jsonContent));
        }

        var rows = new List<CoaTemplateRow>();

        foreach (var property in treeElement.EnumerateObject())
        {
            ParseNode(property.Name, property.Value, parentCode: null, currentRoot: property.Name, rows);
        }

        return rows;
    }

    private static void ParseNode(
        string accountName,
        JsonElement element,
        string? parentCode,
        string currentRoot,
        List<CoaTemplateRow> rows)
    {
        var accountNumber = element.TryGetProperty("account_number", out var numEl) ? numEl.GetString()?.Trim() ?? string.Empty : string.Empty;
        var accountTypeStr = element.TryGetProperty("account_type", out var typeEl) ? typeEl.GetString() : null;
        var rootTypeStr = element.TryGetProperty("root_type", out var rootEl) ? rootEl.GetString() : currentRoot;

        var children = new List<JsonProperty>();
        foreach (var prop in element.EnumerateObject())
        {
            if (!MetadataFields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.Object)
            {
                children.Add(prop);
            }
        }

        bool isGroup = children.Count > 0;
        if (element.TryGetProperty("is_group", out var isGroupEl))
        {
            if (isGroupEl.ValueKind == JsonValueKind.True || isGroupEl.ValueKind == JsonValueKind.False)
                isGroup = isGroupEl.GetBoolean();
            else if (isGroupEl.ValueKind == JsonValueKind.Number)
                isGroup = isGroupEl.GetInt32() == 1;
        }

        var rootType = MapRootType(rootTypeStr);
        var subType = MapAccountSubType(accountTypeStr);

        rows.Add(new CoaTemplateRow(
            AccountCode: accountNumber,
            AccountName: accountName,
            AccountType: rootType,
            IsGroup: isGroup,
            ParentCode: parentCode,
            SubType: subType
        ));

        var currentCode = string.IsNullOrWhiteSpace(accountNumber) ? parentCode : accountNumber;
        foreach (var child in children)
        {
            ParseNode(child.Name, child.Value, currentCode, rootTypeStr ?? currentRoot, rows);
        }
    }

    private static AccountType MapRootType(string? rootType)
    {
        if (string.IsNullOrWhiteSpace(rootType)) return AccountType.Asset;

        return rootType.ToLowerInvariant() switch
        {
            "asset" or "assets" or "application of funds (assets)" => AccountType.Asset,
            "liability" or "liabilities" or "source of funds (liabilities)" => AccountType.Liability,
            "equity" => AccountType.Equity,
            "income" or "revenue" => AccountType.Revenue,
            "expense" or "expenses" => AccountType.Expense,
            _ => AccountType.Asset
        };
    }

    private static AccountSubType? MapAccountSubType(string? accountType)
    {
        if (string.IsNullOrWhiteSpace(accountType)) return null;

        return accountType.ToLowerInvariant() switch
        {
            "cash" => AccountSubType.CashAccount,
            "bank" => AccountSubType.BankAccount,
            "receivable" => AccountSubType.AccountsReceivable,
            "payable" => AccountSubType.AccountsPayable,
            "stock" => AccountSubType.Stock,
            "cost of goods sold" => AccountSubType.CostOfGoodsSold,
            "tax" => AccountSubType.TaxPayable,
            "fixed asset" => AccountSubType.FixedAsset,
            "accumulated depreciation" => AccountSubType.AccumulatedDepreciation,
            "depreciation" => AccountSubType.DepreciationExpense,
            "round off" => AccountSubType.OperatingExpense,
            _ => null
        };
    }
}
