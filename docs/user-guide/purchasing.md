# Purchasing

## Overview

The **Purchasing** module is where MyERP handles everything you buy — from the moment
someone realises "we're running low on this" all the way through to paying the supplier's
bill.

For a pharmaceutical wholesaler this matters a great deal. You buy stock in large volumes
from many suppliers, you need a paper trail for every purchase (regulators, auditors and
your own accountants all expect it), and you need to know exactly what was ordered, what
actually arrived, and what you were charged. The Purchasing module keeps all three of
these separate but linked, so you never pay for goods you didn't receive and never lose
track of an outstanding order.

Everything you do here lives under the **Purchasing** menu on the left-hand navigation.
The main entries are:

| Menu item | What it's for |
|-----------|---------------|
| Material Requests | "We need this" — an internal request to buy or move stock |
| Request for Quotations (RFQ) | Ask several suppliers to quote a price |
| Supplier Quotations | Record the prices suppliers quote back |
| Purchase Orders | The official order you send to a supplier |
| Purchase Receipts | Record goods physically arriving (stock in) |
| Purchase Invoices | Record the supplier's bill (money owed) |
| Subcontracting | Send raw materials out for a supplier to process |
| Subcontracting Inward Orders | Track jobs where you receive processed goods |
| Supplier Scorecards | Rate how well each supplier performs |
| Purchase Register | A report of all posted supplier bills |

### The Draft → Submit → Post lifecycle

Almost every document in Purchasing moves through the same stages. Understanding these
three words is the key to the whole module.

- **Draft** — a work-in-progress. You can freely edit or delete it. It affects nothing.
- **Submit** — you confirm the document is correct and "release" it. Once submitted it
  becomes official, is locked from casual editing, and can be acted upon (converted,
  received, billed).
- **Post** — the document's financial or stock effect is written into your records:
  - Submitting a **Purchase Receipt** posts the stock **into** your warehouse.
  - Posting a **Purchase Invoice** records the amount you **owe** the supplier in
    Accounts Payable.

If you make a mistake, most documents can be **Cancelled** (which reverses their effect),
and a cancelled document can often be **Amended** to create a corrected copy.

### How documents flow into one another (conversions)

You rarely re-type the same information twice. Instead you **convert** one document into
the next, and MyERP copies the lines across for you:

```
Material Request ──▶ Purchase Order ──▶ Purchase Receipt ──▶ Purchase Invoice
                                    └──▶ Purchase Invoice
```

---

## Material Requests

**What it is** — An internal request that says "we need these items." It is not yet an
order to a supplier; it is the trigger that starts the buying process. Each request has a
**Request Type**: *Purchase* (buy from a supplier), *Material Transfer* (move stock
between warehouses), *Material Issue*, or *Manufacture*.

**Why it exists / when to use it** — Use it when a warehouse or department notices stock is
low and wants Purchasing to act. It separates *who needs something* from *who buys it*, so
requests can be reviewed and combined before any money is committed. For buying stock,
choose the **Purchase** type.

**How to use it**
1. Go to **Purchasing > Material Requests** and click **New**.
2. Set the **Request Type** (choose *Purchase* if you intend to buy), the **Request Date**
   and, optionally, a **Required By Date**.
3. Add the items you need with quantities. Add notes if helpful.
4. **Save** — the request is now in **Draft**.
5. Open the request and click **Submit** to make it official.
6. Once submitted (and if it is a *Purchase* request), click **Convert to Purchase Order**
   to turn it into an order. MyERP carries the items across into a new draft Purchase
   Order.
   - For a *Material Transfer* request you'll instead see **Create Stock Entry**.
7. If a request is no longer needed, open it and click **Cancel**.

**Tips**
- Only a **Submitted** Purchase-type request can be converted to a Purchase Order — a
  draft cannot.
- A request that has already been converted cannot be converted a second time, preventing
  accidental duplicate orders.

---

## Request for Quotation (RFQ)

**What it is** — A document that asks one or more suppliers to quote a price for a list of
items. It records *what* you want quoted and *which suppliers* you're asking.

**Why it exists / when to use it** — Use it before committing to a supplier when you want
to compare prices — for example a bulk purchase where several distributors might supply
the same drug. It gives you a documented, fair basis for choosing.

