using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

/// <summary>
/// Unit tests for QualityInspectionTemplateAppService parameter validations and CRUD workflows
/// per ERPNext PR #58940 / commits aae67879ae, 0ae42aec9b, a6203ca59df034f21984175a25cbc1233515f41c.
/// </summary>
public class QualityInspectionTemplateAppServiceTests
{
    private readonly IRepository<QualityInspectionTemplate, Guid> _templateRepo = Substitute.For<IRepository<QualityInspectionTemplate, Guid>>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IAbpLazyServiceProvider _lazySp = Substitute.For<IAbpLazyServiceProvider>();
    private readonly QualityInspectionTemplateAppService _service;

    public QualityInspectionTemplateAppServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _lazySp.LazyGetService<IGuidGenerator>().Returns(_guidGenerator);
        _lazySp.LazyGetRequiredService<IGuidGenerator>().Returns(_guidGenerator);

        _service = new QualityInspectionTemplateAppService(_templateRepo)
        {
            LazyServiceProvider = _lazySp
        };
    }

    [Fact]
    public async Task CreateAsync_DuplicateParameter_ThrowsValidationException()
    {
        // Arrange
        var input = new CreateQiTemplateDto
        {
            Name = "Duplicate Parameter Template",
            Parameters = new List<CreateQiTemplateParameterDto>
            {
                new() { Specification = "Hardness", ExpectedValue = "Pass", IsNumeric = false },
                new() { Specification = "Hardness", ExpectedValue = "Fail", IsNumeric = false }
            }
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<BusinessException>(() => _service.CreateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]?.ToString()!.ShouldContain("Row #2: Parameter Hardness is added more than once");
    }

    [Fact]
    public async Task CreateAsync_NumericMinGreaterThanMax_ThrowsValidationException()
    {
        // Arrange
        var input = new CreateQiTemplateDto
        {
            Name = "Invalid Bounds Template",
            Parameters = new List<CreateQiTemplateParameterDto>
            {
                new() { Specification = "Thickness", MinValue = 20m, MaxValue = 10m, IsNumeric = true }
            }
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<BusinessException>(() => _service.CreateAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]?.ToString()!.ShouldContain("Row #1: Minimum Value cannot be greater than Maximum Value for parameter Thickness");
    }

    [Fact]
    public async Task CreateAsync_ValidTemplate_InsertsAndReturnsDto()
    {
        // Arrange
        var input = new CreateQiTemplateDto
        {
            Name = "Standard Raw Material Template",
            Description = "Standard QC checklist",
            Parameters = new List<CreateQiTemplateParameterDto>
            {
                new() { Specification = "Visual Check", ExpectedValue = "Clean", IsNumeric = false },
                new() { Specification = "Density", MinValue = 1.0m, MaxValue = 1.5m, IsNumeric = true }
            }
        };

        // Act
        var result = await _service.CreateAsync(input);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Standard Raw Material Template");
        result.ParameterCount.ShouldBe(2);
        await _templateRepo.Received(1).InsertAsync(Arg.Is<QualityInspectionTemplate>(t =>
            t.Name == "Standard Raw Material Template" && t.Parameters.Count == 2));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesPropertiesAndReplacesParametersWithValidation()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new QualityInspectionTemplate(templateId, "Old Name");
        template.AddParameter(Guid.NewGuid(), "Old Param", expectedValue: "Pass");

        var list = new List<QualityInspectionTemplate> { template }.AsQueryable();
        _templateRepo.WithDetailsAsync().Returns(Task.FromResult(list));
        _templateRepo.GetAsync(templateId).Returns(Task.FromResult(template));

        var updateInput = new UpdateQiTemplateDto
        {
            Name = "New Name",
            Description = "Updated description",
            Parameters = new List<CreateQiTemplateParameterDto>
            {
                new() { Specification = "New Length", MinValue = 5m, MaxValue = 15m, IsNumeric = true },
                new() { Specification = "New Finish", ExpectedValue = "Polished", IsNumeric = false }
            }
        };

        // Act
        var result = await _service.UpdateAsync(templateId, updateInput);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("New Name");
        result.Description.ShouldBe("Updated description");
        result.ParameterCount.ShouldBe(2);
        template.Parameters.Count.ShouldBe(2);
        template.Parameters[0].Specification.ShouldBe("New Length");
        await _templateRepo.Received(1).UpdateAsync(template);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateParameter_ThrowsValidationException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new QualityInspectionTemplate(templateId, "Template To Update");
        var list = new List<QualityInspectionTemplate> { template }.AsQueryable();
        _templateRepo.WithDetailsAsync().Returns(Task.FromResult(list));
        _templateRepo.GetAsync(templateId).Returns(Task.FromResult(template));

        var updateInput = new UpdateQiTemplateDto
        {
            Name = "Template To Update",
            Parameters = new List<CreateQiTemplateParameterDto>
            {
                new() { Specification = "Surface", ExpectedValue = "Smooth", IsNumeric = false },
                new() { Specification = "Surface", ExpectedValue = "Matte", IsNumeric = false }
            }
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<BusinessException>(() => _service.UpdateAsync(templateId, updateInput));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]?.ToString()!.ShouldContain("Row #2: Parameter Surface is added more than once");
    }
}
