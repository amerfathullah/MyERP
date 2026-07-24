# Accounting

## Overview

The Accounting module is the financial heart of MyERP. It keeps your company's
books so that every sale, purchase, payment and bank movement is recorded once,
correctly, and in a way the auditors and LHDN will accept.

MyERP uses **double-entry accounting**. In plain terms: money never appears or
disappears — it only moves. Every transaction has two sides that must be equal:
a **debit** on one account and a matching **credit** on another. When you sell
RM 1,000 of goods, the system debits "Accounts Receivable" (the customer owes
you) and credits "Sales Revenue" (you earned income) — RM 1,000 on each side.
Because the two sides always balance, your books stay in balance automatically.

You rarely have to work out the debits and credits yourself. When you post a
sales invoice, a purchase invoice or a payment, MyERP's **accounting rules
engine** looks up the correct accounts and produces a balanced **Journal Entry**
for you behind the scenes. You mostly review reports and handle the exceptions
(bank reconciliation, month-end, adjustments). This guide walks through the
whole module, grouped the way you actually work: **Setup**, **Daily
transactions**, **Bank**, **Period-end**, and **Reports**.

> **Where to find it:** everything below lives under the **Accounting** menu in
> the left navigation.

---

## Setup

Do these once (usually at go-live) and then only touch them occasionally.

### Chart of Accounts

**What it is.** The master list of every account your business posts to — cash,
bank, receivables, sales, expenses, and so on. It is organised as a **tree**:
broad groups at the top (e.g. *Assets*), with detailed posting accounts
underneath (e.g. *Maybank Current Account*).

**Why it exists.** Every ringgit that moves has to land in an account. The Chart
of Accounts is the filing cabinet that makes your reports meaningful. A ready-made
**Malaysian Chart of Accounts is seeded automatically for each company**, so you
usually start with a sensible structure already in place.

Each account has one of **five account types**, which drives where it appears on
your reports:

| Account type | Appears on | Examples |
|---|---|---|
| Asset | Balance Sheet | Cash, Bank, Accounts Receivable, Inventory |
| Liability | Balance Sheet | Accounts Payable, Tax Payable, Loans |
| Equity | Balance Sheet | Share Capital, Retained Earnings |
| Revenue | Profit & Loss | Sales Revenue, Other Income |
| Expense | Profit & Loss | Cost of Goods Sold, Rent, Salaries |

**How to use it**

1. Go to **Accounting → Chart of Accounts**.
2. Browse the tree to see how accounts are grouped.
3. To add an account, click **New**, then fill in:
   - **Account Code** and **Account Name**
   - **Account Type** (one of the five above) and, optionally, a **Sub Type**
     (e.g. *Bank Account*, *Accounts Receivable*)
   - **Parent Account** — which group it sits under
   - **Is Group** — tick this only for a heading that other accounts sit under
     (group accounts cannot receive postings directly)
   - **Currency** — leave blank to use the company default (MYR)
4. Save.

**Tips**

- A **group** account is a folder; only **non-group (leaf)** accounts can be
  posted to.
- Tick **Is Frozen** on an account to stop any new entries hitting it (useful
  for accounts you are retiring).
- Prefer editing the seeded Malaysian accounts to renaming them wholesale — it
  keeps your reports tidy.

### Fiscal Years

**What it is.** The accounting year for your company — a start date and an end
date (for example 1 Jan 2026 to 31 Dec 2026).

**Why it exists.** Every Journal Entry belongs to a fiscal year. Fiscal years let
you report per year and, at year-end, **lock** the year so no one can change
history.

**How to use it**

1. Go to **Accounting → Fiscal Years**.
2. Click **New**, enter a **Name** (e.g. "2026"), a **Start Date** and an
   **End Date**.
3. Save. Create the next year before it begins so postings never fall outside a
   defined year.

**Tips**

- A fiscal year marked **Closed** blocks further changes — do this only after
  year-end is finalised (see *Period Closing*).
- Keep years contiguous (no gaps, no overlaps).

### Accounting Dimensions

