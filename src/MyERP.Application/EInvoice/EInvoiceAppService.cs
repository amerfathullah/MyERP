using System;
using System.Collections.Generic;
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

using MyERP;

namespace MyERP.EInvoice;

[Authorize(MyERPPermissions.EInvoice.Default)]
public class EInvoiceAppService : ApplicationService, IEInvoiceAppService
{
    private readonly EInvoiceService _eInvoiceService;
    private readonly InvoiceDocumentBuilder _documentBuilder;
    private readonly InvoiceDocumentSigner _documentSigner;
    private readonly EInvoiceValidationService _validationService;
    private readonly EInvoiceConsolidationService _consolidationService;
    private readonly IRepository<EInvoiceSubmission, Guid> _submissionRepository;
    private readonly IRepository<EInvoiceConsolidation, Guid> _consolidationRepository;
    private readonly IRepository<LhdnSuccessLog, Guid> _successLogRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly TaxpayerValidationService _taxpayerValidationService;
    private readonly ISettingProvider _settingProvider;

    public EInvoiceAppService(
        EInvoiceService eInvoiceService,
        InvoiceDocumentBuilder documentBuilder,
        InvoiceDocumentSigner documentSigner,
        EInvoiceValidationService validationService,
        EInvoiceConsolidationService consolidationService,
        TaxpayerValidationService taxpayerValidationService,
        IRepository<EInvoiceSubmission, Guid> submissionRepository,
        IRepository<EInvoiceConsolidation, Guid> consolidationRepository,
        IRepository<LhdnSuccessLog, Guid> successLogRepository,
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
        _consolidationService = consolidationService;
        _taxpayerValidationService = taxpayerValidationService;
        _submissionRepository = submissionRepository;
        _consolidationRepository = consolidationRepository;
        _successLogRepository = successLogRepository;
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
        var company = await _companyRepository.GetAsync(input.CompanyId);
        if (!company.EnableLhdnInvoice)
        {
            // Per MyInvois PR d9adf36: Gracefully bypass LHDN submission if disabled.
            // Return an empty/dummy DTO to prevent blocking ERP transactions.
            return new EInvoiceSubmissionDto { Status = "NotSubmitted" };
        }

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
            ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed)
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

