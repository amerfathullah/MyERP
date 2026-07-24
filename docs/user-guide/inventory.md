# Inventory

## Overview

The Inventory module is where MyERP keeps track of every physical product you buy, store, move, and sell — your medicines, medical devices, and consumables. For a pharmaceutical wholesaler, getting stock control right is not just about knowing "how many boxes do we have" — it is a compliance and patient-safety issue:

- **Batches and expiry** — Every pharmaceutical product moves in batches (lots). You must know which batch is in which warehouse, and when it expires, so you can sell oldest-first (FEFO — First Expired, First Out), quarantine expired stock, and trace a batch instantly if a supplier issues a recall.
- **Cold chain** — Vaccines, insulin, and other temperature-sensitive goods must be stored in dedicated refrigerated warehouses. In MyERP you model each fridge/cold room as its own **Warehouse**, so stock in a cold-chain location is always separated from ambient stock.
- **Serial numbers** — High-value or regulated items (e.g. certain devices) can be tracked individually by serial number for full traceability.
- **Accurate valuation** — The value of stock on your balance sheet must be correct. MyERP supports several valuation methods (FIFO, Weighted Average, LIFO, Standard Cost) and posts the accounting entries automatically.

### The golden rule: Draft → Submit → Post

Almost every stock document in MyERP moves through three stages. **Understanding this is the single most important thing in the whole module.**

| Stage | What it means | Does it move stock? |
|-------|---------------|---------------------|
| **Draft** | A work-in-progress you can freely edit, save, or delete. | No |
| **Submit** | You have finalised the document. It is now locked and validated. | No |
| **Post** | The system writes the **Stock Ledger Entries** and updates warehouse balances (and the accounting/GL entries). | **Yes — this is the step that actually changes your stock** |

If a document is only Drafted or Submitted, your on-hand quantities have **not** changed yet. Nothing is real until it is **Posted**.

