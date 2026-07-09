# MyERP — API Reference

## Base URL

| Environment | URL |
|-------------|-----|
| Development | `http://localhost:5000` |
| Production | `https://api.myerp.example.com` |

## Authentication

All API endpoints require OAuth 2.0 Bearer tokens from the OpenIddict server.

```
Authorization: Bearer <access_token>
```

Token endpoint: `POST /connect/token`

---

## Core Module

### Companies
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/companies` | List companies |
| GET | `/api/app/companies/{id}` | Get company |
| POST | `/api/app/companies` | Create company |
| PUT | `/api/app/companies/{id}` | Update company |
| DELETE | `/api/app/companies/{id}` | Delete company |

### Branches
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/branches` | List branches |
| POST | `/api/app/branches` | Create branch |
| PUT | `/api/app/branches/{id}` | Update branch |
| DELETE | `/api/app/branches/{id}` | Delete branch |

---

## Accounting Module

### Accounts (Chart of Accounts)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/accounts` | List accounts |
| GET | `/api/app/accounts/{id}` | Get account |
| POST | `/api/app/accounts` | Create account |
| PUT | `/api/app/accounts/{id}` | Update account |
| DELETE | `/api/app/accounts/{id}` | Delete account |

### Journal Entries
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/journal-entries` | List entries |
| GET | `/api/app/journal-entries/{id}` | Get entry |
| POST | `/api/app/journal-entries` | Create entry |
| POST | `/api/app/journal-entries/{id}/post` | Post entry |
| POST | `/api/app/journal-entries/{id}/cancel` | Cancel entry |

### Payment Entries
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/payment-entries` | List payments |
| POST | `/api/app/payment-entries` | Create payment |
| POST | `/api/app/payment-entries/{id}/submit` | Submit payment |
| POST | `/api/app/payment-entries/{id}/cancel` | Cancel payment |

### Reports
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/reporting/trial-balance` | Trial balance report |
| GET | `/api/app/reporting/profit-loss` | Profit & loss statement |
| GET | `/api/app/reporting/balance-sheet` | Balance sheet |

---

## Sales Module

### Customers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/customers` | List customers |
| GET | `/api/app/customers/{id}` | Get customer |
| POST | `/api/app/customers` | Create customer |
| PUT | `/api/app/customers/{id}` | Update customer |
| DELETE | `/api/app/customers/{id}` | Delete customer |

### Quotations
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/quotations` | List quotations |
| POST | `/api/app/quotations` | Create quotation |
| POST | `/api/app/quotations/{id}/submit` | Submit quotation |
| POST | `/api/app/quotations/{id}/cancel` | Cancel quotation |

### Sales Orders
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/sales-orders` | List orders |
| POST | `/api/app/sales-orders` | Create order |
| POST | `/api/app/sales-orders/{id}/submit` | Submit order |
| POST | `/api/app/sales-orders/{id}/cancel` | Cancel order |

### Delivery Notes
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/delivery-notes` | List delivery notes |
| POST | `/api/app/delivery-notes` | Create delivery note |
| POST | `/api/app/delivery-notes/{id}/submit` | Submit delivery note |
| POST | `/api/app/delivery-notes/{id}/cancel` | Cancel delivery note |

### Sales Invoices
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/sales-invoices` | List invoices |
| GET | `/api/app/sales-invoices/{id}` | Get invoice |
| POST | `/api/app/sales-invoices` | Create invoice |
| PUT | `/api/app/sales-invoices/{id}` | Update invoice |
| POST | `/api/app/sales-invoices/{id}/submit` | Submit invoice |
| POST | `/api/app/sales-invoices/{id}/post` | Post invoice |
| POST | `/api/app/sales-invoices/{id}/cancel` | Cancel invoice |

