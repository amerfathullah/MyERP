using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml.Linq;
using Volo.Abp.DependencyInjection;

namespace MyERP.EInvoice.Services;

/// <summary>
/// Signs UBL 2.1 invoice XML with XAdES digital signature for LHDN MyInvois API v1.1.
/// Migrated from myinvois original.py: certificate_data(), sign_data(), signed_properties_hash(),
/// ubl_extension_string(), xml_hash(), apply_signature_flow().
/// </summary>
public class InvoiceDocumentSigner : ITransientDependency
{
    /// <summary>
    /// Signs an invoice XML document using the provided PFX certificate.
    /// Returns the XML with UBLExtension containing ds:Signature (XAdES-BES).
    /// </summary>
    public string Sign(string invoiceXml, byte[] pfxBytes, string pfxPassword)
    {
        using var cert = new X509Certificate2(pfxBytes, pfxPassword, X509KeyStorageFlags.Exportable);
        var privateKey = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("PFX does not contain an RSA private key.");

        // Step 1: Compute document hash (SHA-256 of canonical XML)
        var documentHash = ComputeXmlHash(invoiceXml);

        // Step 2: Build SignedProperties and compute its hash
        var signedPropertiesXml = BuildSignedProperties(cert);
        var signedPropertiesHash = ComputeSha256(signedPropertiesXml);

        // Step 3: Sign the document hash with RSA-SHA256
        var signatureValue = SignData(privateKey, documentHash);

        // Step 4: Build UBLExtension with full ds:Signature block
        var signatureExtension = BuildUblExtension(
            documentHash, signedPropertiesHash, signatureValue, cert, signedPropertiesXml);

        // Step 5: Inject UBLExtension into the invoice XML
        return InjectSignature(invoiceXml, signatureExtension);
    }

    /// <summary>
    /// Compute SHA-256 hash of the invoice XML (canonical form).
    /// </summary>
    public byte[] ComputeXmlHash(string xml)
    {
        // Normalize whitespace for canonical form
        var canonicalXml = xml.Replace("\r\n", "\n").Trim();
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonicalXml));
    }

    /// <summary>
    /// RSA-SHA256 sign the given data.
    /// </summary>
    private static byte[] SignData(RSA privateKey, byte[] data)
    {
        return privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// Build XAdES SignedProperties XML element with signing time, cert digest, issuer info.
    /// </summary>
    private static string BuildSignedProperties(X509Certificate2 cert)
    {
        var signingTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var certDigest = Convert.ToBase64String(SHA256.HashData(cert.RawData));
        var issuerName = cert.IssuerName.Name;
        var serialNumber = cert.SerialNumber;

        return $@"<xades:SignedProperties Id=""id-xades-signed-props"">
  <xades:SignedSignatureProperties>
    <xades:SigningTime>{signingTime}</xades:SigningTime>
    <xades:SigningCertificate>
      <xades:Cert>
        <xades:CertDigest>
          <ds:DigestMethod Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256""/>
          <ds:DigestValue>{certDigest}</ds:DigestValue>
        </xades:CertDigest>
        <xades:IssuerSerial>
          <ds:X509IssuerName>{issuerName}</ds:X509IssuerName>
          <ds:X509SerialNumber>{serialNumber}</ds:X509SerialNumber>
        </xades:IssuerSerial>
      </xades:Cert>
    </xades:SigningCertificate>
  </xades:SignedSignatureProperties>
</xades:SignedProperties>";
    }

    /// <summary>
    /// Build full UBL Extension containing ds:Signature with XAdES SignedProperties.
    /// </summary>
    private static string BuildUblExtension(
        byte[] documentHash, byte[] signedPropertiesHash, byte[] signatureValue,
        X509Certificate2 cert, string signedPropertiesXml)
    {
        var docHashB64 = Convert.ToBase64String(documentHash);
        var propHashB64 = Convert.ToBase64String(signedPropertiesHash);
        var sigValueB64 = Convert.ToBase64String(signatureValue);
        var certB64 = Convert.ToBase64String(cert.RawData);

        return $@"<ext:UBLExtension>
  <ext:ExtensionContent>
    <sig:UBLDocumentSignatures>
      <sac:SignatureInformation>
        <ds:Signature Id=""signature"">
          <ds:SignedInfo>
            <ds:CanonicalizationMethod Algorithm=""http://www.w3.org/2006/12/xml-c14n11""/>
            <ds:SignatureMethod Algorithm=""http://www.w3.org/2001/04/xmldsig-more#rsa-sha256""/>
            <ds:Reference Id=""id-doc-signed-data"" URI="""">
              <ds:DigestMethod Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256""/>
              <ds:DigestValue>{docHashB64}</ds:DigestValue>
            </ds:Reference>
            <ds:Reference Type=""http://www.w3.org/2000/09/xmldsig#SignatureProperties"" URI=""#id-xades-signed-props"">
              <ds:DigestMethod Algorithm=""http://www.w3.org/2001/04/xmlenc#sha256""/>
              <ds:DigestValue>{propHashB64}</ds:DigestValue>
            </ds:Reference>
          </ds:SignedInfo>
          <ds:SignatureValue>{sigValueB64}</ds:SignatureValue>
          <ds:KeyInfo>
            <ds:X509Data>
              <ds:X509Certificate>{certB64}</ds:X509Certificate>
            </ds:X509Data>
          </ds:KeyInfo>
          <ds:Object>
            <xades:QualifyingProperties Target=""#signature"">
              {signedPropertiesXml}
            </xades:QualifyingProperties>
          </ds:Object>
        </ds:Signature>
      </sac:SignatureInformation>
    </sig:UBLDocumentSignatures>
  </ext:ExtensionContent>
</ext:UBLExtension>";
    }

    /// <summary>
    /// Inject the signature extension into the invoice XML (before the main content).
    /// </summary>
    private static string InjectSignature(string invoiceXml, string signatureExtension)
    {
        // Insert UBLExtensions block after the opening Invoice tag
        var extensionsBlock = $@"<ext:UBLExtensions xmlns:ext=""urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"" xmlns:sig=""urn:oasis:names:specification:ubl:schema:xsd:CommonSignatureComponents-2"" xmlns:sac=""urn:oasis:names:specification:ubl:schema:xsd:SignatureAggregateComponents-2"" xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"" xmlns:xades=""http://uri.etsi.org/01903/v1.3.2#"">
  {signatureExtension}
</ext:UBLExtensions>";

        // Find insertion point — after the first <Invoice ...> opening tag
        var insertIdx = invoiceXml.IndexOf('>') + 1;
        return invoiceXml.Insert(insertIdx, extensionsBlock);
    }

    private static byte[] ComputeSha256(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
}
