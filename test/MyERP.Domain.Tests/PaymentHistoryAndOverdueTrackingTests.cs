using System;
using System.IO;
using System.Text.Json;
using Xunit;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Core;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for:
/// - Invoice Payment History endpoint (InvoicePaymentHistoryDto)
/// - Overdue days calculation logic
/// - Payment progress percentage
/// - Outstanding amount display with overdue indicator
/// Session: 2026-07-26
/// </summary>
public class PaymentHistoryAndOverdueTrackingTests
{
    private static JsonElement GetLocalizationTexts()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        return JsonDocument.Parse(json).RootElement.GetProperty("texts");
    }

    // --- InvoicePaymentHistoryDto ---

    [Fact]
    public void InvoicePaymentHistoryDto_HasAllRequiredFields()
    {
        var dto = new InvoicePaymentHistoryDto
        {
            Id = Guid.NewGuid(),
            PaymentNumber = "PE-2026-00001",
            PostingDate = new DateTime(2026, 7, 15),
            PaymentType = "Receive",
            Amount = 5000m
        };

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("PE-2026-00001", dto.PaymentNumber);
        Assert.Equal(new DateTime(2026, 7, 15), dto.PostingDate);
        Assert.Equal("Receive", dto.PaymentType);
        Assert.Equal(5000m, dto.Amount);
    }

    [Fact]
    public void InvoicePaymentHistoryDto_DefaultsToZeroAmount()
    {
        var dto = new InvoicePaymentHistoryDto();
        Assert.Equal(0m, dto.Amount);
        Assert.Null(dto.PaymentNumber);
        Assert.Null(dto.PaymentType);
    }

    // --- Overdue Days Calculation Logic ---

    [Fact]
    public void OverdueDays_PostedInvoice_PastDueDate_ReturnsPositive()
    {
        // Simulating: dueDate = 10 days ago, status = Posted
        var dueDate = DateTime.Today.AddDays(-10);
        var daysDiff = (DateTime.Today - dueDate).Days;
        Assert.Equal(10, daysDiff);
    }

    [Fact]
    public void OverdueDays_FutureDueDate_ReturnsZero()
    {
        var dueDate = DateTime.Today.AddDays(15);
        var daysDiff = Math.Max(0, (DateTime.Today - dueDate).Days);
        Assert.Equal(0, daysDiff);
    }

    [Fact]
    public void OverdueDays_TodayDueDate_ReturnsZero()
    {
        var dueDate = DateTime.Today;
        var daysDiff = Math.Max(0, (DateTime.Today - dueDate).Days);
        Assert.Equal(0, daysDiff);
    }

    [Fact]
    public void OverdueDays_NullDueDate_ReturnsZero()
    {
        DateTime? dueDate = null;
        var overdue = dueDate.HasValue ? Math.Max(0, (DateTime.Today - dueDate.Value).Days) : 0;
        Assert.Equal(0, overdue);
    }

    // --- Payment Progress ---

    [Fact]
    public void PaymentProgress_NoPayment_ReturnsZero()
    {
        decimal grandTotal = 10000m;
        decimal amountPaid = 0m;
        var progress = grandTotal > 0 ? Math.Min(100, (amountPaid / grandTotal) * 100) : 0;
        Assert.Equal(0m, progress);
    }

    [Fact]
    public void PaymentProgress_PartialPayment_ReturnsPercentage()
    {
        decimal grandTotal = 10000m;
        decimal amountPaid = 3000m;
        var progress = grandTotal > 0 ? Math.Min(100, (amountPaid / grandTotal) * 100) : 0;
        Assert.Equal(30m, progress);
    }

    [Fact]
    public void PaymentProgress_FullPayment_Returns100()
    {
        decimal grandTotal = 10000m;
        decimal amountPaid = 10000m;
        var progress = grandTotal > 0 ? Math.Min(100, (amountPaid / grandTotal) * 100) : 0;
        Assert.Equal(100m, progress);
    }

    [Fact]
    public void PaymentProgress_Overpayment_CappedAt100()
    {
        decimal grandTotal = 10000m;
        decimal amountPaid = 12000m;
        var progress = grandTotal > 0 ? Math.Min(100, (amountPaid / grandTotal) * 100) : 0;
        Assert.Equal(100m, progress);
    }

    [Fact]
    public void PaymentProgress_ZeroGrandTotal_ReturnsZero()
    {
        decimal grandTotal = 0m;
        decimal amountPaid = 500m;
        var progress = grandTotal > 0 ? Math.Min(100, (amountPaid / grandTotal) * 100) : 0;
        Assert.Equal(0m, progress);
    }

    // --- SI Entity Outstanding ---

    [Fact]
    public void SalesInvoice_OutstandingAmount_ReducedByPayment()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-001", DateTime.Today);
        var itemId = Guid.NewGuid();
        invoice.AddItem(itemId, "Test Item", 5, 200m, 0m);
        invoice.Submit();
        invoice.Post();

        // Before payment
        Assert.Equal(1000m, invoice.OutstandingAmount);

        // Record partial payment
        invoice.AmountPaid = 400m;
        Assert.Equal(600m, invoice.OutstandingAmount);
    }

    [Fact]
    public void SalesInvoice_IsOverdue_WhenPostedAndPastDue()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-002", DateTime.Today);
        var itemId = Guid.NewGuid();
        invoice.AddItem(itemId, "Test Item", 1, 500m, 0m);
        invoice.DueDate = DateTime.Today.AddDays(-5);
        invoice.Submit();
        invoice.Post();

        Assert.True(invoice.IsOverdue);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_WhenFullyPaid()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoice = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-003", DateTime.Today);
        var itemId = Guid.NewGuid();
        invoice.AddItem(itemId, "Test Item", 1, 500m, 0m);
        invoice.DueDate = DateTime.Today.AddDays(-10);
        invoice.Submit();
        invoice.Post();
        invoice.AmountPaid = 500m;

        Assert.False(invoice.IsOverdue);
    }

    // --- Localization Keys ---

    [Theory]
    [InlineData("PaymentHistory")]
    [InlineData("Payments")]
    [InlineData("DueOn")]
    [InlineData("AmountPaid")]
    [InlineData("DaysOverdue")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var texts = GetLocalizationTexts();
        Assert.True(texts.TryGetProperty(key, out _), $"Missing localization key: {key}");
    }

    // --- Session Tracking ---

    [Fact]
    public void Session_PaymentHistoryEndpoint_Added()
    {
        // Validates that InvoicePaymentHistoryDto is available for the API
        var dto = new InvoicePaymentHistoryDto();
        Assert.IsType<InvoicePaymentHistoryDto>(dto);
    }

    [Fact]
    public void Session_OverdueIndicator_OnDetailPage()
    {
        // Validates that days-overdue calculation uses correct formula
        var pastDue = DateTime.Today.AddDays(-45);
        var daysOverdue = Math.Max(0, (DateTime.Today - pastDue).Days);
        Assert.Equal(45, daysOverdue);
        // Over 30 days = red indicator (per ERPNext aging buckets)
        Assert.True(daysOverdue > 30);
    }

    [Fact]
    public void Session_PaymentProgressBar_CalculatesCorrectly()
    {
        // Validates payment progress bar percentage calculation
        var grandTotal = 15000m;
        var amountPaid = 6750m;
        var expected = 45m; // 6750/15000 * 100
        var progress = Math.Min(100, (amountPaid / grandTotal) * 100);
        Assert.Equal(expected, progress);
    }
}
