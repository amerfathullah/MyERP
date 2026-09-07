using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class OperationDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkstationId { get; set; }
    public string? WorkstationType { get; set; }
    public Guid? WorkstationTypeId { get; set; }
    public bool CreateJobCardBasedOnBatchSize { get; set; }
    public int BatchSize { get; set; }
    public bool IsCorrectiveOperation { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalOperationTime { get; set; }
    public SubOperationDto[] SubOperations { get; set; } = [];
}

public class SubOperationDto
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public decimal TimeInMins { get; set; }
    public string? Description { get; set; }
}

public class CreateSubOperationDto
{
    [Required]
    public Guid OperationId { get; set; }
    public decimal TimeInMins { get; set; }
    public string? Description { get; set; }
}

public class RoutingDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public RoutingOperationDto[] Operations { get; set; } = [];
}

public class RoutingOperationDto
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public Guid? WorkstationId { get; set; }
    public decimal OperatingCost { get; set; }
    public bool BatchSplit { get; set; }
    public decimal? WeightPerPiece { get; set; }
}

public class CreateOperationDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkstationId { get; set; }
    public string? WorkstationType { get; set; }
    public Guid? WorkstationTypeId { get; set; }
    public bool CreateJobCardBasedOnBatchSize { get; set; }
    public int BatchSize { get; set; }
    public bool IsCorrectiveOperation { get; set; }
    public bool IsActive { get; set; } = true;
    public CreateSubOperationDto[] SubOperations { get; set; } = [];
}

public class CreateRoutingDto
{
    public string Name { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public CreateRoutingOperationDto[] Operations { get; set; } = [];
}

public class CreateRoutingOperationDto
{
    public Guid OperationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public Guid? WorkstationId { get; set; }
    public bool BatchSplit { get; set; }
    public decimal? WeightPerPiece { get; set; }
}
