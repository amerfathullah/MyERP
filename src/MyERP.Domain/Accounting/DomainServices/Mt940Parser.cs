using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// MT940 Bank Statement Parser — parses SWIFT MT940 format bank statements.
/// Malaysian banks (Maybank, CIMB, Public Bank, etc.) typically export MT940.
/// 
/// MT940 structure:
/// :20: Transaction reference
/// :25: Account identification  
/// :28C: Statement number/sequence
/// :60F: Opening balance
/// :61: Statement line (individual transaction)
/// :86: Information to account owner (transaction details)
/// :62F: Closing balance (booked)
/// :64: Closing available balance
/// 
/// Per gotcha #84: customer_reference overflow at 16-char cap → concatenate extra_details.
/// Per gotcha #NONREF: "NONREF" sentinel → falls back to bank_reference → transaction_reference.
/// </summary>
public class Mt940Parser : DomainService
{
    // MT940 tag patterns
    private static readonly Regex TagPattern = new(@"^:(\d{2}[A-Z]?):(.*)$", RegexOptions.Compiled);
    private static readonly Regex StatementLinePattern = new(
        @"^(\d{6})(\d{4})?([CD])\D?([A-Z]{3})?(\d+[,\.]\d{0,2})(\S{4})(\S{0,16})//(\S{0,16})(.*)$",
        RegexOptions.Compiled);
    private static readonly Regex BalancePattern = new(
        @"^([CD])(\d{6})([A-Z]{3})(\d+[,\.]\d{0,2})$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses an MT940 formatted bank statement string into structured transactions.
    /// </summary>
    public Mt940ParseResult Parse(string mt940Content)
    {
        if (string.IsNullOrWhiteSpace(mt940Content))
            return new Mt940ParseResult([], [], "Empty MT940 content");

        var transactions = new List<Mt940Transaction>();
        var errors = new List<string>();
        var lines = mt940Content.Split('\n');

        string? accountId = null;
        string? statementRef = null;
        string? openingBalanceStr = null;
        string? closingBalanceStr = null;
        string? currency = null;
        Mt940Transaction? currentTransaction = null;
        int lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var tagMatch = TagPattern.Match(line);
            if (tagMatch.Success)
            {
                var tag = tagMatch.Groups[1].Value;
                var value = tagMatch.Groups[2].Value;

                switch (tag)
                {
                    case "20": // Transaction reference
                        statementRef = value.Trim();
                        break;

                    case "25": // Account identification (IBAN or account number)
                        accountId = value.Trim();
                        break;

                    case "60F": // Opening balance
                        openingBalanceStr = value.Trim();
                        var obMatch = BalancePattern.Match(value.Trim());
                        if (obMatch.Success)
                            currency = obMatch.Groups[3].Value;
                        break;

                    case "61": // Statement line (transaction)
                        // Save previous transaction
                        if (currentTransaction != null)
                            transactions.Add(currentTransaction);

                        currentTransaction = ParseStatementLine(value.Trim(), lineNumber, errors);
                        break;

                    case "86": // Transaction details (belongs to previous :61:)
                        if (currentTransaction != null)
                        {
                            currentTransaction.Description = string.IsNullOrEmpty(currentTransaction.Description)
                                ? value.Trim()
                                : $"{currentTransaction.Description} {value.Trim()}";
                        }
                        break;

                    case "62F": // Closing balance (booked)
                        closingBalanceStr = value.Trim();
                        break;
                }
            }
            else if (currentTransaction != null && !line.StartsWith(':'))
            {
                // Continuation line for :86: tag
                currentTransaction.Description = $"{currentTransaction.Description} {line.Trim()}";
            }
        }

        // Save last transaction
        if (currentTransaction != null)
            transactions.Add(currentTransaction);

        // Extract reference numbers using the enhanced algorithm
        foreach (var tx in transactions)
        {
            tx.ReferenceNumber = ExtractReference(tx);
        }

        return new Mt940ParseResult(transactions, errors, null)
        {
            AccountIdentification = accountId,
            StatementReference = statementRef,
            Currency = currency,
            OpeningBalance = ParseBalance(openingBalanceStr),
            ClosingBalance = ParseBalance(closingBalanceStr)
        };
    }

    private Mt940Transaction? ParseStatementLine(string value, int lineNumber, List<string> errors)
    {
        try
        {
            // Format: YYMMDD[MMDD]CD[D]CCC[NNNN,NN]TTTT[Cust.Ref]//[Bank Ref][Extra]
            if (value.Length < 16)
            {
                errors.Add($"Line {lineNumber}: Statement line too short: '{value}'");
                return null;
            }

            // Parse date (YYMMDD)
            var dateStr = value[..6];
            if (!DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var valueDate))
            {
                errors.Add($"Line {lineNumber}: Invalid date format: '{dateStr}'");
                return null;
            }

            // Find debit/credit indicator
            int dcIndex = 6;
            // Optional entry date MMDD (4 chars)
            if (value.Length > 10 && (value[10] == 'C' || value[10] == 'D'))
                dcIndex = 10;

            var dcIndicator = value[dcIndex];
            bool isCredit = dcIndicator == 'C';

            // Parse amount (after DC indicator, skip optional 3rd char for "RC"/"RD")
            int amountStart = dcIndex + 1;
            if (amountStart < value.Length && char.IsLetter(value[amountStart]) && value[amountStart] != 'N')
                amountStart++; // Skip currency letter if present

            // Find end of amount (digits + comma/period)
            int amountEnd = amountStart;
            while (amountEnd < value.Length && (char.IsDigit(value[amountEnd]) || value[amountEnd] == ',' || value[amountEnd] == '.'))
                amountEnd++;

            var amountStr = value[amountStart..amountEnd].Replace(',', '.');
            if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                errors.Add($"Line {lineNumber}: Invalid amount: '{amountStr}'");
                return null;
            }