**What it is.** Extra labels you can attach to transactions so you can slice
reports by things beyond the account — for example **Branch**, **Department**,
**Region** or **Project**.

**Why it exists.** You might want the Profit & Loss for the Penang branch only,
or expenses per department. Dimensions add those tags to every entry without
cluttering your Chart of Accounts with dozens of near-duplicate accounts.

**How to use it**

1. Go to **Accounting → Accounting Dimensions**.
2. Click **New** and choose the **Reference Document Type** the dimension points
   to (e.g. *Branch*), and a **Label** shown on forms.
3. Set **Mandatory** if every entry must carry this dimension.
4. Save. New transactions now show the extra field, and reports can filter/group
   by it.

**Tips**

- Start with one or two dimensions (Branch, Cost Center). Too many mandatory
  dimensions slows down data entry.

### Opening Balances

**What it is.** The starting figures you bring in from your old system on the day
you go live (bank balances, unpaid customer invoices, unpaid supplier bills, and
so on).

**Why it exists.** MyERP can't know last year's numbers. Opening balances seed
those go-live figures so your very first Balance Sheet is correct.

**How to use it**

1. Go to **Accounting → Opening Balance**.
2. Enter the balance for each account **as at your go-live date**. Debit balances
   in the debit column, credit balances in the credit column.
3. Confirm the total debits equal total credits, then save/post.

**Tips**

- Do this **after** the Chart of Accounts is finalised but **before** you start
  entering live transactions.
- The temporary "Opening Balance" equity account is the balancing account for
  any difference while you load figures — it should be zero when you're done.

---

## Daily transactions

The everyday work. Most of these post to the ledger automatically.

### Journal Entries

**What it is.** A manual, balanced accounting entry — the raw form of a
double-entry transaction. You pick the accounts and the debit/credit amounts
yourself.

**Why it exists.** Most postings are created for you by invoices and payments.
Journal Entries cover everything else: accruals, depreciation, corrections,
reclassifications, and opening adjustments.

**Lifecycle:** a Journal Entry is **Created (Draft) → Posted**. While it is a
draft you can edit it; once **Posted** it affects the ledger and can only be
reversed by **Cancelling** it.

**How to use it**

1. Go to **Accounting → Journal Entry** and click **New**.
2. Set the **Posting Date** (this decides the fiscal year and period).
3. Add lines. On each line choose an **Account** and enter an amount in either
   the **Debit** or the **Credit** column. Add a line **Description** if helpful.
4. For receivable/payable/equity lines you can attach a **Party** (customer or
   supplier).
5. Watch the **Total Debit** and **Total Credit** totals — they must be equal.
   MyERP will not post an unbalanced entry.
6. Add a **Narration** explaining the entry, then **Post**.

**Tips**

- If **Post** is greyed out or errors, your debits and credits don't match, or
  you have no lines.
- You cannot post into a **closed period** (see *Period Closing*) or onto a
  **frozen** account.
- To fix a posted entry, **Cancel** it and create a fresh one — history is never
  silently overwritten.

### Payment Entries

**What it is.** A record of money received from a customer or paid to a supplier
(or a straight bank/cash movement).

**Why it exists.** Payments settle invoices. A Payment Entry moves money between
a **Paid From** and a **Paid To** account and reduces the outstanding balance on
the invoices it is matched to.

**Lifecycle:** **Draft → Submitted → Posted** (and **Cancelled** if you need to
reverse it). Posting is what updates the ledger and the invoice's outstanding
amount.

**How to use it**

1. Go to **Accounting → Payment Entry** and click **New**.
2. Choose the **Payment Type** (Receive or Pay).
3. Choose the **Party Type** (Customer/Supplier) and the **Party**.
4. Set the **Mode of Payment** (Cash, Bank Transfer, Cheque, Online) and the
   **Paid From** / **Paid To** accounts.
5. Enter the **Paid Amount** and, for a cheque or transfer, the
   **Reference Number**.
6. **Allocate** the amount against one or more outstanding invoices. Anything you
   don't allocate becomes an **Unallocated Amount** (an advance / on-account
   payment).
