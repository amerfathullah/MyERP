# Projects

## Overview

The **Projects** module lets you plan and track pieces of work from start to finish,
record the hours your staff spend on them, and — when the work is billable —
turn those hours into a customer invoice.

You reach everything from the **Projects** menu (the project-diagram icon) in the
left sidebar. Under it you will find three areas:

- **Project List** – your projects, their tasks and progress.
- **Timesheets** – time your employees log against projects and activities.
- **Timesheet Billing** – turning billable, logged time into sales invoices.

**Is this module for you?**
For a pure wholesale operation — buying and reselling pharmaceutical stock —
Projects is **optional**. You can run the whole business on Sales, Purchasing and
Inventory without ever opening it.

It becomes useful when you take on **service or implementation work** that runs
alongside the trading business, for example:

- Setting up and validating cold-chain equipment at a customer's site.
- A software or systems rollout for a hospital or clinic.
- An internal improvement project (a warehouse fit-out, a compliance programme).

In those cases Projects gives you a place to break the work into tasks, watch
how far along it is, and — for customer work — recover the time you spend.

---

## Projects

### What it is

A **project** is a container for a body of work. It holds a set of **tasks**,
tracks an overall **percentage complete**, and can optionally be linked to a
**customer** and a **sales order** (for customer-facing work) so you can measure
what it cost you against what you billed.

Each project is created under one of your **companies** and is given an automatic
**project number** — you do not type this in yourself.

A project moves through three states:

- **Open** – active work.
- **Completed** – finished (reached automatically at 100%, or set by hand).
- **Cancelled** – stopped. A completed project cannot be cancelled; a cancelled
  one can be re-opened.

### Why it matters

Projects turn a vague "we're doing an installation for General Hospital" into
something you can measure: a checklist of tasks, a live completion figure, and —
because time is logged against the project — a clear picture of the effort and
cost behind it.

### How to create a project

1. Open **Projects → Project List** from the sidebar.
2. Click **New** (top right).
3. Fill in the form:
   - **Company** (required) – which of your companies owns this project. This is
     pre-filled with your current company.
   - **Name** (required) – a clear title, e.g. *"Cold-chain validation – Pantai Hospital"*.
   - **Description** – a short summary of the work.
   - **Start date** and **End date** – the planned schedule.
   - **Estimated cost** – your budget for the work, in MYR.
   - **Notes** – anything else worth recording.
4. Click **Save**. The project appears in the list with its new project number
   and a status of **Open**.

To change a project later, open it from the list and edit the same form.

### Adding tasks

A **task** is a single unit of work inside the project (for example *"Site survey"*,
*"Install units"*, *"Customer sign-off"*). Each task carries its own details:

- **Subject** – what the task is. It gets an automatic task number (e.g. `TASK-4F9A21`).
- **Priority** – Low, Medium, High or Urgent.
- **Assigned employee** – who owns it.
- **Expected start / end dates** and **expected hours**.
- **Progress** – a 0–100% figure for how far along the task is.
- **Task weight** – how much this task counts towards the project total
  (see *Progress calculation methods* below). The default weight is 1.
- **Milestone** – mark a task as a milestone to flag a key checkpoint.
- **Group task** – a task can act as a parent that holds sub-tasks beneath it.

Each task moves through its own states: **Open → Working → Completed**, with
**Overdue** and **Cancelled** also possible. Completing a task automatically sets
its progress to 100%.

### Task dependencies

A **dependency** says *"this task cannot be finished until another task is done first."*
For example, *"Customer sign-off"* depends on *"Install units"*.

- You add a dependency by pointing a task at the earlier task it depends on.
- The system **prevents circular dependencies** — if Task A depends on Task B,
  you cannot then make Task B depend on Task A (directly or through a chain).
  It checks the whole chain, not just the two tasks in front of you.
- When you try to mark a task **Completed**, the system checks that **every task
  it depends on is already completed**. If any is still outstanding, completion
  is blocked and you are told which dependencies are holding it up.

This keeps your project honest: you cannot accidentally close out a task whose
prerequisites are not finished.

### Progress calculation methods

Every project has a **progress calculation method** that decides how its overall
**percent complete** is worked out from its tasks. The project recalculates this
automatically whenever tasks are added, changed, completed, or removed.

There are four methods:

| Method | How the project percentage is calculated |
| --- | --- |
| **Task Completion** *(default)* | The share of tasks that are Completed or Cancelled. Five tasks with two done = 40%. |
| **Task Progress** | The average of every task's own progress percentage. |
| **Task Weight** | A weighted average using each task's *task weight*, so bigger tasks pull the figure more. |
| **Manual** | You type the percentage in yourself (0–100). The system will not change it automatically. |

Notes:

- With the three automatic methods, you do not touch the project percentage — it
  follows the tasks.
- **Manual** is the only method where you can set the figure directly; with any
  other method the system owns it.
- When a project reaches **100%** while Open, it is marked **Completed**
  automatically.

### Costing and margin

