# Workflow & Automation

## Overview

MyERP has two features that help you stay in control of your business while cutting
down on repetitive manual work:

- **Workflow (Approvals)** puts a checkpoint in front of important documents. For
  example, a large purchase order can be made to wait for a manager's sign-off before
  it can go through. This protects the business from costly mistakes and enforces your
  spending policies.
- **Automation** reacts automatically to things that happen in the system — a sales
  invoice being submitted, stock dropping below its reorder level, an invoice going
  overdue — and takes an action for you, such as sending a notification. This saves
  time and makes sure routine follow-ups never get forgotten.

Both are about the same two goals: **control** (nothing important slips through
without the right person's approval) and **efficiency** (the system does the routine
chasing so your team doesn't have to).

This guide covers the following areas. You will find each on the main menu:

| Area | Where to find it | What it does |
| --- | --- | --- |
| Approval Rules | **Workflow → Approval Rules** | Define who must approve which documents, and above what amount |
| Pending Approvals | **Workflow → Pending Approvals** | The approver's inbox — review and approve or reject |
| Automation Rules | **Automation** | Set up "when this happens, do that" rules |
| Authorization Rules | **Settings → Authorization Rules** | Spending/discount thresholds that require a senior person to approve |
| Email Templates | **Settings → Email Templates** | Reusable message layouts for automated emails |
| Notification Logs | **Settings → Notification Logs** | A history of every alert the system has sent |

---

## Approval Rules

**Menu:** Workflow → Approval Rules

### What it is

An approval rule says: *"For this type of document, at this approval level, this
person or role must sign off — and (optionally) only when the amount reaches a certain
size."* You can stack several rules to build **multi-level approvals** (for example,
a supervisor approves first, then a manager).

### Why it matters

Without approval rules, any user with edit rights could push through a large purchase
or a big journal entry on their own. Approval rules make sure the right people are
looped in before money moves, giving you an audit trail of who approved what and when.

### How multi-level approval works

- Each rule has a **Level** (1, 2, 3, …). Level 1 is the first approval required.
- When a document needs approval, the system creates the Level 1 request(s) first.
- Only **after every Level 1 approver has approved** does the system move the document
  up to Level 2, and so on.
- The document is considered **fully approved** only once all levels are approved.
- If any approver **rejects**, that approval step is marked rejected and the document
  does not advance.

### The fields on an approval rule

| Field | Meaning |
| --- | --- |
| **Name** | A label for the rule, e.g. "Large PO – Manager approval" |
| **Document Type** | Which document this applies to: Sales Invoice, Purchase Invoice, Purchase Order, Payment Entry, Journal Entry, or Stock Entry |
| **Level** | The approval step (1 = first). Use higher numbers for additional layers |
| **Approver (Role)** | The role whose members can approve at this level, e.g. "Manager" |
| **Minimum Amount** | Optional. The rule only kicks in when the document's total is at or above this amount. Leave blank to require approval on every document of this type |
| **Description** | Optional notes explaining the rule |
| **Active** | Turn the rule on or off without deleting it |

### How to create an approval rule

1. Go to **Workflow → Approval Rules**.
2. Click **New Rule**.
3. Enter a **Name**.
4. Choose the **Document Type** the rule applies to.
5. Set the **Level** (start with 1 for the first approver).
6. In **Approver (Role)**, type the role that should approve at this level.
7. (Optional) Set a **Minimum Amount** so the rule only applies to larger documents.
8. Make sure **Active** is ticked.
9. Click **Save**.

To build a second level of approval, create another rule for the **same document
type** with **Level** set to 2 and the more senior role as the approver.

### Editing a rule

On the Approval Rules list, click the **pencil** icon on any row to change it. The
list shows each rule's document type, level, approver, minimum amount, and whether it
is active.

---

## Pending Approvals

**Menu:** Workflow → Pending Approvals

### What it is

