# Manufacturing

## Overview

The **Manufacturing** module lets you turn raw materials and components into finished
products, and plan the materials you need to do it. It covers the full production cycle:
defining recipes (Bills of Material), planning what to make (Production Plans), executing
production (Work Orders), tracking shop-floor work (Job Cards), and configuring the
machines and stations that do the work (Workstations, Operations, Routings).

**Is this module for you?** MyERP is used by a pharmaceutical **wholesale** business, so
for day-to-day trading you may not need Manufacturing at all — you buy finished goods and
sell them on. The module becomes useful when you do any light "value-add" work, such as:

- **Repackaging** — breaking a bulk pack into smaller retail packs (or vice versa).
- **Kitting / bundling** — combining several products into a single sellable kit or promo bundle.
- **Relabelling or assembling** — light assembly that produces a new stock item from existing ones.

In these cases you create a simple Bill of Material (the "recipe"), raise a Work Order, and
when you record production the system automatically removes the raw components from stock
and adds the finished item — keeping your inventory accurate. If you never do any of this,
you can safely ignore the whole module; nothing else in MyERP depends on it.

**Where to find it:** Look for the **Manufacturing** entry (factory icon) in the main menu.
It contains sub-menus for Production Plans, Bill of Materials, Workstations, Job Cards, and
Manufacturing Settings. Everything below assumes you have the relevant permissions (see
[Permissions](#permissions)).

---

## Bills of Material (BOM)

### What it is
A Bill of Material is the **recipe** for a product: it lists every raw material or component
that goes into making one finished item, and how much of each is needed. A BOM belongs to
one finished item and is tied to your company.

### Why it exists
Everything else in Manufacturing is built on BOMs. A Production Plan uses BOMs to work out
what materials to buy; a Work Order uses the BOM to know what to consume when it produces
goods. Without a BOM, the system cannot calculate material needs or automatically deduct
components from stock.

### How to use it
**Menu path:** Manufacturing → Bill of Materials (`/manufacturing/bom`)

**To create a BOM:**
1. Click **New BOM**.
2. Fill in the header:
   - **Item** — the finished product this recipe makes (enter its Item ID).
   - **Item Name** — a friendly description.
   - **Quantity** — how many units this recipe yields (usually 1).
   - **Active** — tick to make the BOM usable in planning and work orders.
3. Under **Materials**, click **Add Item** for each component and enter the Item, an
   optional description, the **Quantity** needed, and the **Rate** (cost per unit). The
   line **Amount** and the overall **Total Cost** are calculated automatically.
4. (Optional) Under **Operations**, add production steps with a **Sequence**, operation
   name, **Time (min)**, **Hour Rate**, and **Batch Size**. Operation costs are added to the
   material cost to give the BOM's total cost.
5. Click **Save**.

The **Total Cost** shown at the bottom is split into **Material** cost (sum of the
component lines) plus **Operations** cost (from the operation steps).

### Key concepts
- **BOM explosion** — when a component is itself made from other parts (a *sub-assembly*),
  the system can "explode" the BOM to reveal every underlying raw material. This is used
  automatically during production planning.
- **Phantom items** — a component can be flagged as a *phantom*: a grouping that is never
  actually stocked or produced on its own. During explosion, a phantom item is replaced by
  its own components, which "bubble up" into the parent recipe. Use this for logical
  groupings you don't want to manage as real stock.
- **Sub-assemblies** — a non-phantom component that has its own BOM is kept as a separate
  item during planning, because it is produced (or bought) in its own right.
- **Cycle detection** — the system prevents circular recipes (for example, Item A made from
  Item B, where Item B is made from Item A). If you try to create such a loop, it is blocked
  with an error.

### Tips
- Keep the **Rate** on each component realistic — it drives your finished-goods costing.
- Mark old recipes as **inactive** rather than deleting them, so historic work orders stay intact.
- For simple repackaging, a one-line BOM (one bulk component → one finished pack) is all you need.

---

## Work Orders

### What it is
A **Work Order** is the instruction to actually produce a specific quantity of a finished
item, using a chosen BOM. It is the document that consumes raw materials and produces
finished goods.

### Why it exists
The BOM is only a recipe; the Work Order is the real production run. It tracks how much you
planned to make, how much you have made so far, and moves stock accordingly.

### How to use it
**Menu path:** Manufacturing → Work Orders (`/manufacturing/work-orders`)

**To create a Work Order:**
1. Click **New**.
2. Choose the **Item** to produce and the **BOM** (recipe) to use.
3. Enter the **Quantity** to produce.
4. Set the source, work-in-progress (WIP), and finished-goods (FG) warehouses, and any
   planned start/end dates.
5. Save.

Work Orders can also be **created directly from a Sales Order** — open a sales order and use
the "make Work Order" action to produce goods against that specific customer order.

**The Work Order lifecycle (status flow):**

| Status | Meaning |
|---|---|
| **Draft** | Newly created; still editable. |
| **Submitted** | Confirmed and ready to start. |
| **Not Started** | Materials have been transferred but production hasn't begun. |
| **In Process** | Production has started. |
| **Completed** | Full quantity produced. |
| **Stopped** | Paused/halted. |
| **Cancelled** | Voided. |

**To run production (from the Work Order detail page):**
1. **Submit** the draft.
2. **Start Production** — the order moves to *In Process*.
3. (Optional) **Material Transfer** — move raw materials into the work-in-progress warehouse.
4. **Record Production** — enter the quantity produced. This is the key step: the system
   - checks there is enough stock of each raw material,
   - removes the raw materials from the source warehouse,
   - adds the finished goods into the FG warehouse,
   - updates the produced quantity and, once the full quantity is reached, marks the order **Completed**.
5. Use **Stop** to halt, or the cancel action to void the order.

### Key concepts
- **Overproduction limit** — you cannot record more than the ordered quantity plus an
  allowed overproduction percentage (default **5%**, set in Manufacturing Settings).
- **Stock is only moved when you Record Production** — creating or submitting a Work Order
  does not touch inventory. Recording production is blocked if the accounting period is
  closed or if there isn't enough raw material in stock.

### Tips
- Set the **finished-goods warehouse** before recording production, otherwise the finished
  item won't be received into stock.
- If a run falls short, record the partial quantity — the order stays *In Process* and shows
  a percent-complete figure until the balance is produced.

---

## Job Cards

### What it is
A **Job Card** tracks the execution of a **single operation** within a Work Order — for
example, the "Repackaging" or "Labelling" step. Each Work Order operation gets one or more
Job Cards, and time spent can be logged against them.

### Why it exists
Job Cards give shop-floor visibility: who did which step, at which workstation, for how long,
and how many units passed through. They are mainly relevant if you track labour and machine
time; for simple wholesale repackaging you may not use them at all.

### How to use it
**Menu path:** Manufacturing → Job Cards (`/manufacturing/job-cards`)

A Job Card records the Work Order and operation it belongs to, the **quantity to make**, the
assigned **workstation**, and the time logged. Its lifecycle:

| Status | Meaning |
|---|---|
| **Open** | Created, not yet started. |
| **Work In Progress** | Work has begun. |
| **Material Transferred** | Materials moved for this operation. |
| **On Hold** | Paused. |
| **Completed** | Operation finished. |
| **Cancelled** | Voided. |

Typical actions on a Job Card: **Start**, **Add Time Log** (record a from/to time and the
quantity completed), **Hold**/**Resume**, **Complete**, or **Cancel**. Adding a time log
automatically totals the minutes worked and units completed.

### Tips
- If **Enforce Time Logs** is switched on in settings, staff must log time before completing a Job Card.
- Job Cards are optional — skip them if you don't need step-by-step labour tracking.

---

## Operations & Routings

### What they are
- An **Operation** is a named production step, such as "Cutting", "Assembly", "Repackaging",
  or "Quality Check".
- A **Routing** is an ordered **sequence of operations** that together describe how a product
  is made. A Routing can be attached to a BOM so every Work Order for that item follows the
  same steps.

### Why they exist
They standardise *how* something is made (as opposed to the BOM, which says *what* goes into
it). They also drive costing — each operation has a time and an hourly rate, so the system
can calculate the labour/machine cost of a production run.

### How to use them
- Each Operation can have a **default workstation** (or workstation *type*), a **batch size**
  for splitting work into multiple Job Cards, and an optional **quality inspection** requirement.
- A Routing lists its operations with a **sequence number**, the **time in minutes**, and the
  **workstation**. Sequence numbers must always increase (step 10, then 20, then 30…), which
  keeps the order of work unambiguous.
- Operating cost per step is calculated as **hour rate × (minutes ÷ 60)**.

### Tips
- For light wholesale work, a single operation (e.g. "Repackaging") on the BOM is usually enough.
- Reuse Operations across products so costing stays consistent.

---

## Workstations

### What it is
A **Workstation** represents a physical place or machine where production happens — a packing
bench, a labelling machine, an inspection table. It carries a capacity, an hourly cost, and
working hours.

### Why it exists
Workstations let you cost operations accurately (via their hourly rate) and, where used, plan
capacity and scheduling around available working hours and holidays.

### How to use it
**Menu path:** Manufacturing → Workstations (`/manufacturing/workstations`)

1. Click **New** and give the workstation a **Name** (and optional type).
2. Set the **Production Capacity** (how many jobs it can run at once — default 1).
3. Add **cost components** (e.g. electricity, labour, rent). The workstation's **Hour Rate**
   is the sum of all its cost components and is calculated automatically.
4. Add **working hours** per day, and optionally link a **holiday list** to block scheduling
   on non-working days.

### Tips
- The hour rate you set here flows into operation and BOM costs — keep it up to date.
- You can also reach Workstations from **Settings → Workstations**.

---

## Production Plans (MRP)

### What it is
A **Production Plan** is a planning document that looks at what you want to make and works
out **what materials you need** and **which work orders to raise**. This is the module's
lightweight **MRP (Material Requirements Planning)** engine.

### Why it exists
Instead of manually checking each recipe, a Production Plan explodes all the BOMs for the
items you plan to make, adds up the material requirements, and can then generate the
follow-on documents (Material Requests to buy/pull stock, and Work Orders to produce) in one go.

### How to use it
**Menu path:** Manufacturing → Production Plans (`/manufacturing/production-plans`)

1. Click **New** and set the **Posting Date** and target warehouse.
2. Add the **planned items** — the finished products you intend to make, each with its BOM
   and planned quantity.
3. Choose planning options as needed:
   - **Combine Items** — merge duplicate materials across planned items into single lines.
   - **Ignore Existing Ordered Qty** — plan the full requirement without deducting what's already on order.
   - **Consider Minimum Order Qty** — round material needs up to supplier minimums.
   - **Include Safety Stock** — add each item's safety-stock buffer to the requirement.
   - **Skip Available Sub-Assembly Item** — don't raise work orders for sub-assemblies you already have in stock.
4. Save, then use the buttons on the plan's detail page:
   - **Calculate Materials** — explodes the BOMs (phantom-aware) and lists every raw material
     required, with quantities and target warehouse. Each line is marked as either
     *in-house manufacturing* (a sub-assembly to produce) or *material request* (to buy/pull).
   - **Submit** — confirm the plan (requires at least one planned item).
   - **Generate Work Orders** — automatically create a Work Order for each planned item that
     doesn't already have one, pre-filled with the item, BOM, quantity, and required materials.
   - **Generate Material Requests** — create Material Requests for the calculated raw materials
     so procurement/stores can supply them.
   - **Cancel** — void the plan.

**Production Plan lifecycle:** Draft → Submitted → In Progress → Completed (or Cancelled).
The plan moves to *In Progress* automatically once you generate Work Orders from it.

### Tips
- Run **Calculate Materials** first and review the requirements before generating anything downstream.
- Work Orders and Material Requests are each generated **only once** per line — the buttons
  skip items that already have them, so you can't accidentally double-create.

---

## Manufacturing Settings

### What it is
A single, **per-company** configuration page that controls how the whole Manufacturing
module behaves.

### Why it exists
It centralises the rules — like how much overproduction is allowed, or how raw materials are
consumed — so they apply consistently across every Work Order and Job Card.

### How to use it
**Menu path:** Manufacturing → Manufacturing Settings (`/manufacturing/settings`)

Adjust the settings and save. The main options:

| Setting | What it controls |
|---|---|
| **Overproduction %** | How much beyond the ordered quantity a Work Order may produce (default 5%). |
| **Extra Materials %** | Extra raw material allowed to be transferred beyond the BOM quantity. |
| **Backflush RM Based On** | Whether raw materials are consumed per the **BOM** quantity or per what was **actually transferred**. |
| **Track Material Consumption** | Record actual vs planned material usage. |
| **Auto Serial/Batch from WO** | Auto-generate serial/batch numbers from the Work Order. |
| **Auto-Update BOM Costs** | Refresh BOM costs automatically when material prices change. |
| **Include Corrective Ops Cost** | Add rework/corrective operation cost into finished-goods value. |
| **Mins Between Ops** | Minimum gap between sequential operations (minutes). |
| **Capacity Planning Days** | Planning horizon for scheduling. |
| **Allow Overtime / Allow Holiday Production** | Permit production outside normal hours / on holidays. |
| **Disable Capacity Planning** | Skip capacity checks when scheduling. |
| **Job Card Excess Transfer** | Allow extra material transfer per Job Card. |
| **Enforce Time Logs** | Require time logs before a Job Card can be completed. |
| **Validate Component Qty per BOM** | Check consumed component quantities against the BOM. |

### Tips
- If you set **Backflush RM Based On** to anything other than "BOM", the **Validate Component
  Qty per BOM** check is automatically switched off (the two rules are mutually exclusive).
- For simple repackaging, the defaults are fine — the main one to review is **Overproduction %**.

---

## Typical workflow

A complete production cycle flows through the module like this:

1. **Bill of Material** — define the recipe for the finished item (what goes into it, and how much).
2. **Production Plan (MRP)** *(optional)* — list the items you plan to make, then **Calculate
   Materials** to see what's required, and **Generate Work Orders** and/or **Material
   Requests**.
3. **Work Order** — for each item to produce: **Submit** → **Start Production**.
4. **Job Card** *(optional)* — track each operation's progress and log time on the shop floor.
5. **Record Production** on the Work Order — the system removes raw materials from stock and
   receives the **finished goods** into the FG warehouse, completing the order.

```
BOM (recipe)
   └─▶ Production Plan  ──Calculate Materials──▶ Material Requirements
          ├──Generate Material Requests──▶ (procure/pull raw materials)
          └──Generate Work Orders──────▶ Work Order
                                             ├─ Submit → Start
                                             ├─ Job Cards (per operation)
                                             └─ Record Production ─▶ raw materials consumed,
                                                                     finished goods received
```

For a wholesaler doing simple repackaging or bundling, you can skip the Production Plan and
Job Cards entirely: just create a **BOM**, raise a **Work Order**, and **Record Production**.

---

## Permissions

Access to Manufacturing is controlled by permissions assigned to your role (Administration →
Roles / Permissions). The relevant permission groups are:

| Permission group | Covers | Available actions |
|---|---|---|
| **Manufacturing** (`MyERP.Manufacturing`) | BOMs, Work Orders, Job Cards, Operations, Routings, Workstations, Settings | View, Create, Edit, Delete |
| **Production Plans** (`MyERP.ProductionPlans`) | Production Plans / MRP | View, Create, Edit, Delete, Submit, Cancel |
| **Material Requests** (`MyERP.MaterialRequests`) | Generating material requests (from plans or work orders) | View, Create, Edit, Delete, Submit, Cancel |

Notes:
- You need the base (View) permission of a group just to see its menu and pages.
- Creating Work Orders, recording production, and running Job Cards fall under the
  **Manufacturing** Create/Edit permissions.
- **Calculate Materials** and **Generate Work Orders** require Production Plans **Edit**;
  **Generate Material Requests** additionally requires Material Requests **Create**.

If a menu item or button is missing, ask your administrator to grant the matching permission.
