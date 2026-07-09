# MyERP — Malaysia Compliance Guide

## Overview

MyERP is built with Malaysia regulatory compliance as a first-class requirement. This document covers all compliance features.

---

## LHDN e-Invoice (MyInvois) Integration

### Architecture

```
MyERP API → EInvoiceService → InvoiceDocumentBuilder (UBL 2.1 XML)
                            → InvoiceDocumentSigner (XAdES, RSA-SHA256)
                            → LhdnApiClient (HTTPS to LHDN)
                            → EInvoiceSubmission (status tracking)
```

### Supported Document Types

| Code | Type | Description |
|------|------|-------------|
| 01 | Invoice | Standard sales invoice |
| 02 | Credit Note | Correction reducing amount |
| 03 | Debit Note | Correction increasing amount |
| 04 | Refund Note | Full/partial refund |
| 11 | Self-billed Invoice | Buyer issues on behalf of supplier |

### Submission Flow

1. **Validate** — Pre-submission checks (TIN format, MSIC code, BRN, required fields, item details)
2. **Build XML** — Generate UBL 2.1 compliant invoice document
3. **Sign** — XAdES digital signature with PFX certificate (RSA-SHA256)
4. **Submit** — POST to LHDN MyInvois API endpoint
5. **Track** — Store UUID, Long ID, validation status, QR code URL
6. **Refresh** — Poll/callback for final validation status

### Cancellation

- Invoices can be cancelled within **72 hours** of validation
- Cancellation requires a reason
- System enforces the 72-hour window at domain level

### Configuration

```json
{
  "LhdnApi": {
    "BaseUrl": "https://myinvois.hasil.gov.my",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "CertificatePath": "/certs/myerp-signing.pfx",
    "CertificatePassword": "FROM_ENVIRONMENT_VARIABLE",
    "Environment": "Production"
  }
}
```

---

## SST (Sales & Service Tax)

### Tax Engine Design

Tax rates are **never hardcoded**. The `TaxEngine` domain service evaluates applicable rules at runtime:

```
TaxContext (category, date, region, item group)
    → TaxRuleRepository.GetApplicableRules()
    → Aggregate rules by priority
    → Return TaxCalculationResult
```

### Configurable Tax Rules

| Field | Purpose |
|-------|---------|
| TaxCategoryId | Links to category (SST-Sales, SST-Service, Exempt) |
| Rate | Percentage (6%, 8%, 10%) |
| EffectiveFrom/To | Date validity window |
| ItemGroupFilter | Apply only to specific item groups |
| RegionFilter | Apply by state/region |

### Current SST Rates (Data-Driven)

These are seeded as initial data — administrators can update:
- Sales Tax: 5%, 10% (depending on goods category)
- Service Tax: 8% (effective March 2024)
- Exempt: 0% (for exempted goods/services)

---

## Payroll Compliance (EPF, SOCSO, EIS, PCB)

### Contribution Rules Engine

All statutory contribution rates are stored as `ContributionRule` entities — **never hardcoded**:

| Contribution | Employee Rate | Employer Rate | Notes |
|-------------|---------------|---------------|-------|
| EPF | 11% (< 60 yrs) | 12-13% | Age-based rates |
| SOCSO | 0.5% | 1.75% | Salary ceiling applies |
| EIS | 0.2% | 0.2% | Salary ceiling applies |
| PCB/MTD | Graduated schedule | — | Monthly tax deduction |

### PayrollEngine

```
PayrollEngine.CalculateAsync(employee, month, year)
    → Fetch applicable ContributionRules by date + age + citizenship
    → Calculate EPF (check age bracket)
    → Calculate SOCSO (check salary ceiling)
    → Calculate EIS (check salary ceiling)
    → Calculate PCB (graduated schedule)
    → Return PayrollCalculation result
```

---

## PDPA (Personal Data Protection Act 2010)

### Protected Fields

| Entity | Sensitive Fields | Protection |
|--------|-----------------|------------|
| Employee | IC Number, Bank Account, Salary | Permission-gated |
| Customer | IC/Passport Number | Permission-gated |
| Payment | Bank Details | Encrypted at rest |

### Implementation

- Field-level permission checks in AppService layer
- Audit logging tracks who accessed sensitive data
- Data Subject Access Request (DSAR) support planned
- Soft-delete ensures data can be "forgotten" while maintaining financial integrity

---

## Malaysian Chart of Accounts

MyERP provides a seeded Malaysian Chart of Accounts template following standard Malaysian accounting practices:

- **Assets** (1xxx): Current assets, fixed assets, investments
- **Liabilities** (2xxx): Current liabilities, long-term liabilities
- **Equity** (3xxx): Share capital, retained earnings
- **Revenue** (4xxx): Sales revenue, other income
- **Expenses** (5xxx–9xxx): Cost of goods, operating expenses, tax expenses

---

## Regulatory Reporting

| Report | Regulation | Status |
|--------|-----------|--------|
| SST-02 Return | RMCD | Template ready (data from Tax Rules) |
| e-Invoice Status | LHDN | ✅ Live dashboard |
| EPF Form A | KWSP | PayrollEngine provides data |
| SOCSO Form 8A | PERKESO | PayrollEngine provides data |
| PCB Form CP39 | LHDN | PayrollEngine provides data |
