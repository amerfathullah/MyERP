# Settings & Administration

## Overview

This guide is for the person who **sets up and looks after** MyERP — usually a business owner, office manager, or system administrator. It covers the parts of the system that are configured once and then rarely touched, plus the tools you use to control **who can do what**.

Three ideas run through this whole guide:

1. **Configuration** — the defaults, accounts, tax numbers, and rules that make the system behave the way *your* business works.
2. **People and permissions** — creating logins for your staff and deciding what each of them is allowed to see and do. This is done in the **Administration** area (Identity & Setting Management), which is built on the ABP framework.
3. **Bulk data** — loading customers and items into the system in one go from a spreadsheet, and pulling them back out.

Most of the settings screens live under the **Settings** area of the left sidebar. There is also a **Settings overview** page (a landing page of tiles) that links to the most common settings across the whole app. Because these screens change how the system behaves for everyone, they are **permission-protected** — an ordinary user (say a warehouse clerk) will not even see them. See [Permissions](#permissions) at the end.

> **A note on two kinds of "company settings".** MyERP has a **company profile** (the legal name, tax numbers, address, base currency) *and* a **Company Settings** screen (operational defaults like default accounts and frozen dates). They are two different screens. Both are covered below, in that order.

---

## Company Settings

There are two places that together describe a company. Set them up in this order when you first go live.

### 1. The company profile (legal identity, tax numbers, SST)

**What it is.** The core record for each legal entity you trade as — its name, registration numbers, tax identity, address, and base currency. This is the information that prints on invoices and is sent to LHDN for e-invoicing.

**Where to find it.** Sidebar → **Companies** → open a company (or **New Company**). This opens the company form.

**Why it exists.** Malaysian compliance (LHDN e-Invoice and SST filing) requires accurate tax identifiers on every document. Getting these right once means every invoice afterwards is correct.

**Key fields to fill in:**

| Field | What to enter |
|-------|---------------|
| **Name** / **Short name** | The company's legal and display names |
| **TIN (Tax ID)** | The Tax Identification Number issued by LHDN — **required** for e-Invoice |
| **Registration number** | The SSM business/company registration number (BRN) |
| **SST Registration No.** | Your SST registration number from Royal Malaysian Customs (leave blank if not SST-registered) |
| **MSIC code** | The Malaysian industry classification code for your business |
| **Address / City / State / Postal code / Country** | Registered address (used on documents and e-Invoice) |
| **Currency** | The company's base/reporting currency (defaults to **MYR**) |
| **Fiscal year start month** | The month your financial year begins |

> **Currency is locked once you transact.** You can freely change the base currency while a company is new, but once it has **submitted** transactions the currency is fixed — this protects the integrity of your ledger. Choose carefully at setup.

### 2. The Company Settings screen (operational defaults)

**What it is.** A screen of **operational defaults** that control how transactions behave for a company: which ledger accounts to post to by default, how stock is valued, whether old periods are frozen, and how much over-delivery/over-billing to tolerate.

**Where to find it.** Sidebar → **Settings → Company Settings** (path `/settings/company`), or from the **Settings overview** tile. At the top, pick the company from the **Select Company** dropdown — the form then loads that company's settings.

**Why it exists.** Rather than choosing an income account or valuation method on every single document, you set sensible defaults here once and the system applies them automatically.

**Sections on the screen:**

- **General**
  - **Default Currency** — the 3-letter currency code used for new documents.
  - **Fiscal Year Start Month** — January through December.
  - **Default Valuation Method** — how inventory is costed: **Moving Average**, **FIFO**, or **LIFO**.
- **Freeze Settings** — protect closed periods from accidental changes.
  - **Stock Frozen Upto** — no stock movements can be recorded on or before this date.
  - **Accounts Frozen Till Date** — no accounting entries can be posted on or before this date.
- **Tolerances**
  - **Over Delivery Allowance (%)** — how much more than the ordered quantity a delivery may contain.
  - **Over Billing Allowance (%)** — how much more than the ordered amount an invoice may bill.
- **Default Accounts** — the chart-of-accounts entries used automatically when posting. You can set defaults for: **Receivable**, **Payable**, **Income**, **Expense**, **Bank**, **Inventory**, **Depreciation Expense**, **Accumulated Depreciation**, and **Exchange Gain/Loss**. Each is a dropdown of your accounts shown as *code – name*.

**How to use it (steps):**

1. Go to **Settings → Company Settings**.
2. Choose the company from **Select Company**.
3. Fill in the General, Freeze, Tolerance, and Default Account fields.
4. Click **Save**.

> **Tip on freeze dates.** After you close a month or year (and after tax filing), set the freeze dates. This is the simplest way to stop staff from editing "history" and throwing your reports out of balance.

Editing Company Settings requires the **Companies → Edit** permission (`MyERP.Companies.Edit`).

---

## Users & Roles (Identity)

This is the heart of administration: creating logins for your staff and controlling what each person can do. It lives under **Administration**, which is ABP's built-in **Identity Management** (`/identity`).

**What it is.** Two connected lists:

- **Roles** — a named bundle of permissions (e.g. "Sales Manager"). A role answers the question *"what is this kind of person allowed to do?"*
- **Users** — an individual login (username + password) that is assigned one or more roles.

**Why it exists.** You almost never grant permissions to a person directly. Instead you grant them to a **role**, then put people into roles. When someone changes jobs, you just change their role — no need to re-tick dozens of permissions.

### How permissions drive what people see

MyERP is **permission-based**, and permissions do two things automatically:

- **The sidebar adapts.** Each menu item has a required permission. A warehouse clerk simply will not see the Accounting menus; an accountant may not see warehouse actions. *If a menu isn't there, you don't have permission for it — that's by design, not a bug.*
- **Buttons adapt.** Even inside a screen, actions like **Create**, **Edit**, **Submit**, **Post**, or **Delete** appear only if your role allows them.

So the way to give someone access to a feature is to grant their role the matching permission; the menus and buttons then appear for them the next time they log in.

### The 7 seeded roles (sample staff)

To help you get started, MyERP ships with one sample login for each common job in a pharmaceutical-wholesale business. They all belong to the **PharmaCare Distributors** demo company and all share the password **`1q2w3E*`** (change these before going live).

| Username | Role | Sample person | What this role can typically do |
|----------|------|---------------|----------------------------------|
| `salesrep` | **Sales Representative** | Aisyah Rahman | Create quotations and sales orders; view customers and items. |
| `salesmgr` | **Sales Manager** | Daniel Tan | Oversee sales; approve orders and discounts; manage pricing, loyalty, and the sales team. |
| `whmanager` | **Warehouse Manager** | Ravi Chandran | Run the warehouse: goods receipts, deliveries, stock counts, transfers, and adjustments. |
| `whstaff` | **Warehouse Staff** | Siti Nurhaliza | Pick, pack, and record day-to-day stock movements. |
| `purchaser` | **Purchasing Officer** | Kumar Vel | Raise purchase orders and material requests; record goods received from suppliers. |
| `accountant` | **Accountant** | Mei Ling Wong | Handle invoices, payments, journals, tax (SST) filing, and financial reports. |
| `hrofficer` | **HR & Payroll Officer** | Farah Aziz | Manage employees, leave, and payroll (EPF/SOCSO/EIS/PCB). |

Plus the built-in **`admin`** account (password `1q2w3E*`), which has full access including this Administration area — use it for setup.

> Treat these as *examples*. Rename them, give them real passwords, add more, or delete the ones you don't need. Because each role carries a tailored set of permissions, the sidebar and buttons already look different when you log in as, say, `whstaff` versus `accountant`.

### How to add a new user (new employee)

1. Log in as **`admin`** (or any account with user-management permission).
2. Go to **Administration → Identity → Users**.
3. Click **New user**.
4. On the **User information** tab, enter the **User name**, **Name**, **Email**, and a **Password**.
5. Switch to the **Roles** tab and tick the role(s) that match the person's job — for example tick **Warehouse Staff** for a new picker.
6. Click **Save**.
7. Give the new person their username and password. The next time they log in, their sidebar will already be limited to what their role allows.

> **Pick the role, not the permissions.** For a normal employee you should only need to tick a role. Avoid hand-picking individual permissions per person — it's harder to maintain and easy to get wrong.

### How to create a custom role

If none of the seven sample roles fit a job, build your own.

1. Go to **Administration → Identity → Roles**.
2. Click **New role**.
3. Give it a clear name (e.g. "Branch Supervisor").
4. Save, then open the role's **Actions → Permissions**.
5. Tick exactly the permissions this role needs. Permissions are grouped by area (Sales, Purchasing, Inventory, Accounting, and so on), and most have sub-levels like **View / Create / Edit / Delete**.
6. Click **Save**.
7. Now assign users to this role (see above).

**Recommended approach:** set up your **roles first**, decide their permissions, *then* create users and drop them into roles. Give day-to-day staff **view** access broadly, but reserve **Create / Edit / Delete / Submit** for the people who are accountable for that area.

> Remember to replace every sample password (`1q2w3E*`) with a strong, unique password before you handle real data.

---

## System Settings (email, timezone)

**What it is.** Application-wide technical settings, found under **Administration → Settings** (ABP's Setting Management, `/setting-management`). These affect the whole system rather than a single company.

**Why it exists.** Some behaviour — how the system sends email, what time zone dates are shown in — has to be configured centrally, once.

**Common tabs you'll use:**

- **Emailing (SMTP)** — the outgoing mail server MyERP uses to send notifications, dunning letters, and e-invoice alerts. Enter your mail host, port, sign-in credentials, the **default "from" address**, and whether SSL is required. There is usually a **Send test email** button to confirm it works. *If email templates and notifications aren't going out, this is the first place to check.*
- **Time Zone** — the time zone used to display and record dates and times.
- **Account** settings — password complexity and self-service registration options.

**How to use it (steps):**

1. Go to **Administration → Settings**.
2. Open the tab you need (e.g. **Emailing**).
3. Change the values and click **Save**.
4. For email, send a test message to confirm the server accepts it.

---

## Email Templates & Notifications

These two screens work as a pair: **templates** define the messages, and **notification logs** show you whether they were delivered. Both live under **Settings** and are governed by the **Automation Rules** permission (`MyERP.AutomationRules`).

### Email Templates

**What it is.** Reusable message templates for automated emails — for example a "your invoice is ready" note or a **dunning** (overdue-payment) reminder. Path: **Settings → Email Templates** (`/settings/email-templates`).

**Why it exists.** So the system can send consistent, branded messages automatically without you retyping them each time.

**Each template has:**

- **Name** — an internal label.
- **Document Type** — which kind of document triggers it: *Sales Invoice, Purchase Invoice, Sales Order, Delivery Note, Payment Entry, Dunning*, or **Any**.
- **Subject** and **Body** — the message text. The body accepts **HTML** and **placeholders** written in curly braces, e.g. `{customer}`, `{invoice_no}`, `{amount}`, `{due_date}`. When the email is sent, each placeholder is replaced with the real value.

**How to use it (steps):**

1. Go to **Settings → Email Templates** and click **New Template**.
2. Enter a name, pick a document type, and write the subject and body (drop in `{placeholders}` where you want live values).
3. Click **Save**.
4. Open the template again and click **Preview** — the system fills the placeholders with sample data so you can see how it will look before it goes to a customer.
5. Use **Edit** or **Delete** on any template later as needed.

### Notification Logs

**What it is.** A searchable history of every notification the system tried to send. Path: **Settings → Notification Logs** (`/settings/notification-logs`).

**Why it exists.** So you can confirm that important messages (invoices, reminders) actually reached people — and spot failures early.

**What you see:** date/time, recipient, subject, **channel** (Email, In-App, or Push), **status** (Queued, Sent, Failed, or Permanently Failed), and how many times a send was retried. A red **"N failed"** badge at the top warns you when messages didn't get through.

**How to use it (steps):**

1. Go to **Settings → Notification Logs**.
2. Filter by **Channel** and/or **Status** (e.g. show only **Failed** emails).
3. If you see failures, check your **Emailing (SMTP)** settings under Administration → Settings — a bad mail server is the usual cause.

---

## Import / Export (bulk data)

**What it is.** A tool to load records into MyERP from a spreadsheet, and to export existing records back out. Path: sidebar → **Import / Export** (`/import-export`). Governed by the **Import/Export** permission (`MyERP.ImportExport`).

**Why it exists.** When you first set up the system — or migrate from another one — you don't want to type thousands of customers and items by hand. Import does it in one go. Export is handy for backups, bulk edits, and sharing data.

> **Supported data today:** **Customers** and **Items**. (Other records such as suppliers are created through their own screens.)

### Importing

**File format:** a **CSV** file (spreadsheet saved as `.csv`; `.xlsx` may also be accepted). The **first row must be column headers**, and each following row is one record. Column names are matched by heading, and a couple of alternative spellings are accepted.

**Customer columns:**

| Column | Required? | Notes |
|--------|-----------|-------|
| `Name` (or `CustomerName`) | **Yes** | The customer's name |
| `CompanyId` | **Yes** | Which company this customer belongs to (the company's ID) |
| `CustomerCode` (or `Code`) | No | Your own code for the customer |
| `TIN` (or `TaxId`) | No | Tax Identification Number |
| `Email` | No | |
| `Phone` | No | |

**Item columns:**

| Column | Required? | Notes |
|--------|-----------|-------|
| `ItemName` (or `Name`) | **Yes** | The item's name |
| `CompanyId` | **Yes** | Which company this item belongs to |
| `ItemCode` (or `Code`) | No | Defaults to the name if left blank |
| `UOM` | No | Unit of measure; defaults to "Unit" |
| `StandardSellingPrice` (or `Price`) | No | The default selling price |

> **The easiest way to get the right headings** is to run an **Export** first (see below). The file you get back already has the correct column names — clear the data rows, fill in your own, and import it.

**How to import (steps):**

1. Go to **Import / Export**.
2. In the **Import Data** card, choose the **Entity Type** (Customer or Item).
3. Click **Choose File** and select your CSV.
4. Click **Start Import** and wait for the progress bar to finish.
5. Read the result message: it tells you how many rows imported, and whether any **failed** (a *partial* result means some rows had problems).
6. Check the **Import History** table at the bottom — it lists each import with its file name, entity type, status, and a *succeeded/total* count so you can see exactly what happened.

> If some rows fail, they are usually missing a required column (like `CompanyId` or `Name`). Fix those rows in your spreadsheet and import again.

### Exporting

**How to export (steps):**

1. Go to **Import / Export**.
2. In the **Export Data** card, choose the **Entity Type** (Customer or Item).
3. Click **Export CSV**.
4. The file downloads to your computer automatically. Open it in Excel or Google Sheets.

Use export for backups, for bulk-editing data offline, or as a ready-made template for a future import.

---

## E-Invoice Settings (pointer)

**Settings → E-Invoice (LHDN)** (`/settings/einvoice`) is where you connect MyERP to the **Malaysian LHDN MyInvois** system: enter your API credentials (Sandbox or Production), check the connection status, upload your **digital certificate** for document signing, and look up a taxpayer's **TIN**. Because e-invoicing is a large topic in its own right, the full step-by-step is in the **E-Invoice guide**. Access requires the **E-Invoice** permission (`MyERP.EInvoice`).

## Authorization Rules (pointer)

**Settings → Authorization Rules** (`/settings/authorization-rules`) is where you set **approval thresholds** — for example "any sales order over RM 50,000 needs Sales Manager approval", or "a discount above 15% must be approved". Each rule names a **transaction type**, what it's **based on** (an amount or a discount %), the **threshold**, the **approving role**, and its **scope** (global, per-role, or per-user). These rules feed the approvals system; the full workflow — how approvals are requested, seen, and granted — is covered in the **Workflow / Approvals guide**. Access requires the **Approval Workflows** permission (`MyERP.ApprovalWorkflows`).

---

## Permissions

Everything in this guide is **admin-only** by design. Here is who can reach each area:

| Area | Required permission | Typically granted to |
|------|--------------------|---------------------|
| Company profile (Companies) | `MyERP.Companies` (View), `MyERP.Companies.Edit` to change | Administrator / Accountant |
| Company Settings screen | `MyERP.Companies.Edit` | Administrator / Accountant |
| Users & Roles (Identity) | ABP Identity permissions | Administrator only |
| System Settings (Emailing, Time Zone) | ABP Setting Management permission | Administrator only |
| Email Templates | `MyERP.AutomationRules` | Administrator |
| Notification Logs | `MyERP.AutomationRules` | Administrator |
| E-Invoice Settings | `MyERP.EInvoice` | Administrator / Accountant |
| Authorization Rules | `MyERP.ApprovalWorkflows` | Administrator / Manager |
| Import / Export | `MyERP.ImportExport` | Administrator |

Notes:

- If a settings screen or menu isn't visible to a user, they lack its permission — grant it on their **role** (Administration → Identity → Roles → Permissions), not on the person.
- Keep the powerful areas — **Identity**, **System Settings**, and **Import/Export** — restricted to a small number of trusted administrators. A mistake here affects everyone.
- Always replace the seeded sample passwords (`1q2w3E*`) with strong, unique passwords before you go live.