**How to use it**
1. Go to **Purchasing > Request for Quotations** and click **New**.
2. Enter the **Transaction Date** and an optional **Message for Suppliers** (e.g. delivery
   requirements or a response deadline).
3. Add the **Items** you want quoted, with quantities and units.
4. Add the **Suppliers** you want to invite (name and email).
5. **Save** the draft, then **Submit** to issue it.
6. On the RFQ detail screen you can see each invited supplier and their **Quote Status**
   (whether they have responded yet).

**Tips**
- Send the same RFQ to several suppliers so you can compare their **Supplier Quotations**
  side by side.
- If an RFQ is withdrawn, open it and click **Cancel**.

---

## Supplier Quotations

**What it is** — A record of the price and terms a supplier has quoted back to you, in
response to an RFQ or on their own.

**Why it exists / when to use it** — Use it to capture each supplier's offer so you have an
apples-to-apples comparison before raising a Purchase Order. It stores the quoted
**Rate** per item, the **Valid Till** date, and any exchange rate for foreign-currency
quotes.

**How to use it**
1. Go to **Purchasing > Supplier Quotations** and click **New**.
2. Select the supplier and enter the **Quotation Number** (their reference), the
   **Transaction Date** and, if relevant, a **Valid Till** date.
3. Add the quoted items with their **Rate** — the **Amount** is calculated for you
   (Qty × Rate).
4. **Save** the draft, then **Submit** to lock the quotation in.
5. Compare the submitted quotations and raise a **Purchase Order** to the supplier you
   choose.

**Tips**
- Watch the **Valid Till** date — prices for pharmaceutical goods can move, and an expired
  quote may no longer be honoured.
- Cancel a quotation you no longer need with the **Cancel** button.

---

## Purchase Orders

**What it is** — The official order you send to a supplier, listing the items, quantities,
prices and delivery expectations. It is the commitment to buy.

**Why it exists / when to use it** — This is the heart of Purchasing. A Purchase Order (PO)
tells the supplier exactly what you want and becomes the reference against which you check
deliveries (Receipts) and bills (Invoices). Never receive or pay for goods without a PO to
match them to.

**How to use it**
1. Reach a Purchase Order in one of two ways:
   - **Purchasing > Purchase Orders > New**, or
   - Convert a submitted **Material Request** (see above).
2. Select the **Supplier**, set the **Order Date**, and add the item lines with
   quantities and rates.
3. **Save** — the PO is in **Draft**. You can still edit or delete it at this stage.
4. Open the PO and click **Submit**. It becomes official and the delivery/billing tracking
   begins.
5. From the submitted PO's action menu you can now:
   - **Make Receipt** — creates a draft Purchase Receipt to record goods arriving.
   - **Make Invoice** — creates a draft Purchase Invoice to record the supplier's bill.
6. Other actions on a submitted PO:
   - **Close** — stop expecting any further deliveries/bills against it.
   - **Reopen** — re-activate a closed PO.
   - **Cancel** — void the order (only if nothing has been received or billed against it).
   - **Amend** — once cancelled, create a corrected copy.

**Tips**
- A PO must be **Submitted** before it can be converted into a Receipt or an Invoice.
- As goods are received and bills recorded, the PO's status updates automatically to show
  how much is still **to deliver and bill**, so you always know what's outstanding.
- You can only edit or delete a PO while it is still a **Draft**.

---

## Purchase Receipts

**What it is** — The document that records goods physically arriving at your warehouse. It
is the "stock in" event.

**Why it exists / when to use it** — Use it the moment a delivery arrives, to confirm what
actually turned up (which is not always exactly what was ordered). Submitting a receipt is
what increases your inventory in MyERP — this is why it is usually done by warehouse staff
at the goods-in door.

**How to use it**
1. Reach a Purchase Receipt by either:
   - Opening the relevant **Purchase Order** and clicking **Make Receipt**, or
   - **Purchasing > Purchase Receipts > New**.
2. Confirm the **Posting Date** and check each line's received quantity against what was
   ordered.
3. **Save** the draft. You can still edit it while it is a **Draft**.
4. Click **Submit**. At this point MyERP **posts the stock into the warehouse** — your
   inventory goes up.
5. If a delivery was recorded in error, open the receipt and click **Cancel**. This
   reverses the stock movement (takes the items back out).

