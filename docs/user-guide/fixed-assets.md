# Fixed Assets

## Overview

The **Fixed Assets** module keeps track of the physical property your business
owns and uses over many years — things like refrigerated delivery vans,
cold-storage units, warehouse shelving, forklifts, and office IT equipment.

Unlike stock you buy and resell, a fixed asset is kept and *used* by the
business. Its value is spread out — "depreciated" — a little at a time across
its useful life, instead of being treated as one big expense on the day you
buy it.

Why bother tracking and depreciating assets?

- **Accounting accuracy** — your books show what your equipment is really worth
  today (its *book value*), not just what you paid for it years ago.
- **Tax** — depreciation is a recognised business expense. Recording it
  correctly each period keeps your profit figures — and your tax — honest.
- **Control** — you always know what you own, where it is, who is responsible
  for it, and when it is due for replacement.

Every time depreciation is recorded, the system automatically posts the matching
entry into the accounting ledger, so Finance never has to key it in by hand. When
an asset is eventually sold or scrapped, the module closes it off and works out
whether you made a gain or a loss on the disposal.

You will find everything under the **Fixed Assets** menu (the building icon),
which contains three areas:

| Menu item | What it is for |
|-----------|----------------|
| **Fixed Assets** | The asset register — every item you own |
| **Asset Repairs** | Logging repair and maintenance costs |
| **Asset Capitalization** | Building a new asset out of stock, services or other assets |

Amounts are shown in Malaysian Ringgit (RM).

### The life of an asset

Every asset follows the same journey, and its **status** tells you where it is:

```
Draft ─▶ Submitted ─▶ Partially Depreciated ─▶ Fully Depreciated
                                │                      │
                                └────────┬─────────────┘
                                         ▼
                                  Sold  /  Scrapped
```

| Status | Meaning |
|--------|---------|
| **Draft** | Just entered, not yet active. Can still be edited or cancelled. |
| **Submitted** | Confirmed and active; depreciation can now be posted. |
| **Partially Depreciated** | Some depreciation has been posted; value is dropping. |
| **Fully Depreciated** | Book value has reached zero; no further depreciation. |
| **Sold** | Disposed of by sale. Closed. |
| **Scrapped** | Written off / thrown away. Closed. |
| **In Maintenance** | Temporarily out of service for repair. |
| **Cancelled** | Voided while still in Draft. |

---

## Asset Categories

A **category** is a template that groups similar assets together — for example
*Delivery Vehicles*, *Cold-Storage Equipment*, *IT Equipment*, or *Warehouse
Fittings*. Setting up categories first saves you time, because every asset you
register can inherit its category's defaults instead of you filling them in each
time.

Each category holds:

