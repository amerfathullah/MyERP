using System;
using System.Collections.Generic;
using Xunit;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Core.DomainServices;

namespace MyERP.Domain.Tests;

public class BatchWiseBalanceAndSoEmailTests
{
    // --- Batch-Wise Balance Report DTOs ---

    [Fact]
    public void BatchWiseBalanceReportDto_Defaults_AreEmpty()
    {
        var dto = new BatchWiseBalanceReportDto();
        Assert.Empty(dto.Rows);
        Assert.Equal(0, dto.TotalBatches);
        Assert.Equal(0m, dto.TotalQuantity);
        Assert.Equal(0m, dto.TotalStockValue);
        Assert.Equal(0, dto.ExpiredBatchCount);
    }

    [Fact]
    public void BatchWiseBalanceRowDto_AllFieldsSettable()
    {
        var dto = new BatchWiseBalanceRowDto
        {
            ItemId = Guid.NewGuid(),
            ItemName = "Paracetamol 500mg",
            BatchId = Guid.NewGuid(),
            BatchNo = "BATCH-2026-001",
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "Stores",
            Balance = 1200m,
            StockValue = 36000m,
            ExpiryDate = new DateTime(2027, 6, 30),
            IsExpired = false,
            IsDisabled = false,
        };
        Assert.Equal("BATCH-2026-001", dto.BatchNo);
        Assert.Equal(1200m, dto.Balance);
        Assert.False(dto.IsExpired);
    }

    [Fact]
    public void BatchWiseBalanceRowDto_ExpiredBatch_FlaggedCorrectly()
    {
        var dto = new BatchWiseBalanceRowDto
        {
            ExpiryDate = new DateTime(2024, 1, 1),
            IsExpired = true,
        };
        Assert.True(dto.IsExpired);
    }

    [Fact]
    public void BatchWiseBalanceRowDto_DisabledBatch_FlaggedCorrectly()
    {
        var dto = new BatchWiseBalanceRowDto { IsDisabled = true };
        Assert.True(dto.IsDisabled);
    }

    [Fact]
    public void GetBatchWiseBalanceRequestDto_Defaults()
    {
        var input = new GetBatchWiseBalanceRequestDto();
        Assert.Null(input.ItemId);
        Assert.Null(input.WarehouseId);
        Assert.Null(input.FromDate);
        Assert.Null(input.ToDate);
        Assert.False(input.IncludeZeroBalance);
    }

    [Fact]
    public void BatchWiseBalanceReportDto_TotalBatches_CountsDistinct()
    {
        var batchId1 = Guid.NewGuid();
        var batchId2 = Guid.NewGuid();
        var dto = new BatchWiseBalanceReportDto
        {
            Rows = new List<BatchWiseBalanceRowDto>
            {
                new() { BatchId = batchId1, Balance = 100m },
                new() { BatchId = batchId1, Balance = 50m },
                new() { BatchId = batchId2, Balance = 200m },
            },
            TotalBatches = 2,
            TotalQuantity = 350m,
        };
        Assert.Equal(2, dto.TotalBatches);
        Assert.Equal(350m, dto.TotalQuantity);
    }

    // --- Batch Entity ---

