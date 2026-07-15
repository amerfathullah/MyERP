using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using MyERP.Manufacturing;
using MyERP.Sales;
using MyERP.Purchasing;
using MyERP.Shared;
using Xunit;

namespace MyERP;

/// <summary>
/// DTO contract tests that verify Create DTOs have required validation attributes.
/// Prevents silent data loss from missing required fields during API calls.
/// </summary>
public class DtoContractTests
{
    // === CreateSalesInvoiceDto ===

    [Fact]
    public void CreateSalesInvoiceDto_HasRequiredCompanyId()
    {
        var prop = typeof(CreateSalesInvoiceDto).GetProperty("CompanyId");
        Assert.NotNull(prop);
        Assert.True(prop!.GetCustomAttributes<RequiredAttribute>().Any());
    }

    [Fact]
    public void CreateSalesInvoiceDto_HasRequiredCustomerId()
    {
        var prop = typeof(CreateSalesInvoiceDto).GetProperty("CustomerId");
        Assert.NotNull(prop);
        Assert.True(prop!.GetCustomAttributes<RequiredAttribute>().Any());
    }

    [Fact]
    public void CreateSalesInvoiceDto_HasRequiredItems()
    {
        var prop = typeof(CreateSalesInvoiceDto).GetProperty("Items");
        Assert.NotNull(prop);
        Assert.True(prop!.GetCustomAttributes<RequiredAttribute>().Any());
    }

    [Fact]
    public void CreateSalesInvoiceDto_DefaultCurrency_IsMYR()
    {
        var dto = new CreateSalesInvoiceDto();
        Assert.Equal("MYR", dto.CurrencyCode);
    }

    [Fact]
    public void CreateSalesInvoiceDto_Items_DefaultEmpty()
    {
        var dto = new CreateSalesInvoiceDto();
        Assert.NotNull(dto.Items);
        Assert.Empty(dto.Items);
    }

    [Fact]
    public void CreateSalesInvoiceDto_HasReturnFields()
    {
        var dto = new CreateSalesInvoiceDto();
        Assert.False(dto.IsReturn);
        Assert.Null(dto.ReturnAgainstId);
    }

    [Fact]
    public void CreateSalesInvoiceDto_HasOpeningField()
    {
        var dto = new CreateSalesInvoiceDto();
        Assert.False(dto.IsOpening);
    }

    // === CreateSalesInvoiceItemDto ===

    [Fact]
    public void CreateSalesInvoiceItemDto_HasRequiredFields()
    {
        var type = typeof(CreateSalesInvoiceItemDto);
        Assert.True(type.GetProperty("ItemId")!.GetCustomAttributes<RequiredAttribute>().Any());
        Assert.True(type.GetProperty("Description")!.GetCustomAttributes<RequiredAttribute>().Any());
        Assert.True(type.GetProperty("Quantity")!.GetCustomAttributes<RequiredAttribute>().Any());
    }

    // === CreatePurchaseOrderDto ===

    [Fact]
    public void CreatePurchaseOrderDto_HasRequiredFields()
    {
        var type = typeof(CreatePurchaseOrderDto);
        Assert.NotNull(type.GetProperty("CompanyId"));
        Assert.NotNull(type.GetProperty("SupplierId"));
        Assert.NotNull(type.GetProperty("Items"));
    }

    [Fact]
    public void CreatePurchaseOrderDto_Items_DefaultEmpty()
    {
        var dto = new CreatePurchaseOrderDto();
        Assert.NotNull(dto.Items);
        Assert.Empty(dto.Items);
    }

    // === WorkstationDto ===

    [Fact]
    public void WorkstationDto_HasAllDisplayFields()
    {
        var type = typeof(WorkstationDto);
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("WorkstationType"));
        Assert.NotNull(type.GetProperty("ProductionCapacity"));
        Assert.NotNull(type.GetProperty("HourRate"));
        Assert.NotNull(type.GetProperty("IsActive"));
        Assert.NotNull(type.GetProperty("Costs"));
        Assert.NotNull(type.GetProperty("WorkingHours"));
    }

    [Fact]
    public void CreateWorkstationDto_HasRequiredFields()
    {
        var type = typeof(CreateWorkstationDto);
        Assert.NotNull(type.GetProperty("CompanyId"));
        Assert.NotNull(type.GetProperty("Name"));
        Assert.NotNull(type.GetProperty("ProductionCapacity"));
    }

    [Fact]
    public void CreateWorkstationDto_DefaultCapacity_IsOne()
    {
        var dto = new CreateWorkstationDto();
        Assert.Equal(1, dto.ProductionCapacity);
    }

    // === CompanyFilteredPagedRequestDto ===

    [Fact]
    public void CompanyFilteredPagedRequestDto_InheritsPagedRequest()
    {
        var dto = new CompanyFilteredPagedRequestDto();
        Assert.Equal(0, dto.SkipCount);
        Assert.True(dto.MaxResultCount > 0);
    }

    [Fact]
    public void CompanyFilteredPagedRequestDto_HasFilterFields()
    {
        var type = typeof(CompanyFilteredPagedRequestDto);
        Assert.NotNull(type.GetProperty("CompanyId"));
        Assert.NotNull(type.GetProperty("Filter"));
        Assert.NotNull(type.GetProperty("Status"));
    }

    // === Data Annotation Validation ===

    [Fact]
    public void CreateSalesInvoiceDto_CompanyId_IsRequiredAttribute()
    {
        // [Required] on Guid doesn't prevent Guid.Empty at validation level
        // but it documents the field as required in API schema (OpenAPI/Swagger)
        var prop = typeof(CreateSalesInvoiceDto).GetProperty("CompanyId");
        Assert.NotNull(prop);
        Assert.True(prop!.GetCustomAttributes<RequiredAttribute>().Any());
        // AppService validates Guid.Empty separately via Check.NotDefaultOrNull
    }

    [Fact]
    public void CreateSalesInvoiceDto_ValidationFails_WithEmptyItems()
    {
        var dto = new CreateSalesInvoiceDto
        {
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IssueDate = DateTime.Today,
            Items = [], // empty - should fail MinLength(1)
        };
        var results = ValidateModel(dto);
        Assert.Contains(results, r => r.MemberNames.Contains("Items"));
    }

    [Fact]
    public void CreateSalesInvoiceDto_ValidationPasses_WithValidData()
    {
        var dto = new CreateSalesInvoiceDto
        {
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            IssueDate = DateTime.Today,
            Items = [new CreateSalesInvoiceItemDto { ItemId = Guid.NewGuid(), Description = "Widget", Quantity = 5m, UnitPrice = 100m }],
        };
        var results = ValidateModel(dto);
        Assert.Empty(results);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