For customer work, a project also tracks estimated cost, total cost, amount to be
billed, and amount actually billed. From these it shows a **gross margin**
(billed amount minus cost) — a quick read on whether a job made money. Timesheets
(below) are what feed the cost and billing figures.

---

## Timesheets

### What it is

A **timesheet** records the time an employee spent, broken into individual
**time entries**. Each entry is a row: an activity type, the number of hours, and
optionally which project and task the time was spent on, plus whether that time is
**billable** to a customer.

A timesheet covers a date range (for example one week) for one employee, and moves
through these states: **Draft → Submitted → Billed**, with **Cancelled** also
possible. Time can only be edited while the timesheet is still a **Draft**.

### Why it matters

Timesheets are how effort gets captured. They feed two things:

- **Project cost and progress** – the hours worked on a project.
- **Billing** – any billable hours become the raw material for customer invoices
  (see *Timesheet Billing*).

Without timesheets, a project shows tasks and dates but no record of the actual
time and money spent.

### How to log time

1. Open **Projects → Timesheets** from the sidebar.
2. Click **New Timesheet**.
3. Fill in the header:
   - **Company** (required) – pre-filled with your current company.
   - **Employee** (required) and **Employee name** – who the time belongs to.
   - **Start date** and **End date** – the period this timesheet covers.
4. Under **Time Entries**, click **Add Item** for each block of work and fill in:
   - **Activity type** – the kind of work, e.g. *Development*, *Consulting*,
     *Design*. This drives the default rates (see below).
   - **Hours** – how long it took.
   - **Billable** – tick this if the time should be charged to a customer.
   - **Billing rate** – the hourly charge. Leave it at 0 to let the system fill in
     a default rate for you (explained below).
   - **Description** – optional detail about what was done.
5. Add as many rows as you need, then click **Save**. The timesheet is created as
   a **Draft**.

The list view shows each timesheet's total hours, billing amount and status.

### How rates are filled in

You do not have to type a rate on every row. When you leave the billing (or
costing) rate at zero, the system looks it up automatically for that employee and
activity type, in this order:

1. **Employee-specific rate** – if a special rate has been set for that employee
   doing that activity, it is used.
2. **Activity type default rate** – otherwise, the standard rate for that activity
   type is used.
3. If neither exists, the rate stays at zero and you can enter it by hand.

For each billable row, the **billing amount** is simply *rate × hours*. The
timesheet totals up billable hours and billing amount for you.

### Submitting

Billing only picks up time from **Submitted** timesheets, so once a timesheet's
entries are correct, submit it. A timesheet must have at least one time entry
before it can be submitted, and once submitted its entries are locked.

---

## Timesheet Billing

### What it is

**Timesheet Billing** is the screen that turns billable, submitted, not-yet-billed
time into a **sales invoice** for a customer. It gathers all the qualifying time
entries, groups them so you can see what you are about to charge, and creates the
invoice in one step.

### Why it matters

This is where logged effort becomes revenue. Instead of re-typing hours onto an
invoice by hand, you pick a customer, review the billable time, and let the system
build the invoice — with every line traceable back to the timesheet it came from.

### What counts as "billable and unbilled"

An entry is included in billing only when **all** of these are true:

- It sits on a **Submitted** timesheet (not a draft).
- It is marked **Billable**.
- It has a billing amount greater than zero.
- It has **not already been billed** on an earlier invoice.

### How to bill time

1. Open **Projects → Timesheet Billing** from the sidebar.
2. (Optional) Enter a **Project** to bill only the time logged against that one
   project. Leave it blank to include all qualifying time for the company.
3. The screen shows an **unbilled summary**, grouped by activity type, with the
   total hours and total amount (in MYR) for each — plus a grand total of hours
   and amount at the bottom.
4. Choose the **Customer** to invoice.
5. Click **Create Invoice**.
6. The system creates a sales invoice — one line per time entry, described as
   *"Activity type – Nh"* — and takes you straight to it. A confirmation shows the
   new invoice number, the total hours and the total amount.

Behind the scenes, every entry that went onto the invoice is stamped as billed, so
it will not appear on a future billing run — no double-charging.

If there is nothing to bill (no submitted, billable, unbilled time for your choice
of company and project), the system tells you and no invoice is created.

---

## Permissions

Access to the Projects module is controlled by role permissions. An administrator
grants these under **Administration → Identity → Roles**.

| Permission | What it allows |
| --- | --- |
| **Projects** | See the Projects menu, project list, tasks and timesheets. Required for everything below. |
| **Projects – Create** | Create new projects, tasks and timesheets. |
| **Projects – Edit** | Change projects and tasks; start/complete/cancel tasks; complete or cancel projects; submit or cancel timesheets. |
| **Projects – Delete** | Delete projects and tasks. |

A note on billing: creating an invoice from timesheets uses the **Sales Invoices –
Create** permission (from the Sales module), because it produces a real sales
invoice. A user therefore needs both Projects access and the right to create sales
invoices in order to bill time.

Users without the base **Projects** permission will not see the Projects menu at all.
