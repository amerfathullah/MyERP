using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.EInvoice.Entities;
using MyERP.EInvoice.Services;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.EInvoice;

[Authorize(MyERPPermissions.EInvoice.Default)]
public class EInvoiceAppService : ApplicationService, IEInvoiceAppService
{
    private readonly EInvoiceService _eInvoiceService;
    private readonly InvoiceDocumentBuilder _documentBuilder;
    private readonly InvoiceDocumentSigner _documentSigner;
    private readonly EInvoiceValidationService _validationService;
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly ISettingProvider _settingProvider;

    public EInvoiceAppService(
        EInvoiceService eInvoiceService,
        InvoiceDocumentBuilder documentBuilder,
        InvoiceDocumentSigner documentSigner,
        EInvoiceValidationService validationService,
        IRepository<EInvoiceSubmission, Guid> submissionRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Company, Guid> companyRepository,
        ISettingProvider settingProvider)
    {
        _eInvoiceService = eInvoiceService;
        _documentBuilder = documentBuilder;
        _documentSigner = documentSigner;
        _validationService = validationService;
        _submissionRepository = submissionRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
        _settingProvider = settingProvider;
    }

    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task<EInvoiceSubmissionDto> SubmitAsync(SubmitEInvoiceDto input)
    {
        // Step 1: Pre-submission validation
        if (input.SourceDocumentType == "SalesInvoice")
        {
            var invoice = await _salesInvoiceRepository.GetAsync(input.SourceDocumentId, includeDetails: true);
            await _validationService.EnsureValidForSubmissionAsync(invoice, input.CompanyId);
        }

        // Step 2: Build UBL 2.1 XML from source document
        var documentData = await BuildDocumentDataAsync(input.CompanyId, input.SourceDocumentType, input.SourceDocumentId);
        var xmlDocument = _documentBuilder.Build(documentData);

        // Step 3: Digital signature (XAdES) if certificate is configured
        var pfxBase64 = await _settingProvider.GetOrNullAsync("EInvoice.CertificatePfxBase64");
        var pfxPassword = await _settingProvider.GetOrNullAsync("EInvoice.CertificatePassword");
        if (!string.IsNullOrEmpty(pfxBase64) && !string.IsNullOrEmpty(pfxPassword))
        {
            var pfxBytes = Convert.FromBase64String(pfxBase64);
            xmlDocument = _documentSigner.Sign(xmlDocument, pfxBytes, pfxPassword);
        }

        // Step 4: Get credentials from settings (not hardcoded)
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException("MyERP:EInvoice:00010")
                .WithData("reason", "LHDN access token not configured. Please authenticate first.");

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        // Step 5: Submit to LHDN
        var submission = await _eInvoiceService.SubmitAsync(
            input.CompanyId,
            input.SourceDocumentType,
            input.SourceDocumentId,
            xmlDocument,
            accessToken,
            environment,
            CurrentTenant.Id);

        // Update source document e-Invoice status
        await UpdateSourceDocumentStatusAsync(input.SourceDocumentType, input.SourceDocumentId, submission);

        return MapToDto(submission);
    }