> **Important:** A plain Stock Entry of type *Material Receipt* is designed for ongoing operational receipts and follows the configured accounting rules. To load your **very first / opening stock**, use a **Stock Reconciliation** instead — it self-balances through a difference (opening) account so your books stay balanced. See [How to load opening stock](#how-to-load-opening-stock).

### Where to find it

Everything below lives under the **Inventory** menu (the warehouse icon) in the left-hand navigation.

---

## Items

**Menu path:** Inventory → Items

### What it is
An **Item** is the master record for anything you stock or sell — a specific product like "Paracetamol 500mg Tablet, 100s". Every stock movement, sale, and purchase refers back to an Item.

### Why it exists / when to use it
You create an Item once, then reuse it everywhere. The Item record holds the settings that control how the product behaves throughout the system — its unit of measure, valuation method, reorder levels, and whether it needs batch/serial tracking or quality inspection.

### How to use it
1. Go to **Inventory → Items**.
2. Click **New**.
3. Fill in the key fields:
   - **Item Code** — a unique code (e.g. `PARA-500-100`).
   - **Item Name** — the human-readable name.
   - **Item Type** — *Goods* (physical, stock-tracked), *Service*, or *Fixed Asset*.
   - **Barcode** — optional, for scanning.
   - **Item Group / Brand** — for classification and reporting.
   - **UOM** (Unit of Measure) — e.g. Box, Strip, Unit.
   - **Valuation Method** — *FIFO*, *Weighted Average*, *LIFO*, or *Standard Cost* (see Tips).
   - **Standard Buying / Selling Price** — default prices.
4. Set stock behaviour:
   - **Maintain Stock** — tick for physical goods you want tracked (defaults on for *Goods*).
   - **Allow Negative Stock** — normally leave **off** for pharma, so you cannot sell what you do not physically have.
   - **Default Warehouse** — where this item is usually stored.
5. Set **reorder controls** (see Tips):
   - **Reorder Level**, **Reorder Qty**, **Safety Stock**, **Min Order Qty**.
6. Set **quality controls** (for regulated goods):
   - **Inspection Required Before Purchase** — forces a Quality Inspection on receipt.
   - **Inspection Required Before Delivery** — forces one before dispatch.
7. Click **Save**.

### Tips
- **Valuation method is sticky.** Once an item has stock movements, the system restricts changing its valuation method (for example, you cannot switch to or from *Standard Cost*, and you cannot move from *Weighted Average* to *FIFO*). Choose carefully up front. **FIFO is the natural fit for pharma** because it mirrors how you physically rotate stock by expiry.
- **Reorder Level** is the trigger point: when available stock drops to this number, MyERP flags the item for reordering (auto-reorder). **Safety Stock** is the buffer you never want to dip below. Set these based on lead time and demand for each product.
- Use **Inspection Required Before Purchase** on any product that must be QC-checked before it can be sold.

---

## Item Attributes

**Menu path:** Inventory → Item Attributes

### What it is
**Item Attributes** define the variation dimensions of a product — for example an attribute "Strength" with values 250mg / 500mg / 1g, or "Pack Size" with values 10s / 50s / 100s.

### Why it exists / when to use it
When you sell the same product in several variants, define the attribute once and reuse it. Items can then be set up as variants of a template item using these attributes, keeping your item list tidy and consistent.

### How to use it
1. Go to **Inventory → Item Attributes**.
2. Click **New**.
3. Enter the **Attribute Name** (e.g. "Pack Size").
4. Add the allowed **values** (e.g. 10s, 30s, 100s).
5. Click **Save**.

### Tips
- Define attributes before setting up variant items, so the values are available to pick from.

---

## Warehouses

**Menu path:** Inventory → Warehouses

### What it is
A **Warehouse** is any location where stock is physically held — a main store, a branch store, a quarantine area, or a cold room.

### Why it exists / when to use it
Stock balances are always tracked **per warehouse**. Modelling your real locations accurately means you always know exactly where each batch sits, and you can keep cold-chain, quarantine, and saleable stock separate.

### How to use it
1. Go to **Inventory → Warehouses**.
2. Click **New**.
3. Fill in:
   - **Name** (e.g. "Cold Room A — 2 to 8°C") and optional **Warehouse Code**.
   - **Address / City / State / Postal Code / Country**.
   - **Parent Warehouse** and **Is Group** — use these to build a tree (e.g. a "Main Store" group containing "Ambient" and "Cold Chain" sub-warehouses).
   - **Is Active**.
4. Click **Save**.

### Tips
- **Create dedicated cold-chain warehouses** (e.g. "Vaccine Fridge 1") so temperature-sensitive stock is never mixed with ambient stock. This makes cold-chain reporting and stock counts straightforward.
- Use a **Quarantine** warehouse for goods awaiting quality inspection or pending return, so they are excluded from saleable stock.
- Group warehouses (*Is Group* ticked) are for structure only — you post stock to the leaf (non-group) warehouses beneath them.

---

## Stock Entries

**Menu path:** Inventory → Stock Entries

### What it is
A **Stock Entry** is the day-to-day document that records stock movement in, out, or between warehouses.

### Why it exists / when to use it
Use a Stock Entry for operational movements. The **Purpose / Entry Type** you choose determines the direction of the movement and the accounting treatment:

| Entry Type | What it does | Typical use |
|------------|--------------|-------------|
| **Material Receipt** | Brings stock **in** to a target warehouse | Miscellaneous receipts, found stock |
| **Material Issue** | Takes stock **out** of a source warehouse | Consumption, samples, write-offs, expiry disposal |
| **Material Transfer** | Moves stock from a source to a target warehouse | Moving between stores; ambient → cold room |

(The system also supports manufacturing/subcontracting types such as Manufacture, Repack, Send/Receive at Warehouse, and Subcontracting flows, used by other modules.)

### How to use it
1. Go to **Inventory → Stock Entries** and click **New**.
2. Choose the **Entry Type / Purpose** (Material Receipt, Material Issue, or Material Transfer).
3. Set the **Posting Date**.
4. Add item rows. For each row set the **Item**, **Quantity**, and the relevant warehouse:
   - **Material Receipt** → set the **Target Warehouse**.
   - **Material Issue** → set the **Source Warehouse**.
   - **Material Transfer** → set **both** Source and Target warehouses.
5. For batch/serial-tracked items, select the **Batch** (and **Serial Numbers** where applicable).
6. Click **Save** — the document is now in **Draft**.
7. Review, then click **Submit** to finalise.
8. Click **Post** to write the stock ledger entries and actually move the stock. **Only now do your on-hand balances change.**

### Tips
- **Nothing moves until you Post.** If your Stock Balance report looks wrong, check whether the entry is still sitting in Draft or Submitted.
- When issuing or disposing stock, **pick the batch that is expiring soonest** (FEFO).
- To dispose of expired stock, use a **Material Issue** out of the quarantine/expired warehouse.
- Posting a Material Issue or Transfer automatically runs a **reorder check** on the source item — if you have dropped to the reorder level, MyERP flags it.
- Remember: **opening/first-load stock should go through a Stock Reconciliation, not a Material Receipt.**

---

## Stock Reconciliations

**Menu path:** Inventory → Stock Reconciliations

### What it is
A **Stock Reconciliation** sets the actual counted quantity (and optionally the value) of items in a warehouse to a specific figure, and books the difference automatically.

### Why it exists / when to use it
Use it for two main jobs:
1. **Loading opening stock** when you first go live (see the dedicated guide below).
2. **Adjusting stock after a physical count** — when what is on the shelf does not match what the system says.

Unlike a plain Material Receipt, a Stock Reconciliation **self-balances**: the gain or loss in stock value is posted to a **difference / opening account**, so your accounts always stay balanced.

### How to use it
1. Go to **Inventory → Stock Reconciliations** and click **New**.
2. Set the **Posting Date** and a **Purpose** note (e.g. "Opening stock" or "Cycle count — Cold Room A").
3. Set the **Expense / Difference Account** (and Cost Center) that absorbs the adjustment.
4. Add rows. For each: choose **Item**, **Warehouse**, the **New Quantity** (the counted figure), and the **New Valuation Rate** (cost per unit). For batch items, specify the **Batch** and its **expiry**.
5. The **Difference Amount** is calculated automatically.
6. **Save** (Draft) → **Submit**. Submitting validates the posting period and books the stock and accounting entries.

### Tips
- Enter a realistic **valuation rate** — this becomes the cost basis for that stock.
- For pharma, reconcile **batch by batch** with the correct expiry dates so FEFO and expiry reporting stay accurate.
- Use Stock Reconciliation for count corrections rather than editing stock directly — it keeps a full audit trail.

---

## Stock Reservations

**Menu path:** Inventory → Stock Reservations

### What it is
A **Stock Reservation** ring-fences a quantity of an item in a warehouse against a specific document (for example a sales order), so it cannot be sold twice.

### Why it exists / when to use it
When stock is promised to one customer, reserving it prevents another order from grabbing the same units. The reservation tracks **Reserved Qty** and **Delivered Qty**, and the remaining **Available Qty** is what is still held.

### How to use it
1. Go to **Inventory → Stock Reservations**.
2. Reservations are usually created against a source document (e.g. a sales order), specifying **Item**, **Warehouse**, **Reserved Qty**, and optionally the **Batch**.
3. As stock is delivered, the **Delivered Qty** rises and the reservation is consumed.

### Tips
- Reserve against a specific **batch** where a customer requires a particular expiry or lot.
- Cancel reservations that are no longer needed so the stock returns to the available pool.

---

## Item Standard Costs

**Menu path:** Inventory → Standard Costs

### What it is
An **Item Standard Cost** records a fixed standard (planned) cost per unit for an item, effective from a given date.

### Why it exists / when to use it
For items valued using the **Standard Cost** method, this is the cost the system uses. It also lets you measure **Purchase Price Variance (PPV)** — the gap between what you planned to pay and what you actually paid.

### How to use it
1. Go to **Inventory → Standard Costs** and click **New**.
2. Choose the **Item**.
3. Enter the **Standard Rate** and the **Effective Date**.
4. **Save**. The previous rate is retained for history.

### Tips
- Only relevant for items whose Valuation Method is **Standard Cost**.
- Update the standard rate when your true costs shift materially, so variance reporting stays meaningful.

---

## Repost Item Valuation

**Menu path:** Inventory → Repost Item Valuation

### What it is
A maintenance tool that **recalculates stock valuation** across historical transactions after a backdated change.

### Why it exists / when to use it
If you post or edit a document with a date in the past, later transactions may need their valuation recalculated (especially with FIFO). Repost Item Valuation replays the affected history to correct the numbers.

### How to use it
1. Go to **Inventory → Repost Item Valuation** and click **New**.
2. Choose the **scope (Repost Method)**: *Item and Warehouse*, *Item-wise*, or *Entire Company*.
3. Set the starting point (item / warehouse / date as required).
4. Submit the job. It runs in the background and shows a **status**: Queued → In Progress → Completed (or Failed / Skipped).

### Tips
- Run this **after** correcting backdated entries, then re-check the Stock Ledger and Stock Balance.
- *Entire Company* reposts can take time — run them during quiet hours.
- This is an admin/manager task, not something warehouse staff use day to day.

---

## Landed Cost Vouchers

**Menu path:** Inventory → Landed Cost Vouchers

### What it is
A **Landed Cost Voucher** adds extra costs — freight, insurance, customs/duty, handling — onto the value of received goods, so your stock is valued at its true landed cost.

### Why it exists / when to use it
The price on the supplier invoice is rarely the full cost of getting a product onto your shelf. For imported pharmaceuticals, shipping, cold-chain logistics, and duty can be significant. This voucher spreads those charges across the received items.

### How to use it
1. Go to **Inventory → Landed Cost Vouchers** and click **New**.
2. Set the **Posting Date**.
3. Link the **received items** the charges apply to.
4. Add the **charges** (e.g. Freight RM 800, Customs RM 1,200).
5. Choose a **Distribution Method**:
   - **Based on Quantity** — split by number of units.
   - **Based on Amount** — split by value (default).
   - **Manual** — you enter the split yourself.
6. **Save** (Draft) → **Submit** to apply the extra cost to item valuation.

### Tips
- Use **Based on Amount** for a mixed shipment of cheap and expensive products; **Based on Quantity** when everything is roughly similar value.
- Apply landed costs promptly after receipt so valuation and margins are accurate.

---

## Quality Inspections

**Menu path:** Inventory → Quality Inspections

### What it is
A **Quality Inspection** records the QC check on a batch of goods — the readings taken, the sample size, and the accept/reject outcome.

### Why it exists / when to use it
Pharmaceutical goods must be QC-checked before they enter saleable stock (incoming) or before they are dispatched (outgoing). If an item is set to **Inspection Required Before Purchase/Delivery**, MyERP enforces an inspection at that point.

### How to use it
1. Go to **Inventory → Quality Inspections** and click **New**.
2. Select the **Item** and the **Inspection Type**: *Incoming*, *Outgoing*, or *In Process*.
3. Enter the **Batch No**, **Sample Size**, and **Inspection Date**.
4. Record the **readings** (measured parameters against acceptance criteria).
5. Set the outcome — **Accepted** or **Rejected** — and add **Remarks**.
6. **Save** and **Submit**.

### Tips
- If any reading fails, the inspection is normally driven to **Rejected** automatically (unless it is a manual inspection).
- Rejected batches should be moved to a **quarantine** warehouse via a Material Transfer and kept out of saleable stock.
- Always record the **Batch No** — it is your link back to the specific lot for recalls.

---

## Batches

**Menu path:** Inventory → Batches

### What it is
A **Batch** (lot) represents a specific production run of an item, with its own manufacturing and expiry dates.

### Why it exists / when to use it
This is the backbone of pharmaceutical traceability. Every batch-tracked item carries a **Batch No**, an **Expiry Date**, and optionally a **Supplier Batch No** — so you can enforce FEFO, block expired stock, and trace any lot instantly during a recall.

### How to use it
1. Go to **Inventory → Batches**.
2. Batches are usually created automatically when you receive batch-tracked stock; you can also create/view them here.
3. Key fields:
   - **Batch No** — the lot identifier.
   - **Item** — the product it belongs to.
   - **Manufacturing Date**, **Expiry Date**, and **Shelf Life (days)**.
   - **Supplier Batch No** — the manufacturer's own lot code.
   - **Use Batchwise Valuation** — value stock per batch.
   - **Is Disabled / Is Cancelled** — to retire a batch.

### Tips
- **Always capture the expiry date** at receipt — it drives expiry reports and near-expiry alerts.
- Disable batches that are expired or recalled so they cannot be picked for sale.
- Reconcile counts **per batch**, not just per item, so expiry data stays accurate.

---

## Serial Numbers

**Menu path:** Inventory → Serial Numbers

### What it is
A **Serial Number** tracks an individual unit of an item, each with its own status (e.g. Active, Delivered).

### Why it exists / when to use it
For high-value or regulated items where you must trace each individual unit — not just the batch — serial tracking gives unit-level traceability from receipt to delivery.

### How to use it
1. Go to **Inventory → Serial Numbers** to view and manage serials.
2. Serials are captured on stock movements for serial-tracked items — you select or scan the specific serials when receiving, transferring, or issuing.

### Tips
- Use serial tracking only where it is genuinely needed — it adds handling effort on every movement.
- Serial numbers and batches can be combined for full lot-plus-unit traceability.

---

## Stock Closing

**Menu path:** Inventory → Stock Closing

### What it is
A **Stock Closing** takes a frozen snapshot of stock quantities and valuation (including the FIFO queue) as at a closing date — an end-of-period stock position.

### Why it exists / when to use it
At month-end or year-end you want a locked, official record of what you held and what it was worth. Stock Closing captures that snapshot so period reports are fast and stable, and the closed period is protected.

### How to use it
1. Go to **Inventory → Stock Closing** and click **New**.
2. Set the **closing date** and scope.
3. **Save** (Draft) → **Submit** to lock the snapshot. (You can **Cancel** to reverse it if needed.)

### Tips
- Run Stock Closing **after** all of the period's stock entries and reconciliations are Posted.
- Do not post backdated entries into a period once it has been closed without checking with your finance team.

---

## Reports

### Stock Balance

**Menu path:** Inventory → Stock Balance

**What it is:** A live view of **how much of each item you have, in each warehouse, right now**, with its value.

**How to use it:**
1. Go to **Inventory → Stock Balance**.
2. Filter by **warehouse**, **item**, **item group**, or **batch** as needed.
3. Read the on-hand quantity and valuation per line.

**Tips:**
- This reflects only **Posted** documents. If a number looks wrong, look for entries still in Draft/Submitted.
- Filter by a cold-chain warehouse to see exactly what is in your fridges.

### Stock Ledger

**Menu path:** Inventory → Stock Ledger

**What it is:** The **transaction history** — every single movement (in and out) for an item, in date order, with the running balance and valuation after each move.

**How to use it:**
1. Go to **Inventory → Stock Ledger**.
2. Filter by **item**, **warehouse**, **batch**, and **date range**.
3. Trace each movement back to the document that caused it.

**Tips:**
- This is your go-to report to answer "why is my balance this number?" — it shows every posting that got you there.
- Filter by **batch** to build a full history of a specific lot for a recall or audit.

---

## How to load opening stock

When you first start using MyERP (go-live), you need to tell the system what you already have in your warehouses. **Do this with a Stock Reconciliation, not a Material Receipt** — the Reconciliation self-balances the value through an opening/difference account, keeping your accounts correct. A plain Material Receipt is for ongoing operational receipts and is not the right tool for the initial load.

Step by step:

1. Make sure your **Items** and **Warehouses** already exist.
2. Go to **Inventory → Stock Reconciliations** and click **New**.
3. Set the **Posting Date** to your go-live/opening date.
4. Set **Purpose** to something like "Opening stock".
5. Choose the **Difference / Opening Account** (ask your finance team which account to use — this is where the opening value is booked).
6. Add one row per item **per batch per warehouse**:
   - **Item** and **Warehouse**.
   - **New Quantity** — the counted quantity on hand.
   - **New Valuation Rate** — your cost per unit.
   - **Batch No** and **Expiry Date** for batch-tracked pharma items.
   - **Serial Numbers** for serial-tracked items.
7. Check the calculated **Difference Amount** looks right.
8. **Save** (Draft), review carefully, then **Submit**.
9. Verify in **Inventory → Stock Balance** that your opening quantities and values are correct.

> **Tip:** Load opening stock **batch by batch with correct expiry dates**. This one-time care means FEFO picking and expiry reporting work correctly from day one.

---

## Typical workflow

A normal end-to-end flow for a pharmaceutical wholesaler:

1. **Set up masters** — create **Items** (with valuation method, reorder levels, batch/QC settings), **Item Attributes**, and **Warehouses** (including dedicated **cold-chain** and **quarantine** locations).
2. **Load opening stock** via **Stock Reconciliation** (batch + expiry per warehouse).
3. **Receive goods** — stock arrives (typically via a Purchase Receipt in the Purchasing module, or a Material Receipt Stock Entry for miscellaneous receipts). Capture **batch and expiry**.
4. **Quality Inspection** — QC-check incoming batches; accept into saleable stock or reject to **quarantine**.
5. **Add landed costs** — record freight/duty via a **Landed Cost Voucher** so valuation is accurate.
6. **Move and reserve stock** — use **Material Transfer** entries to move between stores (e.g. ambient → cold room); use **Stock Reservations** to hold stock for confirmed orders.
7. **Issue / dispatch** — stock leaves on sale or as a **Material Issue** (samples, disposal of expired lots), always **FEFO**.
8. **Post everything** — remember, **balances only change when documents are Posted**.
9. **Count and reconcile** — periodic physical counts corrected via **Stock Reconciliation**.
10. **Monitor** — watch **Stock Balance** and **Stock Ledger**; act on **reorder-level** alerts and near-expiry batches.
11. **Close the period** — run **Stock Closing** at month/year end.

---

## Permissions

Access is controlled per feature, and each stock document separates the right to **create/edit** from the right to **Submit** and **Post** — so the person who prepares a document need not be the one who commits it (a useful control).

Suggested role split:

| Task | Warehouse Staff | Warehouse Manager |
|------|:---------------:|:-----------------:|
| View Items, Warehouses, Stock Balance, Stock Ledger | Yes | Yes |
| Create / edit Items & Warehouses | — | Yes |
| Create / edit **Stock Entries** (Draft) | Yes | Yes |
| **Submit / Post** Stock Entries | — | Yes |
| Create / edit **Stock Reconciliations** (counts, opening stock) | Prepare (Draft) | Submit |
| Record **Quality Inspections** | Yes | Submit / approve |
| **Landed Cost Vouchers** | — | Yes |
| **Item Standard Costs** | — | Yes |
| **Repost Item Valuation** | — | Yes |
| **Stock Closing** | — | Yes |

**Why the split matters:** Posting is what actually moves stock and books the accounting. Reserving the **Submit** and **Post** rights for the **Warehouse Manager** gives you a review checkpoint before anything becomes permanent, while day-to-day staff can still prepare receipts, transfers, and counts. Your exact permissions are configured under **Administration → Identity Management → Roles**; ask your administrator to map these to your organisation's roles.
