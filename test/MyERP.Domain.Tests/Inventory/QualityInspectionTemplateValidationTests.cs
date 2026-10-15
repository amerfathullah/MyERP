using System;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for Quality Inspection Template parameter validations per ERPNext PR #58940 / commits aae67879ae, 0ae42aec9b, a6203ca59df034f21984175a25cbc1233515f41c.
/// Verifies duplicate parameter identification with row number and specification name,
/// min/max value bounds for numeric criteria, required acceptance values for non-numeric criteria,
/// and non-empty formula requirement for formula-based criteria.
/// </summary>
public class QualityInspectionTemplateValidationTests
{
    [Fact]
    public void Validate_DuplicateParameter_ThrowsValidationFailedWithRowAndParameter()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Colour Inspection Template");
        template.AddParameter(Guid.NewGuid(), "Colour", expectedValue: "Red", isNumeric: false);
        template.AddParameter(Guid.NewGuid(), "Colour", expectedValue: "Blue", isNumeric: false);

        // Act & Assert
        var ex = Assert.Throws<BusinessException>(() => template.Validate());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        var detail = ex.Data["detail"]?.ToString();
        Assert.NotNull(detail);
        Assert.Contains("Row #2: Parameter Colour is added more than once", detail);
    }

    [Fact]
    public void Validate_NumericMinGreaterThanMax_ThrowsValidationFailedWithRowAndParameter()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Dimension Inspection Template");
        template.AddParameter(Guid.NewGuid(), "Length", minValue: 10m, maxValue: 5m, isNumeric: true);

        // Act & Assert
        var ex = Assert.Throws<BusinessException>(() => template.Validate());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        var detail = ex.Data["detail"]?.ToString();
        Assert.NotNull(detail);
        Assert.Contains("Row #1: Minimum Value cannot be greater than Maximum Value for parameter Length", detail);
    }

    [Fact]
    public void Validate_NonNumericWithoutExpectedValue_ThrowsValidationFailedWithRowAndParameter()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Visual Inspection Template");
        template.AddParameter(Guid.NewGuid(), "Visual Check", expectedValue: null, isNumeric: false);

        // Act & Assert
        var ex = Assert.Throws<BusinessException>(() => template.Validate());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        var detail = ex.Data["detail"]?.ToString();
        Assert.NotNull(detail);
        Assert.Contains("Row #1: Acceptance Criteria Value is required for parameter Visual Check", detail);
    }

    [Fact]
    public void Validate_FormulaBasedWithoutFormula_ThrowsValidationFailedWithRowAndParameter()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Strength Inspection Template");
        template.AddParameter(Guid.NewGuid(), "Tensile Strength", formulaBased: true, formula: "   ");

        // Act & Assert
        var ex = Assert.Throws<BusinessException>(() => template.Validate());
        Assert.Equal(MyERPDomainErrorCodes.ValidationFailed, ex.Code);
        var detail = ex.Data["detail"]?.ToString();
        Assert.NotNull(detail);
        Assert.Contains("Row #1: Acceptance Criteria Formula is required for parameter Tensile Strength", detail);
    }

    [Fact]
    public void Validate_ValidParameters_PassesSuccessfully()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Comprehensive Inspection Template");
        template.AddParameter(Guid.NewGuid(), "Length", minValue: 5m, maxValue: 10m, isNumeric: true);
        template.AddParameter(Guid.NewGuid(), "Colour", expectedValue: "Red", isNumeric: false);
        template.AddParameter(Guid.NewGuid(), "Tensile", formulaBased: true, formula: "reading > 10");

        // Act & Assert (does not throw)
        template.Validate();
        Assert.Equal(3, template.Parameters.Count);
    }

    [Fact]
    public void ClearParameters_RemovesAllParameters()
    {
        // Arrange
        var template = new QualityInspectionTemplate(Guid.NewGuid(), "Template With Parameters");
        template.AddParameter(Guid.NewGuid(), "Param 1", expectedValue: "Pass");
        template.AddParameter(Guid.NewGuid(), "Param 2", expectedValue: "Pass");
        Assert.Equal(2, template.Parameters.Count);

        // Act
        template.ClearParameters();

        // Assert
        Assert.Empty(template.Parameters);
    }
}