7. **Submit**, then **Post**.

**Tips**

- Paying in a foreign currency? Enter the **Exchange Rate**. MyERP works out any
  **exchange gain/loss** automatically from the difference between the payment
  rate and the invoice rate.
- Leave an amount unallocated to record a **customer deposit / advance**; you can
  apply it to an invoice later via *Payment Reconciliation*.

### Batch Payment

**What it is.** A way to create **many Payment Entries in one go** — a supplier
payment run, or allocating one bank deposit across several customer invoices.

**Why it exists.** Paying fifty supplier invoices one at a time is slow. Batch
Payment handles them together.

**How to use it**

1. Go to **Accounting → Batch Payment**.
2. Filter the outstanding invoices you want to settle (by party, due date, etc.).
3. Choose a mode:
   - **Grouped** — one Payment Entry per party (combines that party's invoices).
   - **Ungrouped** — one Payment Entry per invoice (simplest to reconcile later).
4. Review the amounts, then generate the payments. They are created as Payment
   Entries you can submit and post.

**Tips**

- Use **Grouped** when you send one payment/cheque per supplier; use
  **Ungrouped** when each invoice is paid separately.

---

## Bank

Tools for matching your books to what the bank actually did.

### Bank Statement Import

**What it is.** Uploads your bank statement (CSV/Excel) so its lines become
**Bank Transactions** inside MyERP.

**Why it exists.** It saves keying every line by hand and gives reconciliation
something to match against.

**How to use it**

1. Go to **Accounting → Bank Statement Import**.
2. Select the **Bank Account** the statement belongs to.
3. Upload the file and **map the columns** (date, description, deposit,
   withdrawal, reference).
4. Import. Each row becomes a Bank Transaction marked *not yet reconciled*.

**Tips**

- Import in date order and avoid overlapping date ranges so you don't create
  duplicate transactions.

### Bank Transaction Rules

**What it is.** Rules that automatically categorise or match imported bank
transactions based on their description, amount range or type (deposit vs
withdrawal).

**Why it exists.** Recurring lines — bank charges, standing orders, card
settlements — can be classified automatically instead of by hand every month.

**How to use it**

1. Go to **Accounting → Bank Transaction Rules**.
2. Click **New** and give the rule a **Name**.
3. Set the matching conditions: description **contains / starts with / ends with
   / regex**, an optional **amount range**, and a **transaction type**
   (Any / Deposit / Withdrawal).
4. Choose what matching transactions should be **classified as**.
5. Save. Rules run in **priority order (lowest number first)** — the first match
   wins.

**Tips**

- Order matters: put your most specific rules at a lower priority number so they
  are tried first.
- Disable a rule (untick **Enabled**) instead of deleting it if you only need it
  paused.

### Bank Reconciliation

**What it is.** The process of matching each **Bank Transaction** to the
**Payment Entry** (or other document) that represents it, until your book balance
agrees with the bank's balance.

**Why it exists.** It's the single best control against errors and fraud, and
it's the number-one month-end task. It catches missing payments, bank charges you
forgot, and duplicates.

**How to use it**

1. Go to **Accounting → Bank Reconciliation**.
2. Select the **Bank Account** and the statement period.
3. MyERP **auto-suggests matches** between bank lines and your payments. Confirm
   the ones that look right.
4. For unmatched bank lines, either link them to an existing payment or create a
   new Payment Entry / Journal Entry (e.g. for bank charges or interest).
5. Continue until every line is matched and the **difference is zero**.

**Tips**

- Bank charges and interest usually have **no** matching payment yet — create a
  small Journal Entry for them from this screen.
- Set up **Bank Transaction Rules** first to cut the manual matching down.

### Payment Reconciliation

**What it is.** Matches **unallocated payments and credit notes** to the invoices
they should settle — the accounts-side counterpart to bank reconciliation.

**Why it exists.** Advances, on-account receipts and returns often sit
unallocated. This screen clears them against the right invoices so customer and
supplier balances are accurate.

**How to use it**

1. Go to **Accounting → Payment Reconciliation**.
2. Choose the **Party Type** and **Party**.
3. MyERP lists the party's **unallocated payments** on one side and their
   **outstanding invoices** on the other.
4. Allocate payments to invoices (the system suggests a first-fit allocation),
   then **Reconcile**.

**Tips**

- Any exchange difference on foreign-currency settlements is booked
  automatically as gain/loss.
- Run this after a **Batch Payment** or after recording customer deposits.

---

## Period-end

Run these when closing a month or a year.

### Exchange Rate Revaluation

**What it is.** Restates your **foreign-currency** balance-sheet accounts (foreign
bank accounts, foreign receivables/payables) at the period-end exchange rate.

**Why it exists.** Rates move. At period-end, accounting standards require you to
show foreign balances at the current rate and record the **unrealised exchange
gain or loss** — the paper profit/loss you'd have if you converted today.

**How to use it**

1. Go to **Accounting → Exchange Rate Revaluation**.
2. Set the **Posting Date** (usually the period-end date).
3. Choose the **Exchange Gain/Loss account** that the difference should post to.
4. MyERP lists each foreign-currency account, its current rate and the
   revaluation amount. Review the **Total Gain/Loss**.
5. Submit — the system posts the balancing Journal Entry(ies) automatically.

**Tips**

- Run this **before** Period Closing so the closing figures include the
  revaluation.

### Period Closing

**What it is.** Two related actions: **locking a period** so no one can post into
it, and running a **Period Closing Voucher** at year-end that sweeps all Profit &
Loss balances into Retained Earnings.

**Why it exists.** Once a month or year is reviewed and reported, you must stop
changes to it. The closing voucher also resets income and expense accounts to
zero so the new year starts fresh, carrying last year's profit into equity.

**How to use it**

1. Go to **Accounting → Period Closing**.
2. To **lock** a period: set the period dates and mark it **Closed**. You can
   close it for **all document types** or only specific ones (e.g. close
   Sales/Purchase Invoices but still allow Journal Entries).
3. To run the **year-end closing voucher**: choose the **Fiscal Year**, the
   **Closing (Retained Earnings) account** and the closing date, then post. MyERP
   moves every P&L account's balance into that account.

**Tips**

- Only users with the **exempted role** can post into a closed period — keep that
  role tight.
- Reopen a period only if you genuinely need to correct something, and re-run
  affected reports afterwards.
- Close only after bank reconciliation and revaluation are done.

### Budgets & Budget Variance

**What it is.** Spending limits set per account (and optionally per cost
center/project/dimension) for a fiscal year, plus a report that compares budget
to actual.

**Why it exists.** Budgets keep spending under control. MyERP can **warn** or
**stop** a transaction that would breach the budget, at three levels: Material
Request, Purchase Order, and actual expense.

**How to use it**

1. Go to **Accounting → Budgets** and click **New**.
2. Choose the **Fiscal Year** and what the budget is **against** (cost center,
   project or dimension).
3. Add each **account** and its **budget amount**.
4. Set the action when the budget is exceeded — **Ignore**, **Warn** or **Stop**
   — for the annual and the accumulated-monthly limits.
5. Save.
6. To review performance, go to **Accounting → Reports → Budget Variance** and
   pick the fiscal year.

**Tips**

- Use **Warn** while people get used to budgets; switch to **Stop** once the
  numbers are trusted.

---

## Reports

Where you read the story your books are telling. Open these under **Accounting →
Reports** (some sit directly under the Accounting menu). Most take a **date
range**, a **company**, and often an account or party filter.

### Trial Balance

- **What it shows.** Every account with its total debit and credit balance, with
  the grand totals of each column. The two totals must be equal.
- **When to run it.** First thing at month-end — if it doesn't balance, something
  is wrong before you look at anything else.

### Profit & Loss

- **What it shows.** Revenue minus expenses over a period, ending in net
  profit/loss. Built from your Revenue and Expense accounts.
- **When to run it.** Monthly and at year-end to see whether you made money, and
  to compare periods or branches (via dimensions).

### Balance Sheet

- **What it shows.** What you own (Assets), what you owe (Liabilities) and the
  owners' stake (Equity) **as at a date**. Assets = Liabilities + Equity.
- **When to run it.** At period-end, and whenever you need a snapshot of the
  business's financial position (e.g. for the bank).

### General Ledger

- **What it shows.** Every individual posting to an account over a period, with a
  running balance — the detailed audit trail behind every report number.
- **When to run it.** When you need to explain or trace a figure — "why is this
  account this amount?" Drill in by account, party and dimension.

### Aging Report

- **What it shows.** Outstanding customer (receivable) or supplier (payable)
  balances **bucketed by how overdue they are** (e.g. 0–30, 31–60, 61–90, 90+
  days).
- **When to run it.** Weekly for collections chasing, and at month-end to assess
  bad-debt risk.

### Outstanding Invoices

- **What it shows.** A straight list of invoices not yet fully paid, with the
  amount still due on each.
- **When to run it.** Before a collection call or a payment run — it feeds
  naturally into **Batch Payment** and **Payment Reconciliation**.

### Party Ledger

- **What it shows.** All transactions for a single customer or supplier —
  invoices, payments, credit notes — with a running balance.
- **When to run it.** When a customer or supplier queries their account, or
  before you agree a balance with them.

### Statement of Accounts

- **What it shows.** A formatted, shareable account statement for a party over a
  period (opening balance, transactions, closing balance) — the document you send
  to the customer.
- **When to run it.** Month-end statement runs, or on request from a party.

### Cash Flow

- **What it shows.** Where cash came from and where it went over a period, grouped
  into operating, investing and financing activities.
- **When to run it.** At period-end to understand liquidity — profit on the P&L
  doesn't always mean cash in the bank.

> **Related report:** **Currency Exchange** (**Accounting → Currency Exchange**)
> is where you maintain the exchange rates MyERP uses to convert foreign-currency
> transactions and to run the revaluation above.
>
> **Invoice Discounting** (**Accounting → Invoice Discounting**) records selling
> unpaid customer invoices to a bank for early cash. It follows its own
> lifecycle — **Draft → Sanctioned → Disbursed → Settled** — and posts the
> matching bank, loan and discount-charge entries at each step.

---

## Month-end checklist

A short routine to close a month cleanly:

1. **Enter everything** — all sales invoices, purchase invoices and payments for
   the month are posted.
2. **Import bank statements** and run **Bank Reconciliation** for every bank
   account until each difference is zero.
3. **Payment Reconciliation** — clear unallocated payments, advances and credit
   notes against invoices.
4. **Exchange Rate Revaluation** — if you hold any foreign-currency balances.
5. **Trial Balance** — confirm it balances.
6. Review **Profit & Loss**, **Balance Sheet** and **Aging**.
7. **Period Closing** — lock the month once you're happy.
8. At **year-end** only: run the **Period Closing Voucher** and mark the
   **Fiscal Year** closed.

---

## Permissions

Accounting is controlled by permissions, normally bundled into an **Accountant**
role. Grant only what each person needs:

| Area | Permission | What it allows |
|---|---|---|
| Chart of Accounts | Accounts (Create / Edit / Delete) | Manage accounts |
| Journal Entries | JournalEntries.Create | Create draft journal entries |
| Journal Entries | JournalEntries.Post | Post entries to the ledger |
| Payment Entries | PaymentEntries.Create / Edit / Delete | Manage payments |
| Payment Entries | PaymentEntries.Submit / Cancel | Submit/post and reverse payments |
| Budgets | Budgets (Create / Edit / Delete) | Manage budgets |

**Tips**

- A common split is to let clerks **Create** journal entries and payments while
  only a senior accountant can **Post / Submit** them — a simple maker-checker
  control.
- Posting into a **closed period** additionally requires the period's
  **exempted role**, regardless of the permissions above.
- Reports generally follow the permission of the data they show; give read access
  to the accounts and reports a role needs, and nothing more.