This is the **approver's inbox**. It lists every document that is currently waiting
for approval. Each item shows what kind of document it is, the approval level, who
requested it, and when it was raised. The heading shows a live count of how many items
are pending.

### The columns

| Column | Meaning |
| --- | --- |
| **Document Type** | The kind of document waiting (e.g. Purchase Order) |
| **Level** | Which approval level this request is at |
| **Requested By** | The user who submitted the document |
| **Created Date** | When the approval request was raised |

### How to approve or reject

1. Go to **Workflow → Pending Approvals**.
2. Find the item you need to review.
3. Click **Approve** (green tick) to sign off, or **Reject** (red cross) to send it
   back.

What happens next:

- **Approve** — the request is marked approved. If it was the last approver needed at
  this level, the document automatically advances to the next approval level (if one
  exists) or becomes fully approved.
- **Reject** — the request is marked rejected and the document does not advance.

> Note: An approval step can only be actioned once. After it has been approved or
> rejected, it can't be changed.

---

## Automation Rules

**Menu:** Automation

### What it is

An automation rule follows the pattern **"When [trigger] happens, do [action]"** —
optionally only when a **condition** is met. Rules run in the background so your team
doesn't have to remember to do routine tasks.

### Why it matters

Automation removes manual chasing. Instead of someone remembering to send an alert
every time an invoice goes overdue, or every time a large order comes in, the system
does it for you — instantly and consistently.

### The fields on an automation rule

| Field | Meaning |
| --- | --- |
| **Name** | A label for the rule |
| **Description** | Optional notes about what the rule does |
| **Trigger** | The event that starts the rule (see list below) |
| **Document Type** | Which document the rule watches. Leave blank for **All** |
| **Condition** | Optional. A simple test such as `GrandTotal > 5000`. If blank, the rule always runs when triggered |
| **Action** | What the rule does when it runs (see list below) |
| **Priority** | When several rules match, lower numbers run first |
| **Action Config (JSON)** | Optional advanced settings for the action |
| **Active** | Turn the rule on or off |

### Available triggers

**Event triggers** (fire when something happens to a document):

- Document Submitted
- Document Approved
- Document Posted
- Document Cancelled
- Payment Received
- Stock Below Reorder
- Invoice Overdue
- E-Invoice Validated
- E-Invoice Rejected
- Approval Required

**Scheduled triggers** (fire on a timetable):

- Daily Schedule
- Weekly Schedule
- Monthly Schedule

### Available actions

- Send Notification (an in-app alert to the user)
- Send Email
- Submit to LHDN (send an e-invoice to the tax authority)
- Create Approval Request
- Update Field
- Create Follow-up Task
- Post to Accounting

### How the condition works

The condition is a simple three-part test: a field, a comparison, and a value —
for example `GrandTotal > 5000` or `Status == Overdue`. Supported comparisons are
`>`, `>=`, `<`, `<=`, `==` (equals) and `!=` (not equals). If the condition is left
blank, the rule runs every time its trigger fires.

### How to create an automation rule

1. Go to **Automation**.
2. Click **New Rule**.
3. Enter a **Name** and, optionally, a **Description**.
4. Choose the **Trigger** — the event that should start the rule.
5. Choose the **Document Type** it applies to (or leave blank for all).
6. (Optional) Enter a **Condition**, e.g. `GrandTotal > 5000`.
7. Choose the **Action** to perform.
8. Set a **Priority** if you have several rules that could run together.
9. Make sure **Active** is ticked, then click **Save**.

### Managing rules

On the Automation list you can:

- **Toggle a rule on/off** using the switch in the **Active** column — no need to
  delete it.
- **Edit** a rule with the pencil icon.
- **Delete** a rule with the trash icon.

Each rule keeps an **execution history** so you can see when it ran, whether it
succeeded, and any error — useful for checking that a rule is doing its job.

### Examples

