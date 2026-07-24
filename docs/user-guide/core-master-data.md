# Core Master Data

**Master data** is the reference information your business is built on — your companies, the customers you sell to, the suppliers you buy from, and the items you trade. You set it up once, keep it tidy, and every transaction (invoices, orders, stock movements) reuses it.

Get master data right and everything downstream is easier: prices fill in automatically, tax is calculated correctly, and your reports are trustworthy.

This guide covers each core record: **what it is**, **why it exists**, and **how to create and maintain it** with real menu paths and field names.

> New to MyERP? Read [Getting Started](getting-started.md) first — especially the [document lifecycle](getting-started.md#the-document-lifecycle-very-important).

---

## Companies & Branches

### What it is
A **Company** is a legal trading entity in your business. A **Branch** is a physical location or office belonging to a company (for example, a head office and a regional depot). MyERP is **multi-company** and **multi-branch**: you can run several companies in one system, each with its own accounts, stock, customers, and documents.

### Why it exists
Almost every other record — customers, items, invoices — belongs to a company. The company also carries the settings that drive accounting and tax: the **currency**, the **fiscal year**, and your **SST / tax registration** details used on invoices and e-invoicing. Without a company, nothing else can be created.

### How to create & maintain it

**Menu path:** Sidebar → **Companies** → **New**

Fill in the company form:

| Field | What to enter |
|-------|---------------|
| **Name** *(required)* | The full legal company name. |
| **Short Name** | A brief label used in lists. |
| **Registration Number** | Your SSM / company registration number. |
| **Tax ID** | The company tax identification number. |
| **SST Registration Number** | Your SST registration (for Malaysian sales & service tax). |
| **MSIC Code** | Your Malaysia Standard Industrial Classification code (required for LHDN e-invoicing). |
| **Phone / Email / Website** | Company contact details. |
| **Address / City / State / Postal Code / Country** | Registered address (Country defaults to **Malaysia**). |
| **Currency Code** *(required)* | Your base currency — defaults to **MYR**. |
| **Fiscal Year Start Month** | The month your financial year begins (e.g. January). |
| **Active** | Tick to keep the company in use. |

Click **Save**.

> **What happens automatically:** the first time a company is created, MyERP seeds a **Malaysian chart of accounts**, a **fiscal year**, **cost centres**, and a **default warehouse hierarchy** (*All Warehouses → Stores, Finished Goods, Work In Progress, Goods In Transit*). You don't have to build these by hand.

**Maintaining it:** open **Companies**, click a company to **edit**. Note that the **currency cannot be changed** once submitted transactions exist for the company — this protects the integrity of your historical accounts.

**Branches:** add branches to represent additional locations under a company. A branch carries its own name, code, address, and contact details, and one branch can be marked as the **headquarters**.

---

## Customers

### What it is
A **Customer** is anyone you sell to — in this business typically **pharmacies, hospitals, and clinics**.

### Why it exists
The customer record stores who they are, their tax identity (needed for e-invoicing), and their **credit control** and **accounting defaults** so that sales documents fill themselves in correctly and post to the right account.

### How to create & maintain it

**Menu path:** Sidebar → **Customers** → **New Customer**

| Field | What to enter |
|-------|---------------|
| **Company** *(required)* | The company this customer trades with. |
| **Customer Name** *(required)* | The pharmacy / hospital / clinic name. |
| **Customer Code** | Your internal reference code (optional; can be auto-numbered). |
| **TIN** | The customer's Tax Identification Number, required for LHDN e-invoices (there's an example format shown in the field). |
| **Registration Number** | Their business/company registration number. |
| **SST Registration** | Their SST number, if registered. |
| **ID Type / ID Value** | Identification type (e.g. **BRN** for business registration) and its value — used on e-invoices. |
| **Contact Person / Phone / Email** | Who to deal with and how to reach them. |
| **Address / City / State / Postal Code / Country** | Their address (Country defaults to Malaysia). |
| **Active** | Tick to allow selling to them. |

Click **Save**.