    private async Task<EInvoiceDocumentData> BuildDocumentDataAsync(Guid companyId, string sourceDocType, Guid sourceDocId)
    {
        var company = await _companyRepository.GetAsync(companyId);

        var supplier = new EInvoicePartyData
        {
            Name = company.Name,
            Tin = company.TaxId ?? "",
            IdType = "BRN",
            IdValue = company.RegistrationNumber ?? "",
            SstRegistration = company.SstRegistrationNumber,
            Address = company.Address,
            City = company.City,
            State = company.State,
            PostalCode = company.PostalCode,
            CountryCode = company.Country ?? "MYS",
        };

        return sourceDocType switch
        {
            "SalesInvoice" => await BuildFromSalesInvoiceAsync(sourceDocId, supplier),
            "PurchaseInvoice" => await BuildFromPurchaseInvoiceAsync(sourceDocId, supplier, company),
            _ => throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.UnsupportedEntityType)
                .WithData("entityType", sourceDocType),
        };
    }

    private async Task<EInvoiceDocumentData> BuildFromSalesInvoiceAsync(Guid invoiceId, EInvoicePartyData supplier)
    {
        var invoice = await _salesInvoiceRepository.GetAsync(invoiceId, includeDetails: true);
        var customer = await _customerRepository.GetAsync(invoice.CustomerId);

        return new EInvoiceDocumentData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DocumentTypeCode = invoice.EInvoiceDocType?.ToString("D2") ?? "01",
            CurrencyCode = invoice.CurrencyCode,
            Supplier = supplier,
            Buyer = new EInvoicePartyData
            {
                Name = customer.Name,
                Tin = customer.Tin ?? "EI00000000020",
                IdType = customer.IdType ?? "BRN",
                IdValue = customer.IdValue ?? customer.RegistrationNumber ?? "",
                SstRegistration = customer.SstRegistrationNumber,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                PostalCode = customer.PostalCode,
                CountryCode = customer.Country ?? "MYS",
            },
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            Lines = invoice.Items.Select(item => new EInvoiceLineData
            {
                Description = item.Description,
                Uom = item.Uom ?? "C62",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxAmount = item.TaxAmount,
            }).ToList(),
        };
    }

    private async Task<EInvoiceDocumentData> BuildFromPurchaseInvoiceAsync(
        Guid invoiceId, EInvoicePartyData buyer, Company company)
    {
        var invoice = await _purchaseInvoiceRepository.GetAsync(invoiceId, includeDetails: true);
        var supplier = await _supplierRepository.GetAsync(invoice.SupplierId);

        // For purchase: supplier is the seller, company is the buyer
        return new EInvoiceDocumentData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DocumentTypeCode = "01",
            CurrencyCode = invoice.CurrencyCode,
            Supplier = new EInvoicePartyData
            {
                Name = supplier.Name,
                Tin = invoice.SupplierTin ?? supplier.Tin ?? "EI00000000020",
                IdType = supplier.IdType ?? "BRN",
                IdValue = supplier.IdValue ?? supplier.RegistrationNumber ?? "",
                SstRegistration = supplier.SstRegistrationNumber,
                Address = supplier.Address,
                City = supplier.City,
                State = supplier.State,
                PostalCode = supplier.PostalCode,
                CountryCode = supplier.Country ?? "MYS",
            },
            Buyer = buyer,
            NetTotal = invoice.NetTotal,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            Lines = invoice.Items.Select(item => new EInvoiceLineData
            {
                Description = item.Description,
                Uom = item.Uom ?? "C62",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxAmount = item.TaxAmount,
            }).ToList(),
        };
    }

    private async Task UpdateSourceDocumentStatusAsync(string docType, Guid docId, EInvoiceSubmission submission)
    {
        if (docType == "SalesInvoice")
        {
            var invoice = await _salesInvoiceRepository.GetAsync(docId);
            invoice.EInvoiceStatus = submission.Status == "Accepted"
                ? Sales.EInvoiceStatus.Pending : Sales.EInvoiceStatus.Invalid;
            invoice.LhdnUuid = submission.DocumentUuid;
            invoice.LhdnLongId = submission.LongId;
            await _salesInvoiceRepository.UpdateAsync(invoice);
        }
    }

    public async Task<EInvoiceSubmissionDto> GetStatusAsync(Guid submissionId)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException("MyERP:EInvoice:00010");

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.RefreshStatusAsync(submissionId, accessToken, environment);
        return MapToDto(submission);
    }

    [Authorize(MyERPPermissions.EInvoice.Cancel)]
    public async Task<EInvoiceSubmissionDto> CancelAsync(CancelEInvoiceDto input)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException("MyERP:EInvoice:00010");

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.CancelAsync(
            input.SubmissionId, input.Reason, accessToken, environment);

        return MapToDto(submission);
    }

    public async Task<PagedResultDto<EInvoiceSubmissionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _submissionRepository.GetCountAsync();
        var submissions = await _submissionRepository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "CreationTime DESC");

        return new PagedResultDto<EInvoiceSubmissionDto>(
            totalCount,
            submissions.Select(MapToDto).ToList());
    }

    private static EInvoiceSubmissionDto MapToDto(EInvoiceSubmission s) => new()
    {
        Id = s.Id,
        CompanyId = s.CompanyId,
        SubmissionUid = s.SubmissionUid,
        DocumentUuid = s.DocumentUuid,
        LongId = s.LongId,
        SourceDocumentType = s.SourceDocumentType,
        SourceDocumentId = s.SourceDocumentId,
        DocumentTypeCode = s.DocumentTypeCode,
        Status = s.Status,
        Reason = s.Reason,
        QrCodeUrl = s.QrCodeUrl,
        SubmittedAt = s.SubmittedAt,
        ValidatedAt = s.ValidatedAt,
        CancelledAt = s.CancelledAt
    };
}
