using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Accounting;

/// <summary>
/// Bank Statement Import Service — parses CSV bank statements and creates BankTransaction records.
/// Supports common Malaysian bank CSV formats (date, description, debit, credit, balance).
/// 
/// ERPNext equivalent: banking/doctype/bank_statement_import
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankStatementImportAppService : ApplicationService
{
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;
    private readonly IGuidGenerator _guidGenerator;

    public BankStatementImportAppService(
        IRepository<BankTransaction, Guid> transactionRepository,
        IGuidGenerator guidGenerator)
    {
        _transactionRepository = transactionRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Import bank transactions from a CSV string.
    /// Expected format: Date,Description,Debit,Credit,Balance
    /// First row is treated as header and skipped.
    /// </summary>
    public async Task<BankStatementImportResult> ImportFromCsvAsync(BankStatementImportInput input)
    {
        var result = new BankStatementImportResult();
        var lines = input.CsvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= 1)
        {
            result.Errors.Add("CSV file is empty or contains only a header row.");
            return result;
        }

        // Skip header
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < 4)
                {
                    result.Errors.Add($"Row {i + 1}: insufficient columns (expected at least 4).");
                    result.SkippedCount++;
                    continue;
                }

                var date = ParseDate(fields[0].Trim());
                var description = fields[1].Trim().Trim('"');
                var debit = ParseDecimal(fields[2]);
                var credit = ParseDecimal(fields[3]);

                // Amount: positive = money in (credit), negative = money out (debit)
                var amount = credit - debit;
                if (amount == 0)
                {
                    result.SkippedCount++;
                    continue;
                }

                var transaction = new BankTransaction(
                    _guidGenerator.Create(),
                    input.CompanyId,
                    input.BankAccountId,
                    date,
                    description,
                    amount,
                    input.TenantId);

                // Set directional amounts (positive = deposit, negative = withdrawal)
                if (amount > 0)
                    transaction.Deposit = amount;
                else
                    transaction.Withdrawal = Math.Abs(amount);

                // Set currency from input (for validation against bank account)
                if (!string.IsNullOrWhiteSpace(input.CurrencyCode))
                    transaction.CurrencyCode = input.CurrencyCode;

                // Normalize any excluded fees and validate included fees
                transaction.NormalizeFees();
                transaction.ValidateIncludedFee();

                // Try to extract reference number from description
                transaction.ReferenceNumber = ExtractReference(description);

                await _transactionRepository.InsertAsync(transaction);
                result.ImportedCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {i + 1}: {ex.Message}");
                result.SkippedCount++;
            }
        }

        return result;
    }

    private static DateTime ParseDate(string value)
    {
        // Support common date formats
        string[] formats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "d/M/yyyy", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(value.Trim('"'), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        return DateTime.Parse(value.Trim('"'), CultureInfo.InvariantCulture);
    }

    private static decimal ParseDecimal(string value)
    {
        var cleaned = value.Trim().Trim('"').Replace(",", "");
        if (string.IsNullOrWhiteSpace(cleaned)) return 0;
        return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parser (handles quoted fields with commas)
        var fields = new List<string>();
        bool inQuotes = false;
        var current = "";

        foreach (var ch in line)
        {
            if (ch == '"') { inQuotes = !inQuotes; current += ch; }
            else if (ch == ',' && !inQuotes) { fields.Add(current); current = ""; }
            else { current += ch; }
        }
        fields.Add(current);
        return fields.ToArray();
    }

    private static string? ExtractReference(string description)
    {
        // Common patterns: "REF: 12345", "TRN/12345", invoice numbers
        if (description.Contains("REF:", StringComparison.OrdinalIgnoreCase))
        {
            var idx = description.IndexOf("REF:", StringComparison.OrdinalIgnoreCase) + 4;
            return description[idx..].Trim().Split(' ')[0];
        }
        return null;
    }
}

public class BankStatementImportInput
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }
    public string CsvContent { get; set; } = null!;
    public Guid? TenantId { get; set; }

    /// <summary>Optional currency code. When set, validated against bank account GL currency.</summary>
    public string? CurrencyCode { get; set; }
}

public class BankStatementImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0 || ImportedCount > 0;
}