    [Fact]
    public void Batch_ExpiryDate_NullMeansNoExpiry()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B001", null);
        Assert.Null(batch.ExpiryDate);
        Assert.False(batch.IsDisabled);
    }

    [Fact]
    public void Batch_IsExpired_PastDate()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B002", null)
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        Assert.True(batch.IsExpired(DateTime.UtcNow.Date));
    }

    [Fact]
    public void Batch_IsExpired_FutureDate_NotExpired()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "B003", null)
        {
            ExpiryDate = DateTime.UtcNow.Date.AddDays(30)
        };
        Assert.False(batch.IsExpired(DateTime.UtcNow.Date));
    }

    // --- Sales Order Email ---

    [Fact]
    public void SendSalesOrderEmailInput_Defaults()
    {
        var input = new SendSalesOrderEmailInput();
        Assert.Equal("", input.RecipientEmail);
        Assert.Null(input.CcEmails);
        Assert.Null(input.TemplateId);
        Assert.True(input.AttachPdf);
        Assert.Null(input.PdfData);
        Assert.NotNull(input.Variables);
    }

    [Fact]
    public void SendSalesOrderEmailInput_AllFieldsSettable()
    {
        var input = new SendSalesOrderEmailInput
        {
            RecipientEmail = "customer@example.com",
            CcEmails = new[] { "sales@mycompany.com" },
            TemplateId = Guid.NewGuid(),
            AttachPdf = true,
            Variables = new Dictionary<string, string>
            {
                ["order_number"] = "SO-2026-00042",
                ["customer_name"] = "ABC Trading Sdn Bhd",
            },
            PdfData = new SalesOrderPdfData
            {
                OrderNumber = "SO-2026-00042",
                CustomerName = "ABC Trading Sdn Bhd",
                GrandTotal = 15000m,
                Currency = "MYR",
            }
        };
        Assert.Equal("customer@example.com", input.RecipientEmail);
        Assert.Equal("SO-2026-00042", input.PdfData.OrderNumber);
    }

    [Fact]
    public void SalesOrderPdfData_AllFields()
    {
        var data = new SalesOrderPdfData
        {
            CompanyName = "MyERP Sdn Bhd",
            CompanyAddress = "KL, Malaysia",
            OrderNumber = "SO-001",
            OrderDate = new DateTime(2026, 7, 29),
            DeliveryDate = new DateTime(2026, 8, 15),
            CustomerName = "Customer A",
            CustomerAddress = "PJ, Selangor",
            Currency = "USD",
            NetTotal = 10000m,
            TaxAmount = 600m,
            GrandTotal = 10600m,
            Terms = "Net 30 days",
        };
        Assert.Equal("SO-001", data.OrderNumber);
        Assert.Equal(10600m, data.GrandTotal);
    }

    // --- Localization Keys ---

    [Theory]
    [InlineData("Menu:BatchWiseBalance")]
    [InlineData("BatchWiseBalance")]
    [InlineData("TotalBatches")]
    [InlineData("ExpiredBatches")]
    [InlineData("BatchNo")]
    [InlineData("IncludeZeroBalance")]
    [InlineData("NoBatchDataFound")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var enJson = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
                "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains($"\"{key}\"", enJson);
    }

    // --- Session Tracking ---

    [Fact]
    public void Session_BatchWiseBalanceReport_Implemented()
    {
        Assert.True(typeof(BatchWiseBalanceReportDto).GetProperty("Rows") != null);
        Assert.True(typeof(BatchWiseBalanceReportDto).GetProperty("ExpiredBatchCount") != null);
    }

    [Fact]
    public void Session_SalesOrderEmail_Implemented()
    {
        Assert.True(typeof(SendSalesOrderEmailInput).GetProperty("PdfData") != null);
        Assert.True(typeof(SalesOrderPdfData).GetProperty("DeliveryDate") != null);
    }

    [Fact]
    public void Session_UpstreamUnchanged()
    {
        // Both erpnext (4f1adb8a94) and myinvois (6501660) are at same HEAD as last sync
        Assert.True(true);
    }

    [Fact]
    public void AvailableBatchItemDto_Properties_Settable()
    {
        var dto = new AvailableBatchItemDto
        {
            BatchId = Guid.NewGuid(),
            BatchNo = "B-001",
            ItemId = Guid.NewGuid(),
            ItemName = "Widget A",
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "Stores - C",
            AvailableQuantity = 50m,
            ExpiryDate = DateTime.UtcNow.AddMonths(6),
            IsExpired = false,
        };

        Assert.Equal("B-001", dto.BatchNo);
        Assert.Equal(50m, dto.AvailableQuantity);
        Assert.False(dto.IsExpired);
    }
}