| Setting | What it controls |
|---------|------------------|
| **Category Name** | The label, e.g. "Delivery Vehicles". |
| **Is Depreciable** | Whether assets in this category lose value over time. (Land, for example, would be off.) |
| **Default Depreciation Method** | Straight-Line, Double-Declining-Balance, or Written-Down-Value (see [Assets](#assets)). |
| **Default Useful Life** | How long the asset is expected to last, in months. New categories default to **60 months (5 years)**. |
| **Default Depreciation Rate** | The yearly rate (a percentage) — used by the Written-Down-Value method. |

Categories also carry the **accounting links** that make automatic posting
possible:

| Account | Used for |
|---------|----------|
| **Asset Account** | Where the asset's cost sits on the balance sheet. |
| **Depreciation Expense Account** | Where each period's depreciation is charged as an expense. |
| **Accumulated Depreciation Account** | The running total of depreciation charged so far. |

When depreciation is posted, the system looks for these accounts **on the
category first**; if any are blank, it falls back to the company's default
accounts. If neither is set, that asset is safely skipped rather than posted
wrongly — so it is worth setting the accounts on each category up front.

---

## Assets

### What it is

The asset register is the master list of every fixed asset the company owns.
Each entry records what the item is, what it cost, where it lives, and how its
value is written down over time.

An asset carries an auto-generated **Asset Number**, plus details such as:

- **Asset Name** and **Location** (e.g. "Freezer Van 02", "Shah Alam depot")
- **Purchase Date** and **Purchase Amount**
- **Additional Cost** — delivery, installation, fit-out, etc. The
  **Total Asset Cost** is *Purchase Amount + Additional Cost*.
- **Book Value** (shown as *Value after Depreciation*) — what the asset is worth
  today after depreciation so far.

### Why depreciate

Recording depreciation spreads the cost of the asset across the years it is
actually used, so each period's profit reflects the wear-and-tear on your
equipment. The module can do this automatically once you switch on depreciation
and choose a method.

### The three depreciation methods

When you enable **Calculate Depreciation**, you pick how the value should fall:

| Method | How each period's charge is worked out | Best suited to |
|--------|----------------------------------------|----------------|
| **Straight-Line** | The same amount every period: total cost ÷ number of periods. | Assets that wear evenly — shelving, fittings, buildings. |
| **Double-Declining-Balance (DDB)** | A bigger charge early on, tapering off: current book value × (2 ÷ number of periods). | Assets that lose value fast when new — IT equipment, vehicles. |
| **Written-Down-Value (WDV)** | Current book value × your yearly **rate %**. | Assets depreciated at a fixed statutory/tax rate. |

There is also a **Manual** option for assets you prefer to handle by hand.

You also set:

- **Useful Life (months)** — the total lifespan.
- **Frequency (months)** — how often a charge is posted (defaults to every
  **12 months**). Useful Life ÷ Frequency gives the number of depreciation
  periods.
- **Available-for-Use Date** — when depreciation starts counting (defaults to
  the purchase date if left blank).
- **Opening Accumulated Depreciation** — for assets that were already partly
  used before being entered into the system.

Whichever method you choose, the **final period automatically absorbs any
rounding difference** so the book value lands exactly on zero — you never end up
with a few stray cents.

### How to register an asset

1. Go to **Fixed Assets** in the menu and click **New Asset** (or **Register
   First Asset** if the list is empty).
2. Choose the **Company**, and enter the **Name** and **Location**.
3. Enter the **Purchase Date**, **Purchase Amount**, and any **Additional
   Cost**.
4. Set the **Useful Life** in months and, where relevant, add **Notes**.
5. Assign an **Asset Category** so the item inherits the right method and
   accounts.
6. Click **Save**. The asset is created in **Draft** with an Asset Number, and
   its depreciation schedule is generated automatically when depreciation is
   switched on.
7. Review the details, then **Submit** the asset to make it active. Once
   submitted, depreciation can be posted.

> **Note:** an asset can only be submitted if its depreciation settings are
> complete — a useful life and an available-for-use date must be filled in.

### How depreciation gets posted

You do not run depreciation by hand each month. A **scheduled background job**
checks daily for any depreciation that has fallen due (schedule date on or
before today) and, for each one, posts a journal entry automatically:

- **Debit** the Depreciation Expense account
- **Credit** the Accumulated Depreciation account

It then reduces the asset's book value and updates its status to **Partially
Depreciated**, or to **Fully Depreciated** once the value reaches zero. Draft
assets, and any dates that fall inside a **frozen accounting period**, are
skipped so the closed books are never disturbed.

---

## Repairs

### What it is

**Asset Repairs** log the cost of fixing or servicing an asset — for example
replacing a compressor in a cold-storage unit or repairing a van's refrigeration
system. Each repair records a description, the **Failure Date**, the **Repair
Cost** (parts plus labour), and a **Completion Date**.

A repair moves through **Pending → Completed**, or can be **Cancelled**.

### When to use it — and the "Capitalize" choice

The key decision on a repair is whether to **Capitalize** the cost:

- **Do not capitalize** (the usual case) — a routine repair that just keeps the
  asset working. The cost is a normal expense and does **not** change the
  asset's value.
- **Capitalize the repair cost** — a major improvement that genuinely adds value
  or extends the asset's life (e.g. a full engine rebuild). When you tick
  **Capitalize** and the repair is marked **Completed**, its cost is **added to
  the asset's book value**, and you can also enter **Increase in Asset Life**
  (extra months) to lengthen the depreciation schedule.

> **Fully depreciated assets** can still be repaired, but capitalizing the cost
> and extending the life are automatically switched off — you cannot add value
> back to an asset that has already been written down to zero.

### How to log a repair

1. Go to **Asset Repairs** and click **New Repair**.
2. Select the **Asset**, and enter a **Description** and the **Failure Date**.
3. Enter the **Repair Cost**.
4. For a value-adding improvement, tick **Capitalize** and, if relevant, enter
   the extra months of life.
5. Save. The repair starts as **Pending**.
6. When the work is done, open the repair and click the green **Complete**
   tick (or **Cancel** with the red cross if it did not go ahead). Completing a
   capitalized repair updates the asset's value and life at that point.

---

## Capitalizations

### What it is

**Asset Capitalization** *builds* a fixed asset out of other things you already
have, rather than buying it ready-made. It is how you convert **Capital Work in
Progress (CWIP)** — the parts, labour and services that go into constructing or
assembling something — into a finished, depreciable asset.

A capitalization targets one **asset** (the finished item) and gathers together
the value being poured into it from three sources:

| Source | What it means | Effect |
|--------|---------------|--------|
| **Stock Items** | Parts and materials taken from the warehouse. | Reduces inventory; adds their cost to the asset. |
| **Service / Expense Items** | Labour, contractor fees and other services. | Reduces the expense; adds it to the asset. |
| **Consumed Assets** | Existing assets folded into the new one. | Removes the old asset; adds its remaining value to the target. |

The system adds these up into the **Total Value** capitalized onto the target
asset. A capitalization runs **Draft → Submitted**, and can be **Cancelled**.

### When to use it

Use a capitalization when a new asset is *assembled or built* rather than simply
purchased — for example fitting out a cold-storage room from bought-in panels,
refrigeration units and installation labour, then recognising the whole thing as
a single asset that depreciates over its life.

### How to create one

1. Go to **Asset Capitalization** and click **New Capitalization**.
2. Choose the **Target Asset** that will receive the value, and set the
   **Posting Date**.
3. Add the **Stock Items**, **Service Items**, and any **Consumed Assets** that
   go into it. The **Total Value** updates as you add each line.
4. Save as **Draft** to keep working, or **Submit** to finalise. On submission
   the value is transferred onto the target asset.

---

## Disposal (sale/scrap)

Eventually every asset reaches the end of its useful life and is disposed of.
There are two ways to close it, and both record a **Disposal Date**:

- **Sell** — the asset is sold. You enter the **Disposal Amount** (what you
  received). The system compares this with the asset's remaining book value:

  > **Gain or loss = Disposal Amount − Book Value.**
  >
  > A positive figure is a **gain** (you sold it for more than its book value);
  > a negative figure is a **loss** (a write-off). This is posted to accounting
  > automatically.

- **Scrap** — the asset is thrown away or written off with no money received.
  The disposal amount is zero, so its entire remaining book value becomes a loss.

An asset can be sold or scrapped from any active state, but **not** while it is
still a **Draft** or already **Cancelled**. Once disposed, the asset moves to
**Sold** or **Scrapped** and is closed — no further depreciation is posted.

---

## Permissions

Access to the Fixed Assets module is controlled by the **Asset Management**
permission group. An administrator grants these to a role under
*Settings → Permissions*:

| Permission | Allows the user to |
|------------|--------------------|
| **Asset Management** (view) | Open and view the asset register, repairs and capitalizations. |
| **Register assets** | Create new assets, categories, repairs and capitalizations. |
| **Edit assets** | Change existing assets, and record sales/scrap disposals. |
| **Delete assets** | Remove assets. |
| **Submit assets** | Submit assets, and complete or cancel repairs. |

Give day-to-day staff view and register rights, and reserve **Submit**,
**Edit** (disposals) and **Delete** for supervisors or Finance so that
activation, disposal and removal of assets stay under control.
