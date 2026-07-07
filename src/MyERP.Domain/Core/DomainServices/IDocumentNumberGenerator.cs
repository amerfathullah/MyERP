using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Generates the next document number for a given document type and company.
/// </summary>
public interface IDocumentNumberGenerator : ITransientDependency
{
    /// <summary>
    /// Gets the next number in the series for the specified document type.
    /// Must be called inside a unit of work.
    /// </summary>
    Task<string> GenerateAsync(string documentType, System.Guid companyId);
}
