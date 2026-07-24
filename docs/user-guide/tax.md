# Tax (SST)

## Overview

Malaysia levies **Sales & Service Tax (SST)** instead of a broad-based GST. Two taxes sit under the SST umbrella:

- **Sales Tax** — charged on manufactured and imported goods (commonly **5%** or **10%**).
- **Service Tax** — charged on prescribed services (commonly **6%**, with certain services at **8%**).

Some supplies carry **no tax**. In SST there are two distinct "no-tax" situations that must never be confused on a return:

- **Exempt** — the goods or services fall outside the tax (many pharmaceuticals and essential medicines qualify).
- **Zero-rated** — the supply is taxable but at 0%, typically **exports**.

For a pharmaceutical wholesaler this distinction matters: a large part of your catalogue may be exempt, while the taxable lines still have to be tracked, collected, and reported.

**How MyERP handles SST.** MyERP treats tax as **data you configure, not numbers baked into the software**. Rates live in **Tax Rules** that are effective for a date range, so when Customs changes a rate you add a new rule with a new effective date — nothing in the system is "hardcoded". The tax engine always looks up the rule that was valid on the transaction's date. This means historical invoices keep their original rate and new invoices automatically pick up the new one.

The Tax module has three screens, all under the **Tax** area of the menu:

| Menu item | Path | What it is for |
|-----------|------|----------------|
| **Tax Categories** | `/tax/categories` | Define tax categories and their date-effective rate rules |
| **Tax Summary** | `/tax/summary` | Management report of tax collected vs. tax paid for a period |
| **SST-02 Filing** | `/tax/sst02-filing` | The statutory SST-02 return, laid out section by section |

All amounts are shown in Malaysian Ringgit (RM / MYR).

---

## Tax Categories & Rules

### What

A **Tax Category** is a named type of tax — for example *SST Sales 10%*, *SST Service 6%*, or *Exempt*. Each category has a **Type**, which is one of:

| Type | Meaning |
|------|---------|
| **Sales** | Sales Tax on goods |
| **Service** | Service Tax on services |
| **Exempt** | Exempt supplies (no tax charged) |
| **Zero-Rated** | Taxable at 0% (e.g. exports) |
| **Out of Scope** | Falls entirely outside SST |

A **Tax Rule** attaches an actual **rate** and an **effective date range** to a category. A category can hold several rules over time — that is how a rate change is recorded without touching old invoices.

### Why

Keeping rates in rules (rather than fixed in the software) means:

- **Rate changes are safe.** When Customs moves Service Tax from 6% to 8%, you add a new rule effective from the change date. Invoices before that date keep 6%; invoices on or after it use 8%.
- **Exemptions are explicit.** Pharmaceuticals that are exempt or zero-rated are set up as their own categories, so they are reported correctly and never accidentally taxed.
- **Full history is preserved.** The system can always reproduce the tax on any past invoice, which is exactly what an audit requires.

### How — create a Tax Category

1. Open **Tax → Tax Categories**.
2. Click **New Category**.
3. Fill in:
   - **Code** — a short unique identifier (e.g. `SST-S10`, `EXEMPT`).
   - **Name** — a readable label (e.g. *SST Sales 10%*).
   - **Type** — Sales, Service, Exempt, Zero-Rated, or Out of Scope.
   - **Description** *(optional)* — a note on what it covers.
   - **Active** — leave ticked so it can be used on documents.
4. Click **Save**.

### How — add a date-effective Tax Rule

1. In the Tax Categories list, expand the category you want.
2. Click **Add Rule** and complete:
   - **Rate (%)** — the percentage, e.g. `10` for 10%. For Exempt / Zero-Rated categories use `0`.
   - **Effective From** — the first date this rate applies. **Required.**
   - **Effective To** — the last date it applies. Leave **blank** for "no expiry" (applies indefinitely).
   - **Item Group Filter** *(optional)* — restrict the rule to a specific item group.
   - **Region Filter** *(optional)* — restrict the rule to a state/region.
   - **Priority** — when more than one rule matches, the **higher** priority wins.
   - **Description** *(optional)*.
3. Click **Save**.

> **Rate change example.** To move Service Tax from 6% to 8% on 1 March 2024: keep the existing 6% rule and set its **Effective To** to 29 Feb 2024, then add a new rule at **8%** with **Effective From** 1 March 2024. Every invoice is then taxed at the rate valid on its own date.

### How — apply tax to items and invoices

MyERP works out the tax on a line using the rule that is valid on the **transaction date**, matched by category (and, if set, item group and region). Where a particular item needs to differ from the document's normal tax, you use an **Item Tax Template**:

- An **Item Tax Template** holds one or more lines, each pointing at a tax account with a specific **rate**.
- A line can be marked **Not Applicable (N/A)**. This is the important setting for **exempt pharmaceuticals**: an N/A line tells the system to **exclude that tax entirely** for items using the template, rather than charging 0% as a taxable line.
- When an item carries a template, its rate **overrides** the document-level rate for the matching tax; where the template says nothing, the document-level rate is used.