**Tips**
- You cannot receive against a **Cancelled** or **Closed** Purchase Order.
- The system guards against **over-receipt** — receiving more than was ordered — and
  against dates that fall in a closed accounting period.
- A submitted receipt can be converted straight into a **Purchase Invoice** (see below).
- You cannot cancel a receipt if a supplier invoice has already been raised against it —
  cancel the invoice first.

---

## Purchase Invoices (Supplier Bills)

**What it is** — A record of the bill your supplier sends you — the amount you owe and when
it's due. Also called a "bill" or "purchase bill."

**Why it exists / when to use it** — Use it to capture the supplier's invoice so the amount
owed appears in **Accounts Payable** and can be paid. It also feeds the Purchase Register
report and, where required, Malaysian e-Invoice reporting.

**How to use it**
1. Reach a Purchase Invoice by either:
   - Opening a submitted **Purchase Order** and clicking **Make Invoice**, or
   - Opening a submitted **Purchase Receipt** and converting it to an invoice, or
   - **Purchasing > Purchase Invoices > New**.
2. Enter the **Supplier Invoice No** (their reference), the **Issue Date** and the
   **Due Date**. Check the item lines, unit prices and tax.
3. **Save** — the invoice is in **Draft** (editable/deletable while draft).
4. Click **Submit** to confirm the figures. This links the amounts back to the Purchase
   Order and updates how much of the PO has been billed.
5. Click **Post**. This records the amount owed in **Accounts Payable** — the bill is now
   part of your financial books.
6. Once posted you can:
   - **Make Payment** — record paying the supplier (in whole or in part).
   - **Write Off** — clear a small remaining balance you won't pursue.
   - **Cancel** — reverse the invoice (only if no payment has been made against it).
   - **Amend** — after cancelling, create a corrected copy.

**Tips**
- The detail screen shows a **Payment Schedule** plus running totals of **Paid** and
  **Outstanding**, so you can see at a glance what's still owed.
- You cannot cancel an invoice that already has payments recorded — reverse the payments
  first.
- Only **Posted** invoices appear in the **Purchase Register** and count towards Accounts
  Payable.

---

## Subcontracting (Outward)

**What it is** — A way to manage jobs where you send your own raw materials out to a
supplier (a "subcontractor") who processes them and returns finished goods — for example
sending bulk product out for repackaging or labelling.

**Why it exists / when to use it** — Use it when a third party does work on materials you
own. It tracks both the **finished items** you expect back and the **supplied items** (the
raw materials you hand over), so nothing goes missing.

**How to use it**
1. Go to **Purchasing > Subcontracting** and create a new **Subcontracting Order**.
2. Choose the **Supplier** (the subcontractor) and set the **Order Date**.
3. Add the **Items** you expect back and the **Supplied Items** (materials you are
   providing).
4. **Save**, then **Submit** the order.
5. When the processed goods come back, create a **Subcontracting Receipt** against the
   order and **Submit** it — this posts the received stock into your warehouse.
6. Use **Cancel** on the order or the receipt to reverse them if needed (cancelling a
   submitted receipt reverses the stock movement).

**Tips**
- Keep the supplied-items list accurate — it is your record of the materials you handed
  over and expect to be accounted for.

---

## Subcontracting Inward Orders

**What it is** — A record for tracking subcontracting jobs from the inward side: what you
are due to **receive** and be **billed** for, with running received and billed statuses.

**Why it exists / when to use it** — Use it to keep visibility of subcontracting jobs in
progress — what has come in, and what has yet to be invoiced — so nothing is received
without being billed or vice versa.

**How to use it**
1. Go to **Purchasing > Subcontracting Inward Orders** and create a new order.
2. Select the **Supplier**, set the **Order Date** and add the item lines.
3. **Save**, then **Submit** the order.
4. As goods arrive and bills are recorded, the order's **Received** and **Billed** statuses
   update automatically.
5. When the job is fully complete, **Close** the order. Use **Cancel** to void it.

**Tips**
- Closing an order signals it is finished and stops it appearing as outstanding work.

---

## Supplier Scorecards

**What it is** — A rating card that tracks how well each supplier performs against
criteria you define (for example delivery timeliness, quality, pricing), producing an
overall **Score** and a **Standing** (e.g. good / average / poor).

**Why it exists / when to use it** — Use it to make supplier choice evidence-based. For a
wholesaler juggling many distributors, a scorecard highlights who consistently delivers on
time and in full — and who to avoid. Scores start at 100 and move as you evaluate each
period.