            // Transaction type code (4 chars after amount)
            string txTypeCode = amountEnd + 4 <= value.Length ? value[amountEnd..(amountEnd + 4)] : "";

            // Remaining: customer_reference // bank_reference [extra]
            string remaining = amountEnd + 4 < value.Length ? value[(amountEnd + 4)..] : "";
            string? customerRef = null;
            string? bankRef = null;
            string? extraDetails = null;

            var slashIdx = remaining.IndexOf("//", StringComparison.Ordinal);
            if (slashIdx >= 0)
            {
                customerRef = remaining[..slashIdx].Trim();
                var afterSlash = remaining[(slashIdx + 2)..].Trim();
                // Bank ref may be followed by extra details
                var spaceIdx = afterSlash.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    bankRef = afterSlash[..spaceIdx];
                    extraDetails = afterSlash[(spaceIdx + 1)..].Trim();
                }
                else
                {
                    bankRef = afterSlash;
                }
            }
            else
            {
                customerRef = remaining.Trim();
            }

            return new Mt940Transaction
            {
                ValueDate = valueDate,
                Amount = amount,
                IsCredit = isCredit,
                TransactionTypeCode = txTypeCode,
                CustomerReference = string.IsNullOrEmpty(customerRef) ? null : customerRef,
                BankReference = string.IsNullOrEmpty(bankRef) ? null : bankRef,
                ExtraDetails = string.IsNullOrEmpty(extraDetails) ? null : extraDetails,
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Line {lineNumber}: Parse error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts the best available reference number from an MT940 transaction.
    /// Per PR #57382: handles 16-char customer_reference overflow + NONREF sentinel.
    /// Resolution chain: customer_reference → bank_reference → transaction_reference → null
    /// </summary>
    public static string? ExtractReference(Mt940Transaction tx)
    {
        // Per PR #57382: handle customer_reference overflow at 16-char cap
        var custRef = tx.CustomerReference;
        if (!string.IsNullOrWhiteSpace(custRef))
        {
            // NONREF sentinel (case-insensitive) → fall back
            if (custRef.Trim().Equals("NONREF", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(tx.BankReference)
                    ? tx.BankReference.Trim()
                    : !string.IsNullOrWhiteSpace(tx.TransactionReference)
                        ? tx.TransactionReference.Trim()
                        : null;
            }

            // 16-char overflow: concatenate extra_details if present
            if (custRef.Length >= 16 && !string.IsNullOrWhiteSpace(tx.ExtraDetails))
                return (custRef + tx.ExtraDetails).Trim();

            return custRef.Trim();
        }

        return !string.IsNullOrWhiteSpace(tx.BankReference) ? tx.BankReference.Trim()
             : !string.IsNullOrWhiteSpace(tx.TransactionReference) ? tx.TransactionReference.Trim()
             : null;
    }

    private static decimal? ParseBalance(string? balanceStr)
    {
        if (string.IsNullOrEmpty(balanceStr)) return null;
        var match = BalancePattern.Match(balanceStr);
        if (!match.Success) return null;

        var isCredit = match.Groups[1].Value == "C";
        var amountStr = match.Groups[4].Value.Replace(',', '.');
        if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return null;

        return isCredit ? amount : -amount;
    }
}

/// <summary>Parsed MT940 transaction.</summary>
public class Mt940Transaction
{
    public DateTime ValueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsCredit { get; set; }
    public string TransactionTypeCode { get; set; } = null!;
    public string? CustomerReference { get; set; }
    public string? BankReference { get; set; }
    public string? TransactionReference { get; set; }
    public string? ExtraDetails { get; set; }
    public string? Description { get; set; }
    public string? ReferenceNumber { get; set; }

    /// <summary>Positive for credit (money in), negative for debit (money out).</summary>
    public decimal SignedAmount => IsCredit ? Amount : -Amount;
}

/// <summary>Result of parsing an MT940 file.</summary>
public class Mt940ParseResult
{
    public List<Mt940Transaction> Transactions { get; }
    public List<string> Errors { get; }
    public string? ErrorMessage { get; }
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage) && Errors.Count == 0;

    public string? AccountIdentification { get; set; }
    public string? StatementReference { get; set; }
    public string? Currency { get; set; }
    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }

    public int TransactionCount => Transactions.Count;
    public int ErrorCount => Errors.Count;

    public Mt940ParseResult(List<Mt940Transaction> transactions, List<string> errors, string? errorMessage)
    {
        Transactions = transactions;
        Errors = errors;
        ErrorMessage = errorMessage;
    }
}
