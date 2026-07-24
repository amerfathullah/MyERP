# Sales

## Overview

The **Sales** module is where your business turns a customer enquiry into money in the bank. It covers the full selling cycle for a pharmaceutical wholesaler: quoting prices to pharmacies and clinics, taking their orders, shipping the goods out of the warehouse, invoicing them, collecting payment, and chasing overdue accounts. It also includes an over-the-counter **Point of Sale** till, tools for recurring and long-term supply agreements, customer loyalty and commission schemes, and a set of reports so managers can see what is selling and how profitable it is.

Everything in Sales is reached from the **Sales** menu in the left-hand navigation.

### The document lifecycle (read this first)

Most sales documents move through the same three stages. Understanding these stages makes every screen below easier to use:

| Stage | Button | What happens |
| --- | --- | --- |
| **Draft** | (saved automatically) | A working copy you can freely edit or delete. Nothing has happened in the business yet. |
| **Submitted** | **Submit** | The document is validated and locked. It can no longer be casually edited, and it becomes available to convert into the next document in the chain. |
| **Posted** | **Post** | The document's financial and stock effects are written to the books: the **stock ledger** (inventory levels) and/or the **general ledger** (accounts). This is the point of no return — to undo it you must **Cancel** and **Amend**. |

Quotations and Sales Orders finish at **Submitted** (they don't touch the ledgers themselves). Sales Invoices and POS sales go all the way to **Posted**, because that is when the sale becomes real money and real stock movement.

**Conversions** let you carry information forward without re-typing it: a **Quotation** becomes a **Sales Order**, and a Sales Order becomes a **Delivery Note** and/or a **Sales Invoice**.

Access is controlled by **permissions** tied to each user's role — for example a Sales Representative may create and submit documents, while only a Sales Manager can cancel or delete them. See [Permissions](#permissions) at the end.

---

## Quotations

**What it is.** A price offer you send to a customer before they commit to buying.

**Why it exists / when to use it.** Wholesale customers usually ask "how much?" before ordering. A quotation records the items, quantities and prices you offered, with an expiry date, so there is no argument later about what was agreed.

**How to use it**

1. Go to **Sales → Quotations** and click **New**.
2. Choose the **Company**, set the **Quotation Date** and, optionally, a **Valid Until** date (the offer's expiry).
3. Select the **Customer**.
4. Add item lines — each line has a **Description**, **Quantity**, **Unit Price** and **Tax Amount**; the **Line Total** is calculated for you.
5. Save. The quotation is now in **Draft** — edit it as much as you like.
6. When the offer is final, open it and click **Submit**.
7. Once submitted you can:
   - **Convert to SO** — creates a Sales Order from the quotation and takes you straight to it.
   - **Mark Lost** — record that the customer went elsewhere.
   - **Cancel** — withdraw the offer.
8. A cancelled or rejected quotation can be re-opened for another attempt with **Amend**, which creates a fresh editable copy.

**Tips.** Marking quotations as **Lost** rather than deleting them keeps your win/loss history intact for later analysis.

---

## Sales Orders

**What it is.** A confirmed commitment from the customer to buy — the central document that everything else (delivery, invoice, payment) hangs off.

**Why it exists / when to use it.** Once a customer says "yes", raise a Sales Order. It reserves what you owe them and drives both the warehouse (what to ship) and accounts (what to bill).

**How to use it**

1. Go to **Sales → Sales Orders → New** (or reach it automatically by converting a Quotation).
2. Set the **Company**, **Order Date** and the required **Delivery Date**.
3. Choose the **Customer** and the **Warehouse** the goods will ship from.
4. Add item lines (Description, Quantity, Unit Price).
5. Save as **Draft**, then click **Submit** to confirm the order.
6. After submitting, the order shows a status such as **To Deliver and Bill**, **To Deliver** or **To Bill**, and offers these actions:
   - **Create Delivery Note** — ship the goods.
   - **Create Invoice** — bill the customer.
   - **Make Payment** — record money received against the order.
   - **Make Work Order** — hand off to Manufacturing if the items must be produced.
   - **Close** / **Reopen** — stop or resume further activity on the order.
   - **Cancel** — void the order.
7. A cancelled order can be revised with **Amend**.

**Tips.** You don't have to deliver and invoice in a fixed order — the status tells you what is still outstanding. Draft orders (only) can be deleted outright.

---

## Delivery Notes

**What it is.** The document that records goods physically leaving your warehouse to the customer.

**Why it exists / when to use it.** It is your proof of shipment and the trigger that reduces stock. For a wholesaler moving large volumes, the delivery note is what the driver and the customer's goods-in desk sign against.

**How to use it**

1. Usually created via **Create Delivery Note** on a submitted Sales Order (fields are copied across). You can also start one at **Sales → Delivery Notes → New**.
2. Check the items and quantities being shipped.
3. Save as **Draft**, then **Submit** to confirm the shipment.
4. From a submitted delivery note you can:
   - **Make Invoice** — bill for exactly what was delivered.
   - **Cancel** — reverse the shipment.
5. Use **Amend** to correct a cancelled note.

**Tips.** Delivering first and invoicing from the delivery note is the cleanest way to make sure you only bill for what actually went out the door.

---

## Sales Invoices

**What it is.** The bill you issue to the customer, and the document that books the sale into your accounts.

**Why it exists / when to use it.** This is where the sale becomes revenue and a receivable. It can also reduce stock directly (handy when you invoice without a separate delivery note).

**How to use it**

1. Reach it by converting a Sales Order or Delivery Note, or start fresh at **Sales → Sales Invoices → New**.
2. Set the **Company**, **Customer**, **Issue Date** and **Due Date**.
3. Add item lines (Item, Description, Quantity, Unit Price).
4. Optionally tick **Update Stock** — do this when the invoice itself should reduce inventory (i.e. there was no separate Delivery Note). Choose the warehouse to draw from.
5. Save as **Draft**, then click **Submit** to validate and lock it. Submitting checks the customer's **credit limit** and warns if it is exceeded.
6. Click **Post** to book it. Posting writes the sale to the **general ledger**, and — if **Update Stock** was ticked — reduces inventory in the **stock ledger**.
7. Once **Posted**, you can:
   - **Make Payment** — record the customer's payment against the invoice.
   - **Create Return** (Credit Note) — reverse all or part of the sale; this reduces the original invoice's outstanding amount.
   - **Write Off** — clear a small remaining balance you won't collect (shown only when an amount is still outstanding).
   - **Cancel** — reverse the invoice, including any stock movement it made.
   - **Submit to LHDN** — send the invoice as a Malaysian e-Invoice to the tax authority (available when it hasn't been submitted yet).
8. A cancelled invoice can be revised with **Amend**. You can also **Duplicate** an invoice or **Print** it.

**Tips.**
- A **payment schedule** is shown on the invoice when payment terms split the total into instalments with due dates.
- Only draft invoices can be deleted; posted ones must be cancelled.

---

## POS (Point of Sale)

**What it is.** A fast, till-style screen for over-the-counter cash sales.

**Why it exists / when to use it.** For walk-in or counter sales you don't want the full multi-step invoice workflow. POS rings up a sale in seconds and handles the stock and accounting behind the scenes.

**How to use it**

1. Go to **Sales → POS**.
2. Search for products by **name, code or barcode** and tap them to add to the cart.
3. Adjust quantities in the cart, or remove a line.
4. Enter the **Amount Received**; the screen shows the **Change** to give.
5. Click to complete the sale.

Behind the scenes the sale is turned into a **Sales Invoice that is already Posted**, stock is **always** deducted from the warehouse, and you get an invoice number and the change to hand back.

**Tips.** POS always reduces stock, so make sure the till is pointed at the correct warehouse. Because each sale posts immediately, there is no draft to edit — mistakes are handled with a return/credit note on the invoice.

---

## POS Closing

**What it is.** The end-of-shift reconciliation for a cashier's till.

**Why it exists / when to use it.** At the end of a shift you need to prove the cash drawer matches the sales rung up. POS Closing gathers all the shift's POS invoices, compares expected against counted amounts per payment mode, and flags any difference.

**How to use it**

1. Go to **Sales → POS Closing** and create a closing entry for the shift.
2. The entry links the shift's **POS invoices** and lists each **payment mode** with its **Expected Amount** and the **Closing Amount** you actually counted.
3. Review the **Difference** (variance) per payment mode and the total.
4. **Submit** the closing entry to finalise it. On submission the shift's sales are consolidated into a single Sales Invoice for the accounts. Use **Cancel** to void a closing entry.

**Tips.** Investigate any variance before submitting — once submitted the figures are locked.

---

## Pricing Rules

**What it is.** Automatic discounts or special prices that apply when certain conditions are met.

**Why it exists / when to use it.** Wholesalers run promotions and volume deals — "10% off this product line", "special price above 100 units", "buy X get a free item". Pricing Rules apply these consistently so staff don't have to remember or key them in manually.

**How to use it**

1. Go to **Sales → Pricing Rules → New**.
2. Give the rule a **Title** and set what it **Applies On** (a specific item or an item group).
3. Choose the **Rule Type** — a discount percentage, a discount amount, or a fixed rate.
4. Set the conditions: **Min/Max Quantity**, **Min/Max Amount**, and the **Valid From** / **Valid Upto** dates.
5. Set a **Priority**. When several rules could apply, the highest priority wins.
6. Save. Enabled rules are then applied automatically to matching sales lines.

**Tips.** If two rules share the same priority and both match a line, the system reports an ambiguity error rather than guessing — give competing rules different priorities to avoid this.

---

## Blanket Orders

**What it is.** A long-term agreement to supply an agreed quantity of items over a period, drawn down by many smaller orders.

**Why it exists / when to use it.** A hospital might commit to buying, say, 10,000 units of a product across a year at a fixed rate. A Blanket Order captures that commitment and tracks how much has been ordered against it and how much remains.

**How to use it**

1. Go to **Sales → Blanket Orders → New**.
2. Set the **Company**, the **Customer** (party), and the agreement window (**From Date** / **To Date**).
3. Add item lines with the agreed **Quantity** and **Rate**.
4. Save as **Draft**, then **Submit** to activate the agreement. Use **Cancel** to end it.
5. As real Sales Orders are placed against it, each item shows **Ordered Quantity** and **Remaining Quantity**.

**Tips.** Blanket Orders fix the price for the period — useful for locking in tender rates.

---

## Subscriptions

**What it is.** A recurring billing arrangement that generates invoices on a schedule.

**Why it exists / when to use it.** For customers on standing monthly supply or service contracts, Subscriptions save you raising the same invoice by hand every period.

**How to use it**

1. Go to **Sales → Subscriptions → New**.
2. Choose the **Customer**, the **Billing Interval** (e.g. Monthly) and how many intervals per cycle.
3. Set the **Start Date**, an optional **End Date**, and any **Trial Period** in days.
4. Add plan lines — the **Item**, **Quantity** and **Rate** billed each period.
5. Save. Each period you can **Generate Invoice** to create that period's Sales Invoice (trial periods and part-periods are prorated automatically), and the subscription **advances** to the next period.
6. Use **Cancel** to stop the subscription.

**Tips.** A subscription must be **Active** and have at least one plan line before it will generate an invoice.

---

## Loyalty Programs

**What it is.** A points scheme that rewards customers for their spending.

**Why it exists / when to use it.** To encourage repeat business, customers earn points as they buy and redeem them for value against future purchases. Tiers let bigger spenders earn faster.

**How to use it**

1. Go to **Sales → Loyalty Programs → New**.
2. Set the **Name**, the **Conversion Factor** (how spending converts to points/value) and the **Expiry Duration** (how long points last, in days).
3. Add **Tiers** — each with a **Tier Name**, the **Minimum Spent** to reach it, and its **Collection** and **Redemption** factors.
4. Save. You can later view a customer's **points balance**, **current tier** and **redemption value**, see their **point history**, and **Redeem Points** on their behalf.

**Tips.** Points expire after the duration you set, so redemption value changes over time.

---

## Shipping Rules

**What it is.** A rulebook for automatically calculating delivery charges.

**Why it exists / when to use it.** Freight cost often depends on order value or destination. Shipping Rules work out the charge for you instead of staff guessing.

**How to use it**

1. Go to **Sales → Shipping Rules → New**.
2. Give it a **Label** and choose the **Calculation Mode** and **Rule Type** (e.g. a fixed amount, or banded by value).
3. For banded shipping, add **Conditions** — each a **From Value**, **To Value** and the **Shipping Amount** for that band.
4. Optionally restrict the rule to specific **Countries**.
5. Save and enable it. The system can then calculate the shipping charge for a given order value and destination.

**Tips.** You can toggle a rule on or off without deleting it.

---

## Sales Persons

**What it is.** A register of your sales staff, arranged as a team hierarchy, with commission rates and targets.

**Why it exists / when to use it.** To attribute sales to the person or team responsible, pay commission, and track performance against targets.

**How to use it**

1. Go to **Sales → Sales Persons → New**.
2. Enter the **Name**, optionally link an **Employee**, and set the **Commission Rate**.
3. To build a team tree, mark a record as a **Group** and set a **Parent** sales person for individuals under it.
4. Add **Targets** per fiscal year (a target quantity and/or amount).
5. A sales person who leaves can be **Disabled** so they can't be assigned to new transactions.

**Tips.** The hierarchy lets managers roll up their team members' figures.

---

## Dunning

**What it is.** A formal overdue-payment reminder, optionally adding a fee or interest.

**Why it exists / when to use it.** When invoices go unpaid past their due date, Dunning produces an escalating reminder to the customer so you can chase collection systematically.

**How to use it**

1. Go to **Sales → Dunnings → New**.
2. Select the **Customer** and set the **Dunning Level** (escalation stage, e.g. 1, 2, 3).
3. Add the **overdue invoices** — each with its outstanding amount, due date and days overdue.
4. Optionally add a **Dunning Fee** and **Interest Amount**; the **Grand Total** is the amount now claimed.
5. Save as **Draft**, then **Submit** to issue it.
6. When the customer pays, mark the dunning **Resolved**.

**Tips.** Raise the dunning level each time you re-chase the same debt to keep an audit trail of escalation.

---

## Installation Notes

**What it is.** A record that equipment or products delivered to a customer have been installed or commissioned.

**Why it exists / when to use it.** Where you supply items that must be set up on site, an Installation Note (linked to the Delivery Note) proves installation happened and captures serial numbers.

**How to use it**

1. Go to **Sales → Installation Notes → New**.
2. Select the **Customer** and the related **Delivery Note**, and set the **Installation Date** (which cannot be earlier than the delivery date).
3. Add the items installed, each with its **Quantity** and **Serial No** where relevant.
4. Save as **Draft**, then **Submit**. Use **Cancel** to void it.

**Tips.** Installation Notes are governed by the same permissions as Delivery Notes.

---

## Sales Reports

Reports are read-only summaries built from **posted** invoices over a date range you choose. They live under **Sales → Reports**.

| Report | Menu path | What it shows |
| --- | --- | --- |
| **Gross Profit** | Sales → Reports → Gross Profit | Revenue, cost, gross profit and gross-profit % — overall and per invoice. Shows how much you actually made, not just sold. |
| **Sales Register** | Sales → Reports → Register | A line per posted invoice with net, tax, grand total, amount paid and outstanding — plus totals. Your book of sales. |
| **Item Sales** | Sales → Reports → Item Sales | Per-item totals: quantity sold, revenue and average rate. Reveals your best- and worst-moving products. |
| **Customer Revenue** | Sales → Reports → Customer Revenue | Revenue grouped by customer, with total paid and outstanding. Shows who your biggest (and slowest-paying) customers are. |

**How to use them**

1. Open the report from the menu.
2. Pick the **Company** and the **From** / **To** dates (sensible defaults are applied if you leave them blank).
3. Read the results on screen.

**Tips.** All four reports exclude returns/credit notes and only count invoices that have been **Posted**, so drafts and un-posted sales won't appear — post your invoices to see them reflected here.

---

## Typical workflow

A day-to-day sale usually flows like this:

1. **Quote.** A pharmacy asks for pricing. You raise a **Quotation** (Sales → Quotations), submit it, and send it over. Any promotional **Pricing Rules** apply automatically.
2. **Order.** They accept, so you **Convert to SO** — a **Sales Order** confirming what they'll buy and when it's due.
3. **Deliver.** You **Create Delivery Note** from the order; the driver ships the goods and you **Submit** it, reducing stock.
4. **Invoice.** You **Make Invoice** from the delivery (or the order), **Submit** it (credit limit is checked), then **Post** it — booking the revenue, and for e-Invoicing you **Submit to LHDN**.
5. **Collect.** When the money arrives you **Make Payment** against the invoice. If it goes overdue, you raise a **Dunning** reminder; if a customer walks in for a quick counter sale instead, you use **POS** and reconcile the till with **POS Closing** at end of shift.
6. **Review.** Managers watch the **Sales Reports** to see profit, top items, and customer revenue, and track staff against targets in **Sales Persons**.

Standing arrangements short-circuit parts of this: **Blanket Orders** pre-agree volumes and price, and **Subscriptions** auto-generate the recurring invoices.

## Permissions

Every screen in this module is permission-controlled, and permissions are granted per role. In practice:

- A **Sales Representative** typically has **Create**, **Edit** and **Submit** rights on Quotations, Sales Orders, Delivery Notes and Sales Invoices — enough to run the day-to-day cycle.
- A **Sales Manager** additionally holds the **Cancel** and **Delete** rights, plus access to sensitive areas such as Pricing Rules, Loyalty Programs, Shipping Rules, Sales Persons and the reports.

The main permission groups behind the module are:

| Area | Permission group | Typical rights |
| --- | --- | --- |
| Quotations | `Quotations` | Create, Edit, Delete, Submit, Cancel |
| Sales Orders, Blanket Orders | `SalesOrders` | Create, Edit, Delete, Submit, Cancel |
| Sales Invoices, POS, POS Closing, Pricing Rules, Subscriptions, Dunning, Reports | `SalesInvoices` | Create, Edit, Delete, Submit, Cancel |
| Delivery Notes, Installation Notes | `DeliveryNotes` | Create, Edit, Delete, Submit, Cancel |
| Loyalty Programs | `LoyaltyPrograms` | Create, Edit, Delete |
| Shipping Rules | `ShippingRules` | Create, Edit, Delete |
| Sales Persons | `SalesPersons` | Create, Edit, Delete |

If a menu item or button is missing for you, it usually means your role doesn't hold the matching permission — ask your administrator.