**To set up an exempt (or special-rate) product:**

1. Create an **Item Tax Template** with a descriptive **Title** (e.g. *Exempt Pharmaceuticals*).
2. Add a detail line for the relevant tax account and either enter its **rate** or tick **Not Applicable** for a true exemption.
3. Assign that template to the exempt medicines in your catalogue.

From then on, invoices for those items are handled automatically — no manual tax entry per invoice.

---

## Tax Summary report

### What it shows

**Tax → Tax Summary** is a management view of your tax position for a chosen period. You pick a **From** and **To** date and click **Generate Report**. It reads all **posted** sales and purchase invoices in that window and presents:

- **Output Tax (Sales)** — tax collected on sales, minus credit-note adjustments, giving **Net Output Tax**. Shows the sales-invoice and credit-note counts, plus a **breakdown by rate**.
- **Input Tax (Purchases)** — tax paid on purchases, minus debit-note adjustments, giving **Net Input Tax**, with the same rate breakdown.
- **Net Tax Position** — Net Output Tax minus Net Input Tax. A green banner shows **Tax Refundable** when input exceeds output; a red banner shows **Tax Payable** otherwise.

Only invoices in **Posted** status are counted; drafts and cancelled documents are ignored. Sales returns (credit notes) and purchase returns (debit notes) are handled as adjustments, not as extra tax.

You can **Export CSV** to hand the figures to your accountant or drop them into a spreadsheet.

### When to run it

- **Mid-period**, to see your likely SST liability building up before the return is due.
- **Before filing**, as a sanity check against the SST-02 figures.
- **Any time** you need a quick read on tax collected vs. tax paid for a date range.

---

## SST-02 Filing

### What it is

**SST-02** is the statutory return that SST-registered businesses submit to the **Royal Malaysian Customs Department (RMCD)**. Registered manufacturers and service providers file it **bimonthly** (every two months), **within 28 days** of the end of the taxable period.

The **Tax → SST-02 Filing** screen builds this return for you from your posted invoices, laid out in the same sections as the official form:

| Section | Contents |
|---------|----------|
| **A — Taxable Supplies** | Sales grouped by rate: Service Tax 6%, Sales Tax 10%, Sales Tax 5%, and any other rate, each with its taxable value and tax |
| **B — Exempt Supplies** | Value of exempt sales (no tax charged) |
| **C — Zero-Rated Supplies** | Value of 0%-rated sales (e.g. exports) |
| **D — Total Output Tax** | Sum of the tax in Section A |
| **E — Input Tax Credit** | Tax paid on purchases in the period |
| **F — Adjustments** | Credit-note and debit-note adjustments, bad-debt relief |
| **G — Net Tax Payable / Refundable** | D − E ± F |

A banner at the top states the period and whether the result is **Tax Payable** or **Refundable**, in RM.

### How to generate and review it

1. Open **Tax → SST-02 Filing**. The screen **defaults to the current bimonthly period**; adjust **From** / **To** if you need a different one.
2. Click **Generate Report**.
3. Review each section against your own records:
   - Confirm **Section A** shows the expected taxable sales at each rate.
   - Confirm **Exempt** (B) and **Zero-Rated** (C) values — for a pharmaceutical wholesaler, exempt supplies are usually significant, so check they are landing here and not in Section A.
   - Check **Input Tax Credit** (E) matches purchases with tax paid.
   - Review **Adjustments** (F) for credit/debit notes.
   - Read the final **Net Tax Payable / Refundable** (G).
4. **Export CSV** to keep a working copy, or **Print** for your filing pack.
5. Use the figures to complete and submit the official SST-02 return to RMCD by the deadline.

> **Note.** MyERP prepares the return figures for you to review and submit; it is a preparation aid, not a direct submission channel to Customs. Always reconcile against the **Tax Summary** report before filing, and confirm your company's **SST registration number** is correct on the return.

---

## Permissions

Access to the Tax module is controlled by the **Tax Categories** permission (`MyERP.TaxCategories`), typically granted to the **Accountant** role. It has these levels:

| Permission | Allows |
|------------|--------|
| **Tax Categories** (view) | Open Tax Categories, Tax Summary, and SST-02 Filing; view categories, rules, and reports |
| **Tax Categories → Create** | Add tax categories, tax rules, and item tax templates |
| **Tax Categories → Edit** | Update existing tax categories and rules |
| **Tax Categories → Delete** | Delete tax categories, rules, and item tax templates |

Notes:

- All three Tax screens require at least the view-level **Tax Categories** permission.
- The **Tax Summary** and **SST-02 Filing** reports also read sales and purchase invoices, so the user needs access to the relevant company's data.
- Give day-to-day staff view access; reserve **Create / Edit / Delete** for the Accountant, since changing a rate or rule affects how tax is calculated on documents.