### Document Conversion
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/document-conversion/quotation-to-sales-order/{id}` | Convert quotation → SO |
| POST | `/api/app/document-conversion/sales-order-to-delivery-note/{id}` | Convert SO → DN |
| POST | `/api/app/document-conversion/sales-order-to-sales-invoice/{id}` | Convert SO → Invoice |
| POST | `/api/app/document-conversion/delivery-note-to-sales-invoice/{id}` | Convert DN → Invoice |

---

## Purchasing Module

### Suppliers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/suppliers` | List suppliers |
| POST | `/api/app/suppliers` | Create supplier |
| PUT | `/api/app/suppliers/{id}` | Update supplier |
| DELETE | `/api/app/suppliers/{id}` | Delete supplier |

### Purchase Orders
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/purchase-orders` | List POs |
| POST | `/api/app/purchase-orders` | Create PO |
| POST | `/api/app/purchase-orders/{id}/submit` | Submit PO |
| POST | `/api/app/purchase-orders/{id}/cancel` | Cancel PO |

### Purchase Invoices
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/purchase-invoices` | List invoices |
| POST | `/api/app/purchase-invoices` | Create invoice |
| POST | `/api/app/purchase-invoices/{id}/submit` | Submit invoice |
| POST | `/api/app/purchase-invoices/{id}/post` | Post invoice |
| POST | `/api/app/purchase-invoices/{id}/cancel` | Cancel invoice |

---

## Inventory Module

### Items
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/items` | List items |
| POST | `/api/app/items` | Create item |
| PUT | `/api/app/items/{id}` | Update item |
| DELETE | `/api/app/items/{id}` | Delete item |

### Stock Entries
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/stock-entries` | List stock entries |
| POST | `/api/app/stock-entries` | Create stock entry |
| POST | `/api/app/stock-entries/{id}/submit` | Submit entry |
| POST | `/api/app/stock-entries/{id}/post` | Post entry |

### Stock Ledger
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/stock-ledger` | Query stock movements |

---

## E-Invoice Module (LHDN)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/app/e-invoice/submit` | Submit to LHDN |
| GET | `/api/app/e-invoice/{uuid}/status` | Check submission status |
| POST | `/api/app/e-invoice/{uuid}/cancel` | Cancel (within 72h) |
| GET | `/api/app/e-invoice/submissions` | List all submissions |

---

## HR Module

### Employees
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/employees` | List employees |
| POST | `/api/app/employees` | Create employee |
| PUT | `/api/app/employees/{id}` | Update employee |
| DELETE | `/api/app/employees/{id}` | Delete employee |

### Payroll
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/payroll` | List payroll entries |
| POST | `/api/app/payroll` | Create payroll run |
| POST | `/api/app/payroll/{id}/submit` | Submit payroll |
| POST | `/api/app/payroll/{id}/cancel` | Cancel payroll |

---

## CRM Module

### Leads
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/leads` | List leads |
| POST | `/api/app/leads` | Create lead |
| POST | `/api/app/leads/{id}/qualify` | Qualify lead |
| POST | `/api/app/leads/{id}/convert` | Convert to opportunity |
| POST | `/api/app/leads/{id}/mark-lost` | Mark as lost |

### Opportunities
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/app/opportunities` | List opportunities |
| POST | `/api/app/opportunities` | Create opportunity |
| POST | `/api/app/opportunities/{id}/convert` | Convert to customer |
| POST | `/api/app/opportunities/{id}/close` | Close opportunity |

---

## Pagination

All list endpoints support ABP standard pagination:

```
GET /api/app/sales-invoices?skipCount=0&maxResultCount=20&sorting=issueDate desc
```

Response:
```json
{
  "items": [...],
  "totalCount": 150
}
```

---

## Error Responses

ABP standard error format:

```json
{
  "error": {
    "code": "MyERP:03001",
    "message": "Sales order has already been fully invoiced.",
    "details": null,
    "validationErrors": null
  }
}
```

Error code ranges: See [Localization Guide](../docs/testing.md) for full error code registry.