| You want to… | Trigger | Condition | Action |
| --- | --- | --- | --- |
| Alert the manager about big new orders | Document Submitted | `GrandTotal > 10000` | Send Notification |
| Chase overdue invoices | Invoice Overdue | *(none)* | Send Email |
| Re-order when stock runs low | Stock Below Reorder | *(none)* | Create Follow-up Task |
| Auto-submit validated e-invoices to LHDN | E-Invoice Validated | *(none)* | Submit to LHDN |

---

## Notifications & Email Templates

Automation and approvals reach people through **notifications** (in-app alerts) and
**emails**. Two settings screens support this.

### Email Templates

**Menu:** Settings → Email Templates

Email templates are reusable message layouts so that automated emails always look
consistent and professional.

- Each template has a **Name**, an optional **Document Type** (e.g. Sales Invoice,
  Dunning), a **Subject**, and a **Body**.
- You can insert **placeholders** in curly braces — such as `{customer}`,
  `{invoice_no}`, `{amount}`, `{due_date}` — and the system fills in the real values
  when it sends the message. The body supports HTML formatting.
- Use the **Preview** button to see how a saved template looks with sample data
  before you rely on it.

To add one: open **Settings → Email Templates**, click **New Template**, fill in the
name, subject and body (with any placeholders you need), and click **Save**.

### Notification Logs

**Menu:** Settings → Notification Logs

This is the delivery history for every alert the system has sent. Use it to confirm
that important messages actually went out.

- Each entry shows the **date**, **recipient**, **subject**, **channel**, **status**,
  and the related **document**.
- **Channels:** Email, In-App, Push.
- **Statuses:** Queued, Sent, Failed, Permanently Failed. Failed messages show how
  many times delivery was retried.
- You can **filter** by channel and status, and a red badge highlights how many
  messages have failed so you can follow up.

---

## Authorization Rules

**Menu:** Settings → Authorization Rules

### What it is

Authorization rules are **spending and discount thresholds**. When a transaction goes
above a set limit, it can only be submitted with sign-off from a senior person. This
is closely related to approval rules but focused specifically on **money limits and
discount limits**.

### How it works

- Each rule targets a **Transaction Type** (e.g. Sales Invoice, Purchase Order).
- **Based On** decides what is measured — for example the grand total of the document,
  or the average discount given.
- The **Threshold** is the limit. Any transaction **above** it needs approval.
- The **Approving Role** (or a specific user) is who can authorize it.
- **Scope** controls who the rule applies to:
  - **User-specific** — applies to one particular user
  - **Role** — applies to everyone in a role
  - **Global** — applies to everyone
- Thresholds shown as a percentage (`%`) are discount limits; the rest are money
  limits shown in Ringgit (RM).

If no authorization rules are set up, transactions are not held back by spending
thresholds.

A few built-in safeguards apply: a person cannot approve their own transaction, and
company-specific rules take priority over global ones.

---

## Permissions

Access to these features is controlled by permissions, which an administrator assigns
to roles under **Administration → Identity Management → Roles**. There are two
permission groups:

| Feature | Permission group | Controls |
| --- | --- | --- |
| Approval Rules & Pending Approvals | **Approval Workflows** | Viewing pending approvals and approving/rejecting; creating, editing, and deleting approval rules |
| Authorization Rules | **Approval Workflows** | Viewing and managing spending/discount thresholds |
| Automation Rules | **Automation Rules** | Viewing, creating, editing, and deleting automation rules |
| Email Templates & Notification Logs | **Automation Rules** | Managing templates and reviewing the notification history |

Each group has finer-grained sub-permissions (Create, Edit, Delete), so you can, for
example, let a team leader **approve** documents in Pending Approvals without letting
them **change the rules** themselves.

**A practical split of duties:**

- **Administrators / finance managers** define and maintain the rules (Approval Rules,
  Authorization Rules, Automation Rules, Email Templates).
- **Approvers** (supervisors, managers) only need access to **Pending Approvals** to
  review and sign off on documents raised by their team.