**How to use it**
1. Go to **Purchasing > Supplier Scorecards** and click **New**.
2. Choose the supplier and the **Period Type** (e.g. *Monthly*).
3. Define the **Standings** (the grade bands, each with a min/max grade) and the
   **Criteria** (each with a weight and a maximum score).
4. **Save** the scorecard.
5. At the end of each period, open the scorecard and **submit a period evaluation** with
   the period's score. The supplier's **Current Standing** updates based on which band the
   score falls into.

**Tips**
- Weight the criteria that matter most to your business (e.g. on-time delivery for
  temperature-sensitive stock) so the overall score reflects real priorities.

---

## Purchase Register

**What it is** — A report listing all **posted** supplier invoices over a date range, with
totals.

**Why it exists / when to use it** — Use it to review your buying and your outstanding
payables for a period — for month-end, supplier reconciliation, or handing figures to your
accountant.

**How to use it**
1. Go to **Purchasing > Purchase Register**.
2. Choose the **Company**, a **From Date** and a **To Date**.
3. Run the report. Each line shows the **Invoice Number**, **Date**, **Net Total**,
   **Tax**, **Grand Total**, **Paid** and **Outstanding**, with grand totals at the bottom.

**Tips**
- Only **Posted** purchase invoices appear here — draft or unposted bills are excluded, so
  the figures always match your accounts.
- The **Outstanding** column is a quick way to see how much you still owe suppliers in the
  period.

---

## Typical workflow

Here is how a normal purchase flows through the module from start to finish:

1. **Need** — the warehouse notices stock is low and raises a **Material Request**
   (type *Purchase*), then **Submits** it.
2. **Compare prices (optional)** — issue a **Request for Quotation** to several suppliers
   and record their **Supplier Quotations** to pick the best offer.
3. **Order** — **Convert to Purchase Order** (or create one directly), then **Submit** the
   PO to send it to the supplier.
4. **Receive** — when the goods arrive, use **Make Receipt** on the PO, check the
   quantities, and **Submit** the **Purchase Receipt**. Stock goes **in**.
5. **Bill** — record the supplier's bill with **Make Invoice**, **Submit** it, then
   **Post** it. The amount owed hits **Accounts Payable**.
6. **Pay** — use **Make Payment** on the posted invoice to settle it. Track what's left in
   the **Purchase Register** report.

```
Material Request → (RFQ / Quotation) → Purchase Order → Purchase Receipt → Purchase Invoice → Payment
   Submit                                  Submit           Submit           Submit → Post      Pay
```

---

## Permissions

Access to Purchasing is controlled by role-based permissions, so staff only see and do
what their job requires. Typical arrangements:

| Task | Permission | Typical role |
|------|-----------|--------------|
| Raise / submit / cancel Material Requests | `MyERP.MaterialRequests` (+ Create / Submit / Cancel) | Warehouse, Purchasing Officer |
| Create RFQs, Supplier Quotations, Purchase Orders | `MyERP.PurchaseOrders` (+ Create / Edit / Submit / Cancel) | Purchasing Officer |
| Submit / cancel Purchase Receipts (stock in) | `MyERP.PurchaseReceipts` (+ Submit / Cancel / Edit) | Warehouse |
| Create / submit / post / cancel Purchase Invoices | `MyERP.PurchaseInvoices` (+ Create / Edit / Submit / Cancel) | Accounts / Finance |
| Manage Subcontracting and Inward Orders | `MyERP.PurchaseOrders` | Purchasing Officer |
| Manage Supplier Scorecards | `MyERP.SupplierScorecards` (+ Create) | Purchasing Manager |
| View the Purchase Register report | `MyERP.PurchaseInvoices` | Accounts / Finance |

Notes:
- Sensitive actions have their own sub-permissions. For example, a user may be allowed to
  **create** a Purchase Order but not **Submit** or **Cancel** it — those are separate
  rights an administrator grants.
- Because submitting a **Purchase Receipt** changes physical stock and posting a
  **Purchase Invoice** changes the financial books, those rights are usually kept with
  warehouse and finance staff respectively, not everyone in Purchasing.
- If a menu item or button is greyed out or missing, you most likely don't have the
  matching permission — ask your system administrator.
