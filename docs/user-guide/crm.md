# CRM

## Overview

The **CRM** (Customer Relationship Management) module is where your sales team keeps track of *prospects* — businesses that are not yet customers but might become one. For a pharmaceutical wholesaler, these prospects are the pharmacies, clinics, hospitals, and dispensaries you are trying to win as buying accounts.

Not every conversation with a prospect turns into a sale on the first day. A pharmacy chain might take weeks of follow-up calls, a product demo, and a formal quotation before it places its first order. The CRM module gives you a place to record every one of these prospects, remember who is chasing them, and see how close each one is to signing up — so that promising leads don't slip through the cracks and your team can focus its effort where it is most likely to pay off.

CRM is the **pre-sales** part of the system. It sits *in front of* the Sales module and feeds it:

```
   Lead  ──►  Opportunity  ──►  Quotation / Customer
 (a contact)   (a live deal)      (an actual order & account)
```

- A **Lead** is a raw contact — a person and/or company you have identified as a possible customer.
- An **Opportunity** is a lead that has become a real, active deal you are actively pursuing, with an estimated value and expected close date.
- Once you win the deal, the prospect becomes a **Customer** with a real **Quotation** / order in the **Sales** module.

You reach the module from the main menu under **CRM**, which contains two screens: **Leads** and **Opportunities**.

---

## Leads

### What it is

A **Lead** is a potential customer you have made contact with (or been referred to) but who has not yet committed to buying anything. Each lead records the contact person, their company, how you found them, and where they are in your follow-up process.

Every lead is given an automatic reference number in the form **`LEAD-20260722-A1B2C3`** (the date it was created plus a short unique code) so you can quote it in emails and phone notes.

### Why it matters

Leads are the top of your sales funnel. A wholesaler that captures every enquiry — a pharmacy that called about pricing, a hospital procurement officer met at a trade show, a referral from an existing customer — never loses track of who to follow up with. Recording the **source** (Website, Referral, Campaign, Cold Call, Advertisement, Social Media, Trade Show, Partner, Other) also tells you *which channels actually bring in business*, so you can invest in the ones that work.

### Lead statuses

A lead moves through a lifecycle. Its current stage is shown as a coloured status badge:

| Status | Meaning |
|---|---|
| **New** | Just created; no follow-up yet. |
| **Open** | Follow-up has started. |
| **Replied** | The contact has responded to your outreach. |
| **Interested** | The contact has shown genuine interest. |
| **Qualified** | Confirmed as a real, worthwhile prospect — ready to become an opportunity. |
| **Converted** | Turned into an opportunity (or customer). The lead's work is done. |
| **Lost** | Not going anywhere — the prospect declined or went cold. |
| **Do Not Contact** | Asked not to be contacted again. |

### How to work with leads

**Create a lead**

1. Go to **CRM → Leads**.
2. Click **New Lead**.
3. Fill in the contact's details. Only the **first name** is required; the rest — last name, company name, email, phone, mobile, job title, website, source, city/state/country, industry, and estimated annual revenue — are optional but well worth capturing.
4. Optionally add **Notes** and assign the lead to a team member.
5. Save. The lead starts life with the status **New**.

**Qualify a lead**

Once you have spoken to the contact and confirmed they are a genuine prospect:

1. Open the lead from the list.
2. Click **Qualify**.
3. The status moves to **Qualified**, marking it as ready to be turned into a real deal.

(You can qualify a lead that is New, Open, Interested, or Replied.)

**Convert a lead to an opportunity**

When a qualified lead is ready to be pursued as an active deal:

1. Open the lead.
2. Click **Convert to Opportunity**.
3. The system creates a new **Opportunity**, copying across the contact's name, email, phone, assigned team member, and region, and links it back to this lead.
4. The lead's status changes to **Converted**, and a banner appears on the lead with a link to **View** the new opportunity.

From here on, you manage the deal on the Opportunity screen (see below).

**Mark a lead as lost**

If a prospect declines or goes cold, open the lead and click **Mark Lost**. You'll be asked to confirm. A lead that has already been converted cannot be marked lost.

> Note: Leads and opportunities are separate records. Converting a lead does **not** delete it — it stays on file (as *Converted*) so you keep a full history of where each deal came from.

---

## Opportunities

### What it is

An **Opportunity** is an active sales deal — a prospect you are seriously working to close. Unlike a lead, an opportunity carries a **money value**, an **expected closing date**, a **probability** of winning, and a **sales stage**, so you can forecast revenue and prioritise the deals most likely to land.

Each opportunity gets an automatic reference number like **`OPP-20260722-A1B2C3`**. Amounts default to Malaysian Ringgit (**MYR**).

Opportunities can also list the **products** (line items) the prospect is interested in — each with a description, quantity, unit price, and unit of measure. The system adds these up to give the total opportunity value.

### Why it matters

The list of open opportunities *is* your sales pipeline. The Opportunities screen shows two headline figures at the top:

- **Total Opportunities** — how many live deals you have.
- **Pipeline Value (MYR)** — the combined value of every opportunity in the list.

Together these tell management how much business is in play and how healthy the sales funnel looks — essential for planning stock, cash flow, and staffing in a wholesale operation.

