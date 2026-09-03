using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Dtos;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.QualityInspections.Default)]
public class QualityInspectionAppService : ApplicationService, IQualityInspectionAppService
{
    private readonly IRepository<QualityInspection, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public QualityInspectionAppService(
        IRepository<QualityInspection, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<PagedResultDto<QualityInspectionDto>> GetListAsync(GetQualityInspectionListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(q => q.CompanyId == input.CompanyId.Value);
        if (input.ItemId.HasValue)
            query = query.Where(q => q.ItemId == input.ItemId.Value);
        if (input.Status.HasValue)
            query = query.Where(q => q.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(q => (q.ItemName ?? "").Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(q => q.InspectionDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<QualityInspectionDto>(totalCount, items.Select(x => ObjectMapper.Map<QualityInspection, QualityInspectionDto>(x)).ToList());
    }

    public async Task<QualityInspectionDto> GetAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Create)]
    public async Task<QualityInspectionDto> CreateAsync(CreateQualityInspectionDto input)
    {
        // Validate active item
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(new[] { input.ItemId });

        // Per ERPNext PR #47746 / commit d8cb073eaf and PR #47002 / commit 8eaa2afeb7:
        // validate if QI is required and can be created after document submission
        if (input.ReferenceId.HasValue && !string.IsNullOrWhiteSpace(input.ReferenceType))
        {
            var allowAfterSubmission = await SettingProvider.IsTrueAsync(
                MyERPSettings.Stock.AllowToMakeQualityInspectionAfterPurchaseOrDelivery);

            if (!allowAfterSubmission)
            {
                var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
                var item = await itemRepo.FindAsync(input.ItemId);
                if (item != null)
                {
                    if ((input.ReferenceType == "PurchaseReceipt" || input.ReferenceType == "PurchaseInvoice")
                        && !item.InspectionRequiredBeforePurchase)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionNotRequired)
                            .WithData("item", item.ItemName)
                            .WithData("action", "Purchase");
                    }

                    if ((input.ReferenceType == "DeliveryNote" || input.ReferenceType == "SalesInvoice")
                        && !item.InspectionRequiredBeforeDelivery)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionNotRequired)
                            .WithData("item", item.ItemName)
                            .WithData("action", "Delivery");
                    }
                }

                bool isSubmitted = false;
                string docNumber = string.Empty;

                switch (input.ReferenceType)
                {
                    case "PurchaseReceipt":
                    {
                        var repo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseReceipt, Guid>>();
                        var doc = await repo.FindAsync(input.ReferenceId.Value);
                        if (doc != null && doc.Status == Core.DocumentStatus.Submitted)
                        {
                            isSubmitted = true;
                            docNumber = doc.ReceiptNumber;
                        }
                        break;
                    }
                    case "PurchaseInvoice":
                    {
                        var repo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.PurchaseInvoice, Guid>>();
                        var doc = await repo.FindAsync(input.ReferenceId.Value);
                        if (doc != null && doc.Status == Core.DocumentStatus.Submitted)
                        {
                            isSubmitted = true;
                            docNumber = doc.InvoiceNumber;
                        }
                        break;
                    }
                    case "DeliveryNote":
                    {
                        var repo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.DeliveryNote, Guid>>();
                        var doc = await repo.FindAsync(input.ReferenceId.Value);
                        if (doc != null && doc.Status == Core.DocumentStatus.Submitted)
                        {
                            isSubmitted = true;
                            docNumber = doc.DeliveryNumber;
                        }
                        break;
                    }
                    case "SalesInvoice":
                    {
                        var repo = LazyServiceProvider.LazyGetRequiredService<IRepository<Sales.Entities.SalesInvoice, Guid>>();
                        var doc = await repo.FindAsync(input.ReferenceId.Value);
                        if (doc != null && doc.Status == Core.DocumentStatus.Submitted)
                        {
                            isSubmitted = true;
                            docNumber = doc.InvoiceNumber;
                        }
                        break;
                    }
                }

                if (isSubmitted)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.QualityInspectionNotAllowedAfterSubmission)
                        .WithData("documentType", input.ReferenceType)
                        .WithData("documentNumber", docNumber);
                }
            }
        }

        var number = await _numberGenerator.GenerateAsync("QI", input.CompanyId);
        var qi = new QualityInspection(GuidGenerator.Create(), input.CompanyId, input.ItemId,
            input.InspectionType, input.InspectionDate, CurrentTenant.Id)
        {
            InspectionNumber = number,
            ItemName = input.ItemName,
            ReferenceType = input.ReferenceType,
            ReferenceId = input.ReferenceId,
            BatchNo = input.BatchNo,
            SampleSize = input.SampleSize,
            ManualInspection = input.ManualInspection,
        };

        if (input.Readings != null && input.Readings.Any())
        {
            foreach (var r in input.Readings)
                qi.AddReading(r.Specification, r.ExpectedValue, r.MinValue, r.MaxValue,
                    r.ReadingValue, r.IsNumeric, r.FormulaBased, r.Formula);
        }
        else
        {
            // Auto-load parameters from Item's Quality Inspection Template if readings not provided
            var templateRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<QualityInspectionTemplate, Guid>>();
            var template = (await templateRepo.WithDetailsAsync())
                .FirstOrDefault(t => t.ItemId == input.ItemId && t.IsEnabled);
            if (template != null)
            {
                foreach (var p in template.Parameters)
                {
                    qi.AddReading(p.Specification, p.ExpectedValue, p.MinValue, p.MaxValue,
                        null, p.IsNumeric, p.FormulaBased, p.Formula);
                }
            }
        }

        await _repository.InsertAsync(qi);
        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Submit)]
    public async Task<QualityInspectionDto> SubmitAsync(Guid id)
    {
        var qi = (await _repository.WithDetailsAsync()).First(q => q.Id == id);
        qi.Submit();
        await _repository.UpdateAsync(qi);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "QualityInspection", qi.Id, "Submitted",
            qi.CompanyId, qi.InspectionNumber, "Draft", "Submitted",
            CurrentUser.Id, tenantId: qi.TenantId));

        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }

    [Authorize(MyERPPermissions.QualityInspections.Submit)]
    public async Task<QualityInspectionDto> CancelAsync(Guid id)
    {
        var qi = await _repository.GetAsync(id);
        qi.Cancel();
        await _repository.UpdateAsync(qi);

        var activityRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        await activityRepo.InsertAsync(new MyERP.Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "QualityInspection", qi.Id, "Cancelled",
            qi.CompanyId, qi.InspectionNumber, "Submitted", "Cancelled",
            CurrentUser.Id, tenantId: qi.TenantId));

        return ObjectMapper.Map<QualityInspection, QualityInspectionDto>(qi);
    }
}

