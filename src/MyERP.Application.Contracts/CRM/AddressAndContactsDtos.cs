using System;
using System.Collections.Generic;

namespace MyERP.CRM;

public class GetAddressAndContactsInput
{
    public Guid CompanyId { get; set; }

    /// <summary>Customer, Supplier, SalesPartner, Lead</summary>
    public string PartyType { get; set; } = "Customer";

    public Guid? PartyId { get; set; }
    public string? Search { get; set; }
}

public class AddressAndContactRowDto
{
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string? PartyCode { get; set; }
    public string? PartyGroup { get; set; }

    // Address
    public Guid? AddressId { get; set; }
    public string? AddressTitle { get; set; }
    public string? AddressType { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public bool IsPrimaryAddress { get; set; }

    // Contact
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? MobileNo { get; set; }
    public bool IsPrimaryContact { get; set; }
}

public class AddressAndContactsReportDto
{
    public string PartyType { get; set; } = "Customer";
    public List<AddressAndContactRowDto> Rows { get; set; } = new();
    public int TotalParties { get; set; }
    public int TotalAddresses { get; set; }
    public int TotalContacts { get; set; }
}