        return ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>(submission);
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
            invoice.LhdnSubmissionId = submission.Id;
            invoice.LhdnSubmittedAt = DateTime.UtcNow;
            await _salesInvoiceRepository.UpdateAsync(invoice);
        }
    }

    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task<BatchSubmitResultDto> BatchSubmitAsync(BatchSubmitEInvoiceDto input)
    {
        if (input.DocumentIds == null || !input.DocumentIds.Any())
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems)
                .WithData("documentType", "BatchEInvoice");

        var result = new BatchSubmitResultDto { TotalRequested = input.DocumentIds.Count };

        foreach (var docId in input.DocumentIds)
        {
            try
            {
                // Check if already submitted
                string? docNumber = null;
                if (input.SourceDocumentType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.FindAsync(docId);
                    if (si == null) { result.SkippedCount++; continue; }
                    if (si.EInvoiceStatus != Sales.EInvoiceStatus.NotSubmitted)
                    {
                        result.Results.Add(new BatchSubmitItemResult
                        {
                            DocumentId = docId,
                            DocumentNumber = si.InvoiceNumber,
                            Success = false,
                            ErrorMessage = "Already submitted to LHDN",
                            Status = si.EInvoiceStatus.ToString()
                        });
                        result.SkippedCount++;
                        continue;
                    }
                    docNumber = si.InvoiceNumber;
                }
                else if (input.SourceDocumentType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.FindAsync(docId);
                    if (pi == null) { result.SkippedCount++; continue; }
                    if (pi.EInvoiceStatus != Sales.EInvoiceStatus.NotSubmitted)
                    {
                        result.Results.Add(new BatchSubmitItemResult
                        {
                            DocumentId = docId,
                            DocumentNumber = pi.InvoiceNumber,
                            Success = false,
                            ErrorMessage = "Already submitted to LHDN",
                            Status = pi.EInvoiceStatus.ToString()
                        });
                        result.SkippedCount++;
                        continue;
                    }
                    docNumber = pi.InvoiceNumber;
                }

                var submission = await SubmitAsync(new SubmitEInvoiceDto
                {
                    CompanyId = input.CompanyId,
                    SourceDocumentType = input.SourceDocumentType,
                    SourceDocumentId = docId,
                });

                result.Results.Add(new BatchSubmitItemResult
                {
                    DocumentId = docId,
                    DocumentNumber = docNumber ?? docId.ToString()[..8],
                    Success = true,
                    LhdnUuid = submission.DocumentUuid,
                    Status = submission.Status
                });
                result.SucceededCount++;
            }
            catch (Exception ex)
            {
                result.Results.Add(new BatchSubmitItemResult
                {
                    DocumentId = docId,
                    DocumentNumber = docId.ToString()[..8],
                    Success = false,
                    ErrorMessage = ex.Message
                });
                result.FailedCount++;
            }
        }

        return result;
    }

    public async Task<EInvoiceSubmissionDto> GetStatusAsync(Guid submissionId)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed);

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.RefreshStatusAsync(submissionId, accessToken, environment);

        // Propagate LHDN validation status to source document
        // Per myinvois get_status.py: updates source invoice EInvoiceStatus field
        await PropagateStatusToSourceAsync(submission);

        return ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>(submission);
    }

    /// <summary>
    /// Propagates LHDN validation status back to the source SI/PI.
    /// Per myinvois: "Valid" → EInvoiceStatus.Valid, includes QR code URL.
    /// Per myinvois: "Cancelled" → EInvoiceStatus.Cancelled.
    /// </summary>
    private async Task PropagateStatusToSourceAsync(EInvoiceSubmission submission)
    {
        if (submission.SourceDocumentType == "SalesInvoice")
        {
            var invoice = await _salesInvoiceRepository.FindAsync(submission.SourceDocumentId);
            if (invoice == null) return;

            invoice.EInvoiceStatus = submission.Status switch
            {
                "Valid" => Sales.EInvoiceStatus.Valid,
                "Cancelled" => Sales.EInvoiceStatus.Cancelled,
                "Invalid" or "Rejected" => Sales.EInvoiceStatus.Invalid,
                _ => invoice.EInvoiceStatus // preserve existing
            };
            invoice.LhdnUuid = submission.DocumentUuid;
            invoice.LhdnLongId = submission.LongId;
            invoice.QrCodeUrl = submission.QrCodeUrl;
            await _salesInvoiceRepository.UpdateAsync(invoice);
        }
        else if (submission.SourceDocumentType == "PurchaseInvoice")
        {
            var invoice = await _purchaseInvoiceRepository.FindAsync(submission.SourceDocumentId);
            if (invoice == null) return;

            invoice.EInvoiceStatus = submission.Status switch
            {
                "Valid" => Sales.EInvoiceStatus.Valid,
                "Cancelled" => Sales.EInvoiceStatus.Cancelled,
                "Invalid" or "Rejected" => Sales.EInvoiceStatus.Invalid,
                _ => invoice.EInvoiceStatus
            };
            invoice.LhdnUuid = submission.DocumentUuid;
            await _purchaseInvoiceRepository.UpdateAsync(invoice);
        }
    }

    [Authorize(MyERPPermissions.EInvoice.Cancel)]
    public async Task<EInvoiceSubmissionDto> CancelAsync(CancelEInvoiceDto input)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed);

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.CancelAsync(
            input.SubmissionId, input.Reason, accessToken, environment);

        return ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>(submission);
    }

    public async Task<PagedResultDto<EInvoiceSubmissionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _submissionRepository.GetCountAsync();
        var submissions = await _submissionRepository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "CreationTime DESC");

        return new PagedResultDto<EInvoiceSubmissionDto>(
            totalCount,
            submissions.Select(ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>).ToList());
    }

    /// <summary>
    /// Submit a consolidated POS invoice to LHDN.
    /// Per myinvois consolidate_invoice.py:
    /// - Uses generic buyer TIN "EI00000000020" for walk-in customers
    /// - Document type = "01" (standard invoice, not self-billed)
    /// - Aggregates line items from the consolidated SI
    /// Per DO-NOT: "Create return for consolidated POS invoices (blocked entirely)"
    /// </summary>
    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task<EInvoiceSubmissionDto> SubmitConsolidatedAsync(SubmitEInvoiceDto input)
    {
        var invoice = await _salesInvoiceRepository.GetAsync(input.SourceDocumentId, includeDetails: true);

        // Validate this is actually a consolidated invoice (has ConsolidatedSalesInvoiceId set on children)
        if (invoice.GrandTotal <= 0)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed)
                .WithData("reason", "Consolidated invoice has no value to submit.");

        // Build document with generic buyer for walk-in POS customers
        var company = await _companyRepository.GetAsync(input.CompanyId);
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

        // Per myinvois: consolidated invoices use generic buyer "General Public"
        var buyer = new EInvoicePartyData
        {
            Name = "Consolidated - General Public",
            Tin = "EI00000000020", // LHDN generic TIN for walk-in customers
            IdType = "BRN",
            IdValue = "EI00000000020",
            CountryCode = "MYS",
        };

        var documentData = new EInvoiceDocumentData
        {
            InvoiceNumber = invoice.InvoiceNumber,
            IssueDate = invoice.IssueDate,
            DocumentTypeCode = "01", // Standard invoice
            CurrencyCode = invoice.CurrencyCode,
            Supplier = supplier,
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

        var xmlDocument = _documentBuilder.Build(documentData);

        // Sign if certificate configured
        var pfxBase64 = await _settingProvider.GetOrNullAsync("EInvoice.CertificatePfxBase64");
        var pfxPassword = await _settingProvider.GetOrNullAsync("EInvoice.CertificatePassword");
        if (!string.IsNullOrEmpty(pfxBase64) && !string.IsNullOrEmpty(pfxPassword))
        {
            var pfxBytes = Convert.FromBase64String(pfxBase64);
            xmlDocument = _documentSigner.Sign(xmlDocument, pfxBytes, pfxPassword);
        }

        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed)
                .WithData("reason", "LHDN access token not configured.");

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.SubmitAsync(
            input.CompanyId, "SalesInvoice", input.SourceDocumentId,
            xmlDocument, accessToken, environment, CurrentTenant.Id);

        await UpdateSourceDocumentStatusAsync("SalesInvoice", input.SourceDocumentId, submission);

        return ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>(submission);
    }

    [Authorize(MyERPPermissions.EInvoice.Submit)]
    public async Task<List<Guid>> ConsolidateInvoicesAsync(ConsolidateInvoicesDto input)
    {
        return await _consolidationService.ConsolidateInvoicesAsync(input.InvoiceIds, input.CompanyId, CurrentTenant.Id);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<TaxpayerSearchResultDto> SearchTaxpayerAsync(SearchTaxpayerDto input)
    {
        try
        {
            var response = await _taxpayerValidationService.ValidateTaxpayerAsync(input.IdType, input.IdValue);
            return new TaxpayerSearchResultDto
            {
                IsSuccess = response.IsFound,
                Tin = response.Tin,
                Name = response.TaxpayerName,
                IdType = input.IdType,
                IdValue = input.IdValue,
                ErrorMessage = response.IsFound ? null : "Taxpayer not found"
            };
        }
        catch (Exception ex)
        {
            return new TaxpayerSearchResultDto
            {
                IsSuccess = false,
                IdType = input.IdType,
                IdValue = input.IdValue,
                ErrorMessage = ex.Message
            };
        }
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<List<LhdnStatusReportItemDto>> GetSalesStatusReportAsync(LhdnStatusReportRequestDto input)
    {
        var salesQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var customerQuery = await _customerRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }
        if (input.FromDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate <= input.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            if (input.Status.Equals("Not Submitted", StringComparison.OrdinalIgnoreCase) ||
                input.Status.Equals("NotSubmitted", StringComparison.OrdinalIgnoreCase))
            {
                salesQuery = salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.NotSubmitted);
            }
            else if (Enum.TryParse<Sales.EInvoiceStatus>(input.Status, true, out var parsedStatus))
            {
                salesQuery = salesQuery.Where(x => x.EInvoiceStatus == parsedStatus);
            }
        }

        var query = from si in salesQuery
                    join c in customerQuery on si.CustomerId equals c.Id into custJoin
                    from c in custJoin.DefaultIfEmpty()
                    orderby si.IssueDate descending, si.InvoiceNumber descending
                    select new LhdnStatusReportItemDto
                    {
                        InvoiceId = si.Id,
                        InvoiceNumber = si.InvoiceNumber,
                        PostingDate = si.IssueDate,
                        PartyName = c != null ? c.Name : "—",
                        GrandTotal = si.GrandTotal,
                        TaxAmount = si.TaxAmount,
                        Status = si.EInvoiceStatus.ToString(),
                        DocumentUuid = si.LhdnUuid,
                        QrCodeUrl = si.QrCodeUrl,
                        SubmittedAt = si.LhdnSubmittedAt
                    };

        return await AsyncExecuter.ToListAsync(query);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<List<LhdnStatusReportItemDto>> GetPurchaseStatusReportAsync(LhdnStatusReportRequestDto input)
    {
        var purchaseQuery = await _purchaseInvoiceRepository.GetQueryableAsync();
        var supplierQuery = await _supplierRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }
        if (input.FromDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(x => x.IssueDate >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(x => x.IssueDate <= input.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            if (input.Status.Equals("Not Submitted", StringComparison.OrdinalIgnoreCase) ||
                input.Status.Equals("NotSubmitted", StringComparison.OrdinalIgnoreCase))
            {
                purchaseQuery = purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.NotSubmitted);
            }
            else if (Enum.TryParse<Sales.EInvoiceStatus>(input.Status, true, out var parsedStatus))
            {
                purchaseQuery = purchaseQuery.Where(x => x.EInvoiceStatus == parsedStatus);
            }
        }

        var query = from pi in purchaseQuery
                    join s in supplierQuery on pi.SupplierId equals s.Id into suppJoin
                    from s in suppJoin.DefaultIfEmpty()
                    orderby pi.IssueDate descending, pi.InvoiceNumber descending
                    select new LhdnStatusReportItemDto
                    {
                        InvoiceId = pi.Id,
                        InvoiceNumber = pi.InvoiceNumber,
                        PostingDate = pi.IssueDate,
                        PartyName = s != null ? s.Name : "—",
                        GrandTotal = pi.GrandTotal,
                        TaxAmount = pi.TaxAmount,
                        Status = pi.EInvoiceStatus.ToString(),
                        DocumentUuid = pi.LhdnUuid,
                        QrCodeUrl = null,
                        SubmittedAt = pi.LhdnSubmittedAt
                    };

        return await AsyncExecuter.ToListAsync(query);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<LhdnVatReportDto> GetVatReportAsync(LhdnVatReportRequestDto input)
    {
        var taxCategories = new Dictionary<string, string>
        {
            { "01", "Sales Tax" },
            { "02", "Service Tax" },
            { "03", "Tourism Tax" },
            { "04", "High-Value Goods Tax" },
            { "05", "Sales Tax on Low Value Goods" },
            { "06", "Not Applicable" },
            { "E", "Tax Exemption" }
        };

        var salesQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var purchaseQuery = await _purchaseInvoiceRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.CompanyId == input.CompanyId.Value);
            purchaseQuery = purchaseQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }
        if (input.FromDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate >= input.FromDate.Value);
            purchaseQuery = purchaseQuery.Where(x => x.IssueDate >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate <= input.ToDate.Value);
            purchaseQuery = purchaseQuery.Where(x => x.IssueDate <= input.ToDate.Value);
        }

        salesQuery = salesQuery.Where(x => x.Status == Core.DocumentStatus.Submitted);
        purchaseQuery = purchaseQuery.Where(x => x.Status == Core.DocumentStatus.Submitted);

        var salesInvoices = await AsyncExecuter.ToListAsync(salesQuery);
        var purchaseInvoices = await AsyncExecuter.ToListAsync(purchaseQuery);

        var salesCatMap = taxCategories.ToDictionary(k => k.Key, v => new LhdnVatCategorySummaryDto
        {
            CategoryCode = v.Key,
            CategoryName = v.Value
        });

        foreach (var si in salesInvoices)
        {
            var catCode = si.TaxAmount > 0 ? "01" : "E";
            if (!salesCatMap.ContainsKey(catCode)) catCode = "E";

            if (si.IsReturn)
            {
                salesCatMap[catCode].Adjustment += si.GrandTotal;
                salesCatMap[catCode].VatAmount -= si.TaxAmount;
            }
            else
            {
                salesCatMap[catCode].Amount += si.GrandTotal;
                salesCatMap[catCode].VatAmount += si.TaxAmount;
            }
        }

        var purchaseCatMap = taxCategories.ToDictionary(k => k.Key, v => new LhdnVatCategorySummaryDto
        {
            CategoryCode = v.Key,
            CategoryName = v.Value
        });

        foreach (var pi in purchaseInvoices)
        {
            var catCode = pi.TaxAmount > 0 ? "02" : "E";
            if (!purchaseCatMap.ContainsKey(catCode)) catCode = "E";

            if (pi.IsReturn)
            {
                purchaseCatMap[catCode].Adjustment += pi.GrandTotal;
                purchaseCatMap[catCode].VatAmount -= pi.TaxAmount;
            }
            else
            {
                purchaseCatMap[catCode].Amount += pi.GrandTotal;
                purchaseCatMap[catCode].VatAmount += pi.TaxAmount;
            }
        }

        var report = new LhdnVatReportDto
        {
            SalesCategories = salesCatMap.Values.ToList(),
            PurchaseCategories = purchaseCatMap.Values.ToList(),
            TotalSalesAmount = salesCatMap.Values.Sum(x => x.Amount),
            TotalSalesAdjustment = salesCatMap.Values.Sum(x => x.Adjustment),
            TotalSalesVat = salesCatMap.Values.Sum(x => x.VatAmount),
            TotalPurchaseAmount = purchaseCatMap.Values.Sum(x => x.Amount),
            TotalPurchaseAdjustment = purchaseCatMap.Values.Sum(x => x.Adjustment),
            TotalPurchaseVat = purchaseCatMap.Values.Sum(x => x.VatAmount)
        };
        report.NetVatPayable = report.TotalSalesVat - report.TotalPurchaseVat;

        return report;
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<LhdnDashboardStatsDto> GetDashboardStatsAsync(Guid? companyId)
    {
        var salesQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var purchaseQuery = await _purchaseInvoiceRepository.GetQueryableAsync();

        if (companyId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.CompanyId == companyId.Value);
            purchaseQuery = purchaseQuery.Where(x => x.CompanyId == companyId.Value);
        }

        var stats = new LhdnDashboardStatsDto
        {
            SalesValid = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Valid)),
            SalesInvalid = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Invalid)),
            SalesSubmitted = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Pending)),
            SalesCancelled = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Cancelled)),
            SalesFailed = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Rejected)),
            SalesNotSubmitted = await AsyncExecuter.CountAsync(salesQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.NotSubmitted)),

            PurchaseValid = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Valid)),
            PurchaseInvalid = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Invalid)),
            PurchaseSubmitted = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Pending)),
            PurchaseCancelled = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Cancelled)),
            PurchaseFailed = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.Rejected)),
            PurchaseNotSubmitted = await AsyncExecuter.CountAsync(purchaseQuery.Where(x => x.EInvoiceStatus == Sales.EInvoiceStatus.NotSubmitted))
        };

        return stats;
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<List<ConsolidationCandidateDto>> GetConsolidationCandidatesAsync(GetConsolidationCandidatesInputDto input)
    {
        var salesQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var custQuery = await _customerRepository.GetQueryableAsync();
        var consolQuery = await _consolidationRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }
        if (input.FromDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.IssueDate <= input.ToDate.Value);
        }

        // Only submitted sales invoices not submitted to LHDN directly
        salesQuery = salesQuery.Where(x => x.Status == Core.DocumentStatus.Submitted &&
                                           x.EInvoiceStatus == Sales.EInvoiceStatus.NotSubmitted);

        var alreadyConsolidatedIds = consolQuery.Select(c => c.OriginalInvoiceId);
        salesQuery = salesQuery.Where(x => !alreadyConsolidatedIds.Contains(x.Id));

        var maxAmount = input.MaxAmount ?? 10000m;
        salesQuery = salesQuery.Where(x => x.GrandTotal <= maxAmount);

        var query = from si in salesQuery
                    join c in custQuery on si.CustomerId equals c.Id into custJoin
                    from c in custJoin.DefaultIfEmpty()
                    orderby si.IssueDate descending, si.InvoiceNumber descending
                    select new ConsolidationCandidateDto
                    {
                        InvoiceId = si.Id,
                        InvoiceNumber = si.InvoiceNumber,
                        IssueDate = si.IssueDate,
                        CustomerId = si.CustomerId,
                        CustomerName = c != null ? c.Name : "General Public",
                        GrandTotal = si.GrandTotal,
                        ItemCount = si.Items.Count,
                        CurrencyCode = si.CurrencyCode ?? "MYR",
                        IsEligible = true
                    };

        return await AsyncExecuter.ToListAsync(query);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<PagedResultDto<EInvoiceConsolidationDto>> GetConsolidationsAsync(GetConsolidationsInputDto input)
    {
        var consolQuery = await _consolidationRepository.GetQueryableAsync();
        var salesQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var custQuery = await _customerRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            consolQuery = consolQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(
            consolQuery.Select(x => x.ConsolidatedInvoiceId).Distinct()
        );

        var consolidatedInvoiceIds = await AsyncExecuter.ToListAsync(
            consolQuery
                .OrderByDescending(x => x.CreationTime)
                .Select(x => x.ConsolidatedInvoiceId)
                .Distinct()
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
        );

        var consolidatedInvoices = await AsyncExecuter.ToListAsync(
            salesQuery.Where(x => consolidatedInvoiceIds.Contains(x.Id))
        );

        var consolRecords = await AsyncExecuter.ToListAsync(
            consolQuery.Where(x => consolidatedInvoiceIds.Contains(x.ConsolidatedInvoiceId))
        );

        var originalInvoiceIds = consolRecords.Select(x => x.OriginalInvoiceId).Distinct().ToList();
        var originalInvoices = await AsyncExecuter.ToListAsync(
            salesQuery.Where(x => originalInvoiceIds.Contains(x.Id))
        );
        var originalCustomerIds = originalInvoices.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await AsyncExecuter.ToListAsync(
            custQuery.Where(x => originalCustomerIds.Contains(x.Id))
        );
        var custMap = customers.ToDictionary(k => k.Id, v => v.Name);

        var origMap = originalInvoices.ToDictionary(k => k.Id, v => new ConsolidationCandidateDto
        {
            InvoiceId = v.Id,
            InvoiceNumber = v.InvoiceNumber,
            IssueDate = v.IssueDate,
            CustomerId = v.CustomerId,
            CustomerName = custMap.TryGetValue(v.CustomerId, out var cName) ? cName : "General Public",
            GrandTotal = v.GrandTotal,
            ItemCount = v.Items.Count,
            CurrencyCode = v.CurrencyCode ?? "MYR",
            IsEligible = true
        });

        var resultList = new List<EInvoiceConsolidationDto>();
        foreach (var cInv in consolidatedInvoices)
        {
            var matchedConsols = consolRecords.Where(x => x.ConsolidatedInvoiceId == cInv.Id).ToList();
            var origList = matchedConsols
                .Where(m => origMap.ContainsKey(m.OriginalInvoiceId))
                .Select(m => origMap[m.OriginalInvoiceId])
                .ToList();

            resultList.Add(new EInvoiceConsolidationDto
            {
                Id = matchedConsols.FirstOrDefault()?.Id ?? cInv.Id,
                CompanyId = cInv.CompanyId,
                ConsolidatedInvoiceId = cInv.Id,
                ConsolidatedInvoiceNumber = cInv.InvoiceNumber,
                ConsolidatedIssueDate = cInv.IssueDate,
                ConsolidatedGrandTotal = cInv.GrandTotal,
                LhdnUuid = cInv.LhdnUuid,
                EInvoiceStatus = cInv.EInvoiceStatus.ToString(),
                QrCodeUrl = cInv.QrCodeUrl,
                OriginalInvoices = origList,
                CreationTime = matchedConsols.FirstOrDefault()?.CreationTime ?? cInv.CreationTime
            });
        }

        return new PagedResultDto<EInvoiceConsolidationDto>(totalCount, resultList);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<PagedResultDto<LhdnSuccessLogDto>> GetSuccessLogsAsync(GetLhdnSuccessLogsInputDto input)
    {
        var logQuery = await _successLogRepository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            logQuery = logQuery.Where(x => x.CompanyId == input.CompanyId.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.SourceDocumentType))
        {
            logQuery = logQuery.Where(x => x.SourceDocumentType == input.SourceDocumentType);
        }
        if (input.FromDate.HasValue)
        {
            logQuery = logQuery.Where(x => x.SubmittedAt >= input.FromDate.Value);
        }
        if (input.ToDate.HasValue)
        {
            logQuery = logQuery.Where(x => x.SubmittedAt <= input.ToDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.SearchFilter))
        {
            var filter = input.SearchFilter.Trim().ToLower();
            logQuery = logQuery.Where(x => x.DocumentUuid.ToLower().Contains(filter) ||
                                           (x.SourceDocumentNumber != null && x.SourceDocumentNumber.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(logQuery);

        var logs = await AsyncExecuter.ToListAsync(
            logQuery.OrderByDescending(x => x.SubmittedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
        );

        var dtoList = logs.Select(x => new LhdnSuccessLogDto
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            SubmissionId = x.SubmissionId,
            DocumentUuid = x.DocumentUuid,
            LongId = x.LongId,
            SourceDocumentType = x.SourceDocumentType,
            SourceDocumentId = x.SourceDocumentId,
            SourceDocumentNumber = x.SourceDocumentNumber,
            DocumentTypeCode = x.DocumentTypeCode,
            SubmittedAt = x.SubmittedAt,
            ValidatedAt = x.ValidatedAt,
            ResponseJson = x.ResponseJson,
            QrCodeUrl = x.QrCodeUrl,
            GrandTotal = x.GrandTotal,
            CurrencyCode = x.CurrencyCode
        }).ToList();

        return new PagedResultDto<LhdnSuccessLogDto>(totalCount, dtoList);
    }

    [Authorize(MyERPPermissions.EInvoice.Default)]
    public async Task<EInvoiceSubmissionDto> RefreshStatusAsync(Guid submissionId)
    {
        var accessToken = await _settingProvider.GetOrNullAsync("EInvoice.AccessToken")
            ?? throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.EInvoiceValidationFailed)
                .WithData("reason", "LHDN access token not configured.");

        var envString = await _settingProvider.GetOrNullAsync("EInvoice.Environment") ?? "Sandbox";
        var environment = Enum.Parse<LhdnEnvironment>(envString);

        var submission = await _eInvoiceService.RefreshStatusAsync(submissionId, accessToken, environment);
        await PropagateStatusToSourceAsync(submission);

        return ObjectMapper.Map<EInvoiceSubmission, EInvoiceSubmissionDto>(submission);
    }
}