**Accounting & credit defaults:** each customer also carries a **default receivable account** (the accounts-receivable account their invoices post to), a **credit limit**, and **default payment terms**. These drive credit control — for example, MyERP warns you (and can block a sale) when a customer would exceed their credit limit — and ensure invoices post to the correct ledger account. If left blank, the customer falls back to the company's default receivable account.

**Maintaining it:** open **Customers** and click a customer to edit. When you open an existing customer, MyERP also shows their **Outstanding Invoices** and a running **Total outstanding**, so you can see at a glance how much they owe before you sell more.

---

## Suppliers

### What it is
A **Supplier** is anyone you buy from — here typically the **manufacturers and principals** who supply your medicines and devices.

### Why it exists
The supplier record holds their identity, tax details, and the **payable account** their bills post to, so purchasing documents are accurate and post to the right place.

### How to create & maintain it

**Menu path:** Sidebar → **Suppliers** → **New Supplier**

| Field | What to enter |
|-------|---------------|
| **Company** *(required)* | The company that buys from this supplier. |
| **Supplier Name** *(required)* | The manufacturer / principal name. |
| **Supplier Code** | Your internal reference code (optional). |
| **TIN** | The supplier's Tax Identification Number. |
| **Registration Number** | Their business registration number. |
| **SST Registration** | Their SST number, if registered. |
| **ID Type / ID Value** | Identification type (e.g. **BRN**) and value. |
| **Contact Person / Phone / Email** | Contact details. |
| **Address / City / State / Postal Code / Country** | Their address. |
| **Active** | Tick to allow purchasing from them. |

Click **Save**.

**Accounting default:** the supplier carries a **default payable account** (accounts-payable). Purchase invoices post the amount you owe to this account; if it's blank, MyERP uses the company's default payable account.

**Maintaining it:** open **Suppliers** and click a supplier to edit. An existing supplier's page also shows **Outstanding** payables and a **Total** — how much you currently owe them.

---

## Items

### What it is
An **Item** is anything you buy, sell, or hold in stock — for this business, your **medicines and devices**. Items also cover services and fixed assets.

### Why it exists
The item record is the single source of truth for what a product is called, how it's counted, what it costs and sells for, and how its stock is tracked and valued. Every sales line, purchase line, and stock movement points back to an item.

### How to create & maintain it

**Menu path:** Sidebar → **Inventory → Items** → **New**

| Field | What to enter |
|-------|---------------|
| **Company** *(required)* | The company that owns this item. |
| **Item Code** *(required)* | A unique code for the product (e.g. `PARA-500`). |
| **Item Name** *(required)* | The product's descriptive name. |
| **Description** | Fuller details, pack size, etc. |
| **Item Type** *(required)* | **Goods**, **Service**, or **Fixed Asset**. Medicines and devices are *Goods*. |
| **Item Group** | A category for grouping and reporting (e.g. Antibiotics, Surgical Devices). |
| **Brand** | The product brand / manufacturer name. |
| **UOM** (Unit of Measure) | How you count it — e.g. *Unit*, *Box*, *Strip*. Defaults to *Unit*. |
| **Standard Buying Price** | The usual cost price. |
| **Standard Selling Price** | The usual sell price. |
| **Valuation Method** | How stock value is calculated: **FIFO** (default), **Weighted Average**, **LIFO**, or **Standard Cost**. |
| **Tax Category** | The tax treatment for this item (links to Tax → Tax Categories). |
| **Maintain Stock** | Tick for physical goods you track (leave off for services). |
| **Reorder Level** | The stock level at which the item flags for reordering. |
| **Reorder Qty** | How much to reorder when you hit that level. |
| **Safety Stock** | A minimum buffer you aim to always keep. |
| **Min Order Qty** | The smallest quantity you'll order. |
| **Inspection required before Purchase / Delivery** | Tick to require a passed **Quality Inspection** before goods can be received or delivered. |
| **Active** | Tick to keep the item usable. |

Click **Save**.

> **Note on Valuation Method:** once an item has stock movements, MyERP restricts changing its valuation method (some switches are blocked entirely) to keep your stock accounts consistent. Choose it thoughtfully when you first create the item.