### The pipeline: stages and statuses

Every opportunity has a **Sales Stage** — a free-text label describing where the deal is (new opportunities start at **"Prospecting"**). Alongside the stage, each opportunity carries a **probability** (a percentage, starting at 20%) shown as a progress bar, giving a quick read on how likely the deal is to close.

Separately, the opportunity has an overall **status** that drives what you can do with it:

| Status | Meaning |
|---|---|
| **Open** | The deal is live and being worked. |
| **Replied** | The prospect has responded to your proposal. |
| **Quotation** | A formal quotation has been prepared/sent to the prospect. |
| **Converted** | The deal was **won** — it becomes a customer/order in Sales. |
| **Lost** | The deal was **lost** — the prospect chose not to proceed. |
| **Closed** | The opportunity was closed without being won (e.g. shelved). |

Opportunities can also be typed as **Sales**, **Support**, or **Maintenance**.

### How to work with opportunities

**Create an opportunity**

You can create an opportunity directly (without a lead) when you already know a deal is real:

1. Go to **CRM → Opportunities**.
2. Click **New Opportunity**.
3. Enter a **Title** (required), the type, contact details, expected value, currency, expected closing date, probability, and sales stage.
4. Optionally add product line items.
5. Save.

More often, an opportunity is created automatically by **converting a lead** (see the Leads section).

**Move it through the pipeline**

As the deal progresses, the opportunity's status is advanced through the stages below. The system enforces a sensible order — for example, you can only mark a quotation, convert, or declare a deal lost while it is still open.

- **Mark as Quotation** — record that a formal quotation has gone out to the prospect (the quotation itself is raised in the **Sales** module). Available while the opportunity is *Open* or *Replied*.
- **Convert (Won)** — mark the deal as won once the prospect commits. The status becomes **Converted**, signalling the prospect is now a **Customer** with an order to be handled in Sales. Available while the opportunity is *Open*, *Replied*, or in *Quotation*.
- **Declare Lost** — mark the deal as lost. You can record a **reason** (e.g. "price too high", "chose a competitor"), which is kept for later analysis. A converted (won) deal cannot be marked lost.
- **Close** — close an opportunity that is neither won nor lost (for example, put on hold). A converted deal cannot be closed.
- **Reopen** — bring a **Lost** or **Closed** opportunity back to life. Its status returns to **Open** and any recorded loss reason is cleared. (A deal that was already **Converted/won** cannot be reopened.)

**Edit an opportunity**

Open the opportunity and click **Edit** to update its details, contacts, sales stage, probability, or line items. Editing is intended for deals that are still Open or Replied.

**Search and browse**

Use the search box on the Opportunities list to find a deal by its title, opportunity number, or contact name. Click any opportunity number to open its detail page, which shows the status, sales stage, amount, probability bar, and product line items.

---

## Typical workflow

Here is how a single prospect — say, a new pharmacy chain — typically travels through the system:

1. **Capture the lead.** A buyer from *Guardian Pharmacy* enquires about supply. In **CRM → Leads**, you create a lead with their contact details and set the source to *Referral*. Status: **New**.
2. **Follow up and qualify.** After a few calls the buyer confirms real interest and budget. You open the lead and click **Qualify**. Status: **Qualified**.
3. **Convert to an opportunity.** You click **Convert to Opportunity**. The system creates *"Guardian Pharmacy - Opportunity"* and links it to the lead. The lead is now **Converted**; the opportunity is **Open**.
4. **Build the deal.** On the opportunity you add the products they want, set the expected value, closing date, and probability, and advance the sales stage as negotiations progress.
5. **Send a quotation.** You prepare a formal quotation in the **Sales** module and click **Mark as Quotation** on the opportunity. Status: **Quotation**.
6. **Win the deal.** Guardian Pharmacy accepts. You click **Convert (Won)**. Status: **Converted** — they are now a **Customer**, and their order is processed in **Sales**.

   *(If instead they walked away, you would **Declare Lost** with a reason. If the timing wasn't right, you might **Close** it and **Reopen** it later.)*

This chain — **Lead → Opportunity → Quotation → Customer** — means every sale can be traced back to where it started, and no prospect is forgotten along the way.

---

## Permissions

Access to CRM is controlled by permissions, which an administrator grants per role. A user only sees the buttons for the actions they are allowed to perform.

**Leads**

| Permission | Allows |
|---|---|
| **Leads** | View the Leads list and lead details. |
| **Leads · Create** | Add new leads. |
| **Leads · Edit** | Change lead details, and qualify or mark a lead as lost. |
| **Leads · Delete** | Delete leads. |
| **Leads · Convert** | Convert a lead into an opportunity. |

**Opportunities**

| Permission | Allows |
|---|---|
| **Opportunities** | View the Opportunities list and opportunity details. |
| **Opportunities · Create** | Add new opportunities. |
| **Opportunities · Edit** | Change opportunity details, and mark as quotation, declare lost, close, or reopen. |
| **Opportunities · Delete** | Delete opportunities. |
| **Opportunities · Convert** | Convert (win) an opportunity. |

If a menu item or button is missing, it usually means your role has not been granted the matching permission — ask your system administrator.
