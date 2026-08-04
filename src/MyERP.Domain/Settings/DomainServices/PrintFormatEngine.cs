using System;
using System.Threading.Tasks;
using MyERP.Settings.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using MyERP.Core;

namespace MyERP.Settings.DomainServices;

/// <summary>
/// Domain service for evaluating Print Formats.
/// Real rendering is pushed to Infrastructure (e.g. DinkToPdf or RazorLight).
/// This service provides the template configuration and validates it.
/// </summary>
public class PrintFormatEngine : DomainService
{
    private readonly IRepository<PrintFormat, Guid> _printFormatRepository;

    public PrintFormatEngine(IRepository<PrintFormat, Guid> printFormatRepository)
    {
        _printFormatRepository = printFormatRepository;
    }

    /// <summary>
    /// Gets the applicable Print Format template for a given document type.
    /// If formatName is provided, it attempts to find that specific format.
    /// Otherwise, it finds the default format for the document type.
    /// </summary>
    public async Task<PrintFormat> GetFormatAsync(string documentType, string? formatName = null)
    {
        if (!string.IsNullOrWhiteSpace(formatName))
        {
            var format = await _printFormatRepository.FirstOrDefaultAsync(x => x.DocumentType == documentType && x.Name == formatName);
            if (format != null)
                return format;
            
            // Fallback to default if named format not found
        }

        var defaultFormat = await _printFormatRepository.FirstOrDefaultAsync(x => x.DocumentType == documentType && x.IsDefault);
        
        if (defaultFormat == null)
            throw new BusinessException(MyERPDomainErrorCodes.PrintFormatNotFound)
                .WithData("DocumentType", documentType);

        return defaultFormat;
    }

    /// <summary>
    /// Placeholder for the rendering logic.
    /// In a real application, an Application Service would use this Domain Service
    /// to get the template, fetch the aggregate root (e.g. SalesInvoice), and pass 
    /// both to an Infrastructure service (like IRazorViewEngine) to generate the PDF string/bytes.
    /// </summary>
    public string GenerateHtml(PrintFormat format, object model)
    {
        // Simple string replacement as a placeholder for full Razor rendering.
        // E.g., replace "{{ Title }}" with model's properties.
        return format.HtmlTemplate; // Mock
    }
}