**Maintaining it:** open **Inventory → Items** and click an item to edit. An existing item's page shows its **current stock levels** per warehouse.

### Pharma essentials: batches & expiry
For a pharmaceutical wholesaler, **batch numbers**, **manufacturing/expiry dates**, and **shelf life** are critical — you need to sell oldest-first and never ship expired stock. Items can be configured to track **batches** (and where relevant, **serial numbers**), and MyERP records manufacturing dates, expiry dates, and shelf life against each batch. These are managed in the **Inventory** area (Batches, Serial Numbers, Quality Inspections) and are covered in detail in the **Inventory guide** rather than here.

---

## Document Series (numbering)

### What it is
A **Document Series** defines how a type of document is automatically numbered. For example, a series with prefix `INV-2026-` and 5-digit padding produces `INV-2026-00001`, `INV-2026-00002`, and so on. Each series belongs to a **company** and applies to one **document type** (Sales Invoice, Purchase Order, etc.).

### Why it exists — and why documents fail without it
Every posted document needs a **unique, sequential number** for legal, audit, and e-invoicing purposes. MyERP generates that number from the matching series. **If no active series exists for a document type in a company, creating that document will fail** — the system has no way to number it. This is why setting up your series is a required first-time step.

### How to create & maintain it

Document Series are set up **per company**, one for each document type you'll use (Sales Invoice, Sales Order, Delivery Note, Purchase Order, Purchase Invoice, Purchase Receipt, Journal Entry, Payment Entry, and so on).

For each series you define:

| Field | What it means |
|-------|---------------|
| **Name** | A friendly label, e.g. "Sales Invoice Numbering". |
| **Document Type** | Which document this numbers, e.g. `SalesInvoice`, `PurchaseOrder`. |
| **Prefix** | The text at the front of every number, e.g. `INV-`, `PO-2026-`. |
| **Number Padding** | How many digits to pad to, e.g. `5` → `00001`. |
| **Current Number** | The counter; the next document is Current Number + 1. |
| **Reset on Fiscal Year** | If on, the counter restarts at 1 each new financial year and the year is woven into the number (e.g. `SI-2026-00001`). |
| **Active** | The series must be active to be used. |

**Best practice:** create every series you need **before you start transacting**. Include the year in the prefix if you like clean yearly runs, and turn on **Reset on Fiscal Year** so numbering starts fresh each year. Avoid editing the Current Number backwards — that risks duplicate numbers.

---

## The Home Dashboard

### What it is
The **Home dashboard** is the first screen you see after logging in. It's a live summary of your business for the **currently selected company**.

### Why it exists
It gives owners and managers an at-a-glance health check — money in, money out, what needs attention — and one-click shortcuts to the tasks you do most, without digging through menus.

### What's on it

**Menu path:** Sidebar → **Home**

**KPI tiles (top summary cards)** — click a tile to jump to the underlying list:

| Tile | What it shows |
|------|---------------|
| **Revenue (MTD)** | Sales revenue month-to-date. |
| **Outstanding Invoices** | Money customers still owe you. |
| **Pending POs** | Purchase orders not yet completed. |
| **E-Invoices Submitted** | How many e-invoices have gone to LHDN. |

**Count cards:** quick totals for **Customers**, **Suppliers**, **Items**, and **Pending Approvals**.

**Financial Overview** (when available): **Revenue**, **Expenses**, **Net Profit** (with profit margin), **Receivables**, **Payables**, and **Revenue Growth** for the period.

**Revenue Trend:** a simple bar chart of revenue over recent months.

**Recent Activity:** the latest document actions (created, submitted, posted) so you can see what's been happening.

**Quick Actions** — one-click shortcuts to:

- New Sales Invoice
- New Purchase Order
- Journal Entry
- LHDN Dashboard
- Run Payroll
- Stock Ledger

> The dashboard reflects the **company selected in the top bar**. Switch companies there to see each one's figures.

---

**See also:** [Getting Started](getting-started.md) for logging in, navigation, roles, and the all-important document lifecycle.
