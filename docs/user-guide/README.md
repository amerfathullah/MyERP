# MyERP — User Guide

Welcome to the complete user guide for **MyERP**, a modular Enterprise Resource Planning system for Malaysian pharmaceutical-wholesale businesses. It covers **accounting, sales, purchasing, inventory, manufacturing, HR & payroll, CRM, projects, fixed assets, LHDN e-Invoice, and SST tax** — with built-in Malaysian compliance.

> **Live system:** https://erp.mosalah.cloud · default admin login `admin` / `1q2w3E*`

Each guide explains, for every feature: **what it is → why it exists → how to use it (step-by-step)**, plus typical workflows and which roles use it.

---

## 🚀 Start here

| Guide | What it covers |
|-------|----------------|
| **[Getting Started](getting-started.md)** | Logging in, the team logins, selecting your company, navigating the sidebar, roles & permissions, the all-important **Draft → Submit → Post** document lifecycle, and a first-time setup checklist. **Read this first.** |
| **[Core Master Data](core-master-data.md)** | The records everything else builds on — Companies & Branches, Customers, Suppliers, Items, Document Series (numbering), and the Home Dashboard. |

### One concept to understand before anything else
Almost every document in MyERP moves through a lifecycle:

**Draft → Submit → Post**
- **Draft** — you're still editing; nothing has happened in the books or the warehouse.
- **Submit** — the document is validated and locked.
- **Post** — the effects become real: stock moves in/out of the **stock ledger**, and balanced entries hit the **general ledger**.

Nothing affects your inventory or accounts until it is **Posted**. (See [Getting Started](getting-started.md#the-document-lifecycle) for details, including Cancel/Amend.)

---

## 📚 Modules

### Sell — win customers and turn orders into cash
| Guide | Highlights |
|-------|-----------|
| **[CRM](crm.md)** | Leads and Opportunities — the pre-sales pipeline that feeds Sales. |
| **[Sales](sales.md)** | Quotations → Sales Orders → Delivery Notes → Sales Invoices, POS, pricing rules, blanket orders, subscriptions, loyalty, dunning, and sales reports. |
| **[E-Invoice (LHDN MyInvois)](e-invoice.md)** | Submit, validate, and cancel invoices with Malaysia's LHDN e-Invoice system; dashboard, logs, and reports. |

### Buy — source and receive stock
| Guide | Highlights |
|-------|-----------|
| **[Purchasing](purchasing.md)** | Material Requests → RFQ → Supplier Quotations → Purchase Orders → Purchase Receipts → Purchase Invoices, subcontracting, and supplier scorecards. |

### Stock — control your warehouse
| Guide | Highlights |
|-------|-----------|
| **[Inventory](inventory.md)** | Items, warehouses, stock entries, reconciliations (opening stock), batches & expiry (pharma!), serial numbers, quality inspections, landed costs, and stock reports. |
| **[Manufacturing](manufacturing.md)** | BOMs, work orders, job cards, production plans (MRP), workstations. Optional for pure wholesaling — useful for repackaging/kitting. |

### Money — keep the books and stay compliant
| Guide | Highlights |
|-------|-----------|
| **[Accounting](accounting.md)** | Chart of accounts, journal entries, payments, bank reconciliation, budgets, period closing, and 9 financial reports (Trial Balance, P&L, Balance Sheet…). |
| **[Tax (SST)](tax.md)** | Configurable, date-effective SST tax categories & rates, tax summary, and the SST-02 statutory return. |
| **[Fixed Assets](fixed-assets.md)** | Asset register, depreciation (SL/DDB/WDV), repairs, capitalizations, and disposal. |

### People — manage staff and work
| Guide | Highlights |
|-------|-----------|
| **[HR & Payroll](hr-payroll.md)** | Employees, leave, expense claims, salary structures, and Malaysian payroll (EPF/SOCSO/EIS/PCB). |
| **[Projects](projects.md)** | Projects & tasks, timesheets, and timesheet billing. Optional — for service/implementation work. |

### Control — approvals, automation, and administration
| Guide | Highlights |
|-------|-----------|
| **[Workflow & Automation](workflow-automation.md)** | Approval rules (amount thresholds, multi-level), the approver inbox, and event-driven automation rules. |
| **[Settings & Administration](settings-administration.md)** | Company settings, **users & roles (Identity)**, system settings, email templates & notifications, and CSV import/export. |

---

## 👥 Roles at a glance

MyERP is permission-based — the sidebar and available actions change per role. The system ships with these roles (all seeded users use password `1q2w3E*`):

| Role (login) | Primarily uses |
|---|---|
| **Administrator** (`admin`) | Everything |
| **Sales Representative** (`salesrep`) | CRM, Quotations, Sales Orders, Customers |
| **Sales Manager** (`salesmgr`) | All of Sales + approvals + pricing |
| **Warehouse Manager** (`whmanager`) | Inventory (full), Delivery Notes, Purchase Receipts, QC |
| **Warehouse Staff** (`whstaff`) | Day-to-day stock movements |
| **Purchasing Officer** (`purchaser`) | Suppliers, Purchase Orders/Invoices |
| **Accountant** (`accountant`) | Accounting, payments, tax, e-invoice |
| **HR & Payroll Officer** (`hrofficer`) | Employees, payroll, projects |

Manage roles and users under **Administration → Identity** — see [Settings & Administration](settings-administration.md).

---

## 🧭 A typical day, end to end

1. **CRM** → qualify a lead into an opportunity → raise a **Quotation** in Sales.
2. **Sales** → convert the quote to a **Sales Order**, deliver it (**Delivery Note**), and bill it (**Sales Invoice**).
3. **E-Invoice** → submit the invoice to LHDN.
4. **Inventory** → stock is deducted automatically when the delivery/invoice posts; reorder low items.
5. **Purchasing** → raise **Purchase Orders** to restock, **receive** goods, and record supplier **bills**.
6. **Accounting** → record customer **payments**, reconcile the bank, and run your reports.
7. **Period-end** → file **SST-02**, run **payroll**, and close the accounting period.

Start with **[Getting Started](getting-started.md)**, then dive into whichever module matches your role.
