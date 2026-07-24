# Getting Started with MyERP

Welcome to **MyERP** — the system your business uses to run sales, purchasing, inventory (stock), accounting, and more, all in one place. This guide walks you through signing in, finding your way around, and setting the system up correctly the first time.

You do not need to be technical to use MyERP. If you can use online banking or a web shop, you can use this. Take it one section at a time.

> **The single most important idea in MyERP** is the *document lifecycle* — **Draft → Submit → Post**. If you only read one section, read [The document lifecycle](#the-document-lifecycle-very-important).

---

## Logging in

MyERP runs in your web browser. Nothing to install.

1. Open your browser and go to **https://erp.mosalah.cloud**.
2. You will land on a login page. Enter your **username** and **password**.
3. Click **Login**. You will arrive at the **Home dashboard**.

If you are ever signed out, or you click a link while logged out, MyERP automatically sends you back to this login page. Just sign in again and you will return to where you were.

### The administrator account

The system is delivered with one built-in administrator:

| Username | Password | What it's for |
|----------|----------|---------------|
| `admin`  | `1q2w3E*` | Full access to everything, including user and role management. Use this for first-time setup. |

> **Security note:** Change the `admin` password (and all the sample passwords below) before you go live with real data. See [Roles & permissions](#roles--permissions).

### The seeded team logins

To help you get started, MyERP comes with a set of sample user accounts — one for each common job role. Every one of these uses the password **`1q2w3E*`**.

| Username     | Role (job)          | What this person typically does |
|--------------|---------------------|---------------------------------|
| `salesrep`   | Sales Representative | Creates quotations and sales orders for customers. |
| `salesmgr`   | Sales Manager        | Oversees sales, approves orders, manages pricing and the sales team. |
| `whmanager`  | Warehouse Manager    | Runs the warehouse: receipts, deliveries, stock counts and adjustments. |
| `whstaff`    | Warehouse Staff      | Picks, packs, and records day-to-day stock movements. |
| `purchaser`  | Purchaser / Buyer    | Raises purchase orders and records goods received from suppliers. |
| `accountant` | Accountant           | Handles invoices, payments, journals, tax filing, and reports. |
| `hrofficer`  | HR Officer           | Manages employees and payroll. |

Think of these as *examples* of how you can organise your own staff. You can rename them, give them real passwords, add more, or remove the ones you don't need — all from the Administration area (see [Roles & permissions](#roles--permissions)).

---

## Selecting your company

MyERP supports **more than one company** in the same system (for example, if your business trades under several legal entities). Nearly every record — a customer, an item, an invoice — belongs to one specific company.

- Look for the **company selector** in the **top bar** at the top of the screen. It shows the name of the company you are currently working in.
- Click it to switch to a different company. The dashboard, lists, and forms will then show data for the company you picked.
- Your choice is remembered on your device, so the next time you log in you are back in the same company.

> When you create a new record such as a customer or item, always check the **Company** field first. Putting a record under the wrong company is a common early mistake.

If you only run one company, you can safely ignore this — MyERP simply keeps you in that one company all the time.

---

## Navigating the app

Everything is reached from the **left sidebar menu**. Menu items are grouped by area of the business:

| Sidebar group | What lives there |
|---------------|------------------|
| **Home**        | Your dashboard — KPIs and quick actions. |
| **Companies**   | Your company (and branch) records. |
| **Customers**   | The businesses you sell to. |
| **Suppliers**   | The businesses you buy from. |
| **CRM**         | Leads and opportunities (sales pipeline). |
| **Sales**       | Quotations, sales orders, invoices, delivery notes, POS, loyalty, salespersons. |
| **Purchasing**  | Purchase orders, purchase invoices, goods receipts, material requests. |
| **Inventory**   | Items, warehouses, stock entries, stock reports, quality inspections, reconciliations. |
| **Accounting**  | Chart of accounts, journals, payments, and the financial reports (Trial Balance, Profit & Loss, Balance Sheet). |
| **E-Invoice**   | Malaysian LHDN e-invoicing dashboard, submission logs and status reports. |
| **Tax**         | Tax categories, tax summary, and SST-02 filing. |
| **Workflow**    | Pending approvals and approval rules. |
| **HR**          | Employees and payroll. |
| **Import / Export**, **Automation**, and more. |

Clicking a group expands it to reveal the screens inside.

### Search

Use the **global search** in the top bar to jump straight to a record (a customer, an item, a document) without hunting through menus. Start typing a name or number and pick from the results.

### Language (English / العربية)

MyERP speaks both **English** and **Arabic (العربية)**. Use the **language switch** in the top bar. When you choose Arabic, the whole screen flips to a **right-to-left (RTL)** layout automatically. Switch back to English at any time — no data is affected.

---

## Roles & permissions

MyERP is **permission-based**. This means the system only shows each person what their job needs:

- **The sidebar adapts.** A warehouse staff member will not see the Accounting menus; an accountant may not see warehouse actions. If a menu isn't visible to you, you don't have permission for it — that's by design, not a fault.
- **Actions adapt too.** Even within a screen, buttons like *Create*, *Edit*, *Submit*, or *Post* only appear if your role allows them.

### Managing users and roles

Only administrators can do this. Go to **Administration → Identity** (available from the `admin` account):

1. **Roles** — create roles (e.g. "Sales Manager") and tick exactly which permissions each role should have.
2. **Users** — create a login for each staff member, set their password, and assign them one or more roles.

The recommended approach: set up your roles first, then create users and drop them into the right roles. When someone changes jobs, you just change their role — no need to reconfigure them field by field.

> Remember to replace the sample passwords (`1q2w3E*`) with strong, unique passwords for every real user.

---

## The document lifecycle (VERY IMPORTANT)

Almost every transaction in MyERP — a sales invoice, a purchase order, a goods receipt, a stock entry, a journal — moves through **three stages**. Understanding these three words is the key to using the system confidently.

```
   DRAFT  ───▶  SUBMIT  ───▶  POST
 (editable)   (locked &      (writes to stock
              validated)     and/or the ledger)
```

### 1. Draft

When you first create and save a document, it is a **Draft**.

- A draft is a **work in progress**. You can edit it freely, fix mistakes, or delete it.
- **A draft affects nothing.** It does *not* change your stock levels and it does *not* affect your accounts. It is just a saved form.

### 2. Submit

When the document is complete and correct, you **Submit** it.

- MyERP **validates** the document — it checks the numbers add up, required fields are filled, stock is available, credit limits aren't exceeded, and so on. If something is wrong, it tells you and refuses to submit.
- Once submitted, the document is **locked**. You can no longer casually edit it, which protects the integrity of your records.
- Submitting may trigger an **approval** step if your business has approval rules (see the Workflow menu). In that case the document waits for a manager to approve before it can go further.

### 3. Post

**Posting** is the moment the document becomes *real* in your books.

- Posting writes to the **stock ledger** (updating item quantities and valuation) and/or the **general ledger** (creating the accounting entries — debits and credits).
- **Nothing hits your stock or your accounts until a document is Posted.** This is the golden rule. A draft or even a submitted-but-not-posted document has not moved any stock or money.

> **Why this matters:** it means you can prepare and review documents safely, and only commit them to your official records when you're ready. Your stock reports and financial statements only ever reflect **posted** documents.

### Cancel and Amend

- **Cancel** — reverses a submitted/posted document. MyERP creates the opposite ledger entries so your books stay balanced, and it prevents cancelling a document that other documents depend on (you'll be asked to cancel those first).
- **Amend** — after cancelling, you create a corrected **new version** rather than editing history. This keeps a clean, auditable trail of what changed and when.

---

## First-time setup checklist

Set things up in **this order**. Later steps rely on earlier ones, so following the sequence saves you a lot of backtracking. (Detailed, step-by-step instructions for each master-data record are in the companion guide, **Core Master Data**.)

1. **Create your Company.**
   Companies → New. Enter the legal name, currency (MYR), fiscal year start, and your tax details (SST registration, MSIC code). Everything else hangs off the company.

2. **Chart of Accounts & Fiscal Year — already done for you.**
   When you create a company, MyERP **automatically seeds** a Malaysian chart of accounts, a fiscal year, cost centres, and a default set of warehouses (*All Warehouses → Stores, Finished Goods, Work In Progress, Goods In Transit*). Review these under **Accounting → Chart of Accounts** and **Inventory → Warehouses**; you rarely need to change them.

3. **Set up Document Series (numbering).**
   Define how each document type is numbered (e.g. `INV-2026-00001`). **This is essential** — documents that have no matching series will fail to save. See [Document Series](core-master-data.md#document-series-numbering) in the master-data guide.

4. **Review / add Warehouses.**
   The defaults cover most needs. Add extra warehouses if you store stock in more than one physical place.

5. **Create your Items.**
   Add the medicines and devices you buy and sell — item code, unit of measure, group, prices, and stock settings.

6. **Add your Suppliers and Customers.**
   The manufacturers/principals you buy from, and the pharmacies, hospitals, and clinics you sell to.

7. **Enter opening balances and opening stock.**
   Tell MyERP what stock you already hold (via a stock reconciliation / opening stock entry) and your opening account balances, so the system starts from your true current position.

8. **Start transacting.**
   You're ready. Raise quotations, sales orders, purchase orders, invoices, goods receipts and deliveries — and move each one through **Draft → Submit → Post**.

---

**Next:** [Core Master Data](core-master-data.md) — how to create and maintain companies, customers, suppliers, items, document series, and understand your dashboard.
