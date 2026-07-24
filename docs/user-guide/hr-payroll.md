# HR & Payroll

## Overview

The **HR & Payroll** module keeps track of your people and pays them correctly and on time. It holds employee records, manages leave and public holidays, handles staff expense claims and loans, and runs monthly payroll.

Because MyERP is built for a Malaysian business, payroll is **statutory-aware**: every salary run automatically works out the four mandatory Malaysian deductions and employer contributions — **EPF** (Employees Provident Fund / KWSP), **SOCSO** (PERKESO), **EIS** (Employment Insurance System) and **PCB/MTD** (monthly income-tax deduction for LHDN). These rates are **not hard-coded**. They are held in data-driven contribution tables, so when the government gazettes new rates you (or your administrator) update a table instead of waiting for a software change. When a payroll run is submitted, MyERP also **posts the accounting entry** into the general ledger for you, so Finance and HR always agree.

You reach everything in this module from the **HR** menu in the left-hand navigation.

> **A note on the document lifecycle.** Most documents in MyERP move through named stages. In HR & Payroll you will see **Draft → Submitted** (payroll and salary slips), **Draft → Approved → Submitted → Reimbursed** (expense claims), **Open → Approved** (leave), and **Draft → Sanctioned → Disbursed** (loans). Nothing hits the accounts until it is submitted, so a Draft is always safe to review or delete.

---

## Employees

**What it is.** The master record for every member of staff — personal details, employment details, bank details, and Malaysian statutory numbers.

**Why it exists.** Every other feature in this module points back to an employee: leave, expenses, loans and payroll all need a person to attach to. It is also where the pay figure (**Basic Salary**) that drives payroll is stored.

**How to use it.**
1. Go to **HR → Employees**.
2. Click **New** (or open an existing person and click **Edit**).
3. Fill in the details, grouped on the form as:
   - **Personal** — First Name, Last Name, Date of Birth, IC Number / Passport Number, Citizenship (Malaysian, Permanent Resident, or Foreign Worker), phone, email, address.
   - **Employment** — Employee ID, Date of Joining, Designation, Department, and **Status** (Active, Probation, On Leave, Resigned, Terminated).
   - **Bank** — Bank Name and Bank Account Number (used when paying salaries and reimbursements).
   - **Statutory (Malaysia)** — EPF Number, SOCSO Number, Tax Number (income-tax / PCB reference), and **Basic Salary**.
4. Click **Save**.

**Tips.**
- Only employees with **Status = Active** and a **Basic Salary** greater than zero are picked up by a payroll run — check these two fields before month-end.
- **Citizenship** and **Date of Birth** matter for payroll: contribution rules can differ by age band and by citizenship (for example, foreign-worker SOCSO/EIS treatment), and MyERP applies the correct rule automatically.

**A word on data protection (PDPA).** Certain employee fields — **IC / passport number, bank account number, and salary** — are treated as PDPA-sensitive personal data. Access to employee records is controlled by permission, so only authorised HR & Payroll staff can open and edit them. Treat this screen as confidential and never share exports of it outside the HR function.

---

## Leave management

**What it is.** The place employees (and HR on their behalf) apply for time off, and where those requests are approved or rejected. It works together with **Leave Types** (Annual, Sick, Maternity, and so on) and **Leave Allocations** (see the next section) which set the balances people draw down.

**Why it exists.** It gives you one controlled record of who is off and when, checks that the person actually has the balance to take the leave, and — importantly — feeds payroll so that **unpaid leave is deducted from salary automatically**.

**How to use it.**
1. Go to **HR → Leave** to see all applications and their status.
2. Click **Apply** (route `/hr/leave/apply`) to raise a new one.
3. Choose the **Employee**, the **Leave Type**, the **From Date** and **To Date** (tick **Half Day** for a half-day), and add a **Reason**. MyERP calculates the **Total Leave Days**, and can exclude holidays if the leave type is configured that way.
4. Save. The application starts as **Open**.
5. An approver opens the application and clicks **Approve** or **Reject**. On approval the days are deducted from the employee's leave allocation; if an approved leave is later cancelled, the days are restored.

**Tips.**
- MyERP blocks **overlapping** leave applications and checks the available balance, so you cannot accidentally double-book or over-draw (unless the leave type explicitly allows a negative balance).
- Leave types can be paid or unpaid. Days taken under an **unpaid** leave type reduce the payment days in that month's payroll.

---

## Leave Allocations

**What it is.** The yearly leave balance granted to an employee for a specific leave type — for example "14 days Annual Leave for 2026".

**Why it exists.** An application can only draw from what has been allocated. Allocations are also where **carry-forward** from the previous year is recorded.

**How to use it.**
1. Go to **HR → Leave Allocations**.
2. Create an allocation for an employee: pick the **Employee**, **Leave Type**, the **From Date** / **To Date** (the period, usually the calendar or fiscal year), and the **Total Leaves Allocated**.
3. Enter any **Carry Forward Days** brought in from the prior year (and, where relevant, a carry-forward expiry date).
4. Save. Use **Bulk Allocate** to grant the same allocation to many employees at once instead of one by one.

**Tips.**
- The **Balance** shown = allocated + effective carry-forward − used. Carried-forward days that have passed their expiry date automatically stop counting toward the balance.
- Set allocations up at the **start of the year** (or when a new employee joins) so leave applications can be approved without hitting a "no balance" error.

---

## Holiday Lists

**What it is.** A calendar of non-working days for a company (and branch) for a given year — public holidays plus weekly off days such as Saturday/Sunday.

**Why it exists.** The holiday calendar is shared across the system. Leave calculations can exclude holidays, and payroll uses it when working out payment days for the month.

**How to use it.**
1. Go to **HR → Holiday Lists**.
2. Click **New**. Give it a **Name** and a **Year**, and set the **Weekly Off** days (for example "Saturday,Sunday"). Mark one list as the **Default** for the company.
3. Add each public holiday as a line with its **Date** and a **Description** (for example "Hari Raya Aidilfitri").
4. Save.

**Tips.**
- Keep one clearly-named default list per company per year (for example "Malaysia Public Holidays 2026") so leave and payroll pick up the right calendar.
- Remember to include state holidays relevant to where your staff are based.

---

## Expense Claims

**What it is.** A staff reimbursement request — travel, food, accommodation and the like — made up of one or more expense lines, which is then approved and paid back to the employee.

**Why it exists.** It gives expenses an approval trail and a clean link into accounting, and it protects against **double payment** where an employee has already taken a cash advance.

**How to use it.**
1. Go to **HR → Expense Claims**.
2. Click **New**. Choose the **Employee**, the **Posting Date** and the **Expense Type**, then add expense lines (each with a date, description and amount). MyERP totals them into **Total Claimed Amount**. The claim starts as **Draft**.
3. A reviewer opens the claim and clicks **Approve** (which sets the sanctioned amount) — or **Reject** while it is still Draft.
4. Click **Submit** to confirm the approved claim.
5. Click **Reimburse** and choose the account to **pay from**. MyERP creates a **Payment Entry** for the reimbursable amount and marks the claim **Reimbursed**.

**Tips.**
- The reimbursed amount is **claim total − any advance already taken − anything already reimbursed**, so if the employee drew an advance for this trip they are only paid the difference.
- You can only **Reimburse** a claim that has been **Submitted** — approve and submit it first.

---

## Salary Structures

**What it is.** A reusable template that defines how a salary is made up: a list of **components** (earnings and deductions), each a fixed amount or a formula.

**Why it exists.** Rather than re-keying figures for every person every month, you define the pay recipe once — Basic, allowances, statutory deductions — and reuse it.

**How to use it.**
1. Go to **HR → Salary Structures**.
2. Click **New**. Give the structure a **Name** and set the **Payroll Frequency** (Monthly by default).
3. Add component lines. Each references a **Salary Component** (for example Basic, Housing Allowance, EPF Employee, PCB) and is either:
   - a **fixed Amount**, or
   - a **Formula** using component abbreviations — for example `B * 0.11` means 11% of Basic.
4. Mark the structure **Active** and Save.

**Tips.**
- Components are tagged as **Earning** or **Deduction**, and statutory ones (EPF, SOCSO, EIS, PCB) are flagged as **statutory** so they are reported separately on the payslip.
- Components that **depend on payment days** are automatically prorated when an employee has unpaid leave in the month.

---

## Salary Slips

**What it is.** An individual payslip for one employee for one pay period, listing that person's **earnings** and **deductions** and the resulting **net pay**.

**Why it exists.** It is the detailed, per-employee record behind a payroll run — what you hand to (or make available to) each employee, and the audit record of exactly how their net pay was reached.

**How to use it.**
1. Go to **HR → Salary Slips** to see the list.
2. Open a slip to view its **Earnings** and **Deductions** sections, the **Gross Amount**, **Total Deductions**, **Net Amount**, and the working-days / payment-days figures.
3. A Draft slip can be **Submitted** to finalise it (and, if needed, **Cancelled**).

**Tips.**
- Salary slips are normally produced as part of a payroll run rather than hand-built one at a time — see **Running payroll** below.
- Statutory lines (EPF/SOCSO/EIS/PCB) are shown as distinct deduction lines so employees can see each contribution clearly.

---

## Payroll

**What it is.** A **payroll run** for one month — it gathers every active employee, calculates gross pay, all four statutory deductions and employer contributions, applies any loan instalment, and produces the totals for the period.

**Why it exists.** This is the engine of the module. It applies Malaysia's statutory rules consistently across everyone, and on submission it pushes the accounting entry into the general ledger so payroll is reflected in the accounts.

**How the statutory calculation works.** For each employee MyERP looks up the applicable **contribution rule** for EPF, SOCSO, EIS and PCB based on the pay period, the employee's salary, age and citizenship. Each rule holds an **employee rate**, an **employer rate** and an optional **salary ceiling**, and is valid between effective dates. Because the rates live in these tables, statutory changes are handled by updating the tables — never by changing figures inside a payslip.

**How to use it.**
1. Go to **HR → Payroll**.
2. Click **New** and choose the **Year** and **Month**. MyERP then:
   - pulls in every **Active** employee that has a Basic Salary,
   - prorates pay for any **unpaid leave** taken that month,
   - calculates **EPF, SOCSO, EIS and PCB** (employee and employer sides), and
   - auto-deducts the **loan instalment (EMI)** for anyone with an active disbursed loan.
   The run is created as **Draft** with a line per employee.
3. Review the totals: **Total Gross Salary**, **Total Deductions**, **Total Net Salary**, and **Total Employer Contributions**.
4. When you are satisfied, click **Submit**. See the mini-guide below for what submission does.

**Tips.**
- Check that new joiners are **Active** and leavers are **Resigned/Terminated** before you run payroll — status drives who is included.
- Nothing reaches the accounts while the run is **Draft**, so review freely first.

---

## Loans

**What it is.** Employee loans, with a repayment schedule, interest, and outstanding balance tracked over time. Instalments can be recovered automatically through payroll.

**Why it exists.** It lets you give staff advances or loans in a controlled way, calculate instalments correctly (two interest methods), and recover them month by month without manual tracking.

**How to use it.**
1. Go to **HR → Loans** and click **New**.
2. Enter the **Employee**, **Loan Amount**, **Annual Interest Rate**, **Tenure (months)**, and choose the **Interest Method**:
   - **Diminishing Balance** — interest on the reducing balance (standard EMI), or
   - **Flat Rate** — interest on the original principal across the whole tenure.
   Optionally set a **Grace Period** (interest-only months).
3. Click **Sanction** to approve the loan.
4. Click **Disburse**, entering the **Disbursement Date** and **Repayment Start Date**. MyERP then calculates the **EMI** and generates the full **repayment schedule**.
5. Repayments are recorded automatically as each payroll run deducts the instalment; you can also record a repayment manually on the loan.

**Tips.**
- Once a loan is **Disbursed**, its EMI is deducted in every subsequent payroll run until the outstanding balance reaches zero (**Fully Repaid**). Cancelling a payroll run reverses that month's loan repayment.
- The last instalment absorbs any rounding difference, so the loan clears to exactly zero.

---

## Running payroll (mini-guide)

Do these steps in order each pay period:

1. **Prepare the setup (once, then maintain).**
   - Confirm **Salary Structures** and their components are in place (**HR → Salary Structures**).
   - Confirm the **Holiday List** for the year is set (**HR → Holiday Lists**).
   - Confirm the **contribution tables** for EPF/SOCSO/EIS/PCB are current (ask your administrator if rates changed).
2. **Check employees.** In **HR → Employees**, make sure everyone to be paid is **Active** with a **Basic Salary**, and leavers are marked **Resigned/Terminated**.
3. **Create the payroll run.** **HR → Payroll → New**, choose **Year** and **Month**. MyERP builds a Draft with one line per employee, including statutory deductions, unpaid-leave proration and loan EMIs.
4. **Review.** Check the per-employee lines and the run totals while the run is still **Draft**.
5. **Submit.** Click **Submit** on the payroll run. On submission MyERP:
   - **posts a journal entry** to accounting — **Debit Salary Expense** (gross pay + employer contributions) and **Credit** Salary Payable, EPF Payable, SOCSO Payable, EIS Payable and PCB Payable; and
   - **records loan repayments** for anyone with an EMI deduction that month.
6. **Salary slips.** Use **HR → Salary Slips** to review each employee's payslip for the period.
7. **Pay out.** Settle the net salaries and the statutory payables (KWSP, PERKESO, LHDN) from Accounting.

> If you spot a mistake after submitting, use **Cancel** on the payroll run. MyERP reverses the accounting entry and the loan repayments for that run so you can correct and re-run.

---

## Permissions

Access to HR & Payroll is controlled by two permission groups, granted by your administrator to the **HR & Payroll Officer** role (menu items only appear if you hold the matching permission).

| Permission | Grants access to |
|---|---|
| **Employees** (`MyERP.Employees`) | View employees, and the Leave, Leave Allocations, Holiday Lists, Loans and Expense Claims screens |
| **Employees → Create / Edit / Delete** | Add, change or remove employee records, apply/approve leave, create allocations, manage loans and expense claims |
| **Payroll** (`MyERP.Payroll`) | View payroll runs, Salary Structures and Salary Slips |
| **Payroll → Create** | Create a payroll run |
| **Payroll → Submit** | Submit a payroll run (which posts the journal entry) |
| **Payroll → Cancel** | Cancel a submitted payroll run (which reverses the accounting) |

Notes:
- **Reimbursing** an expense claim also needs the accounting **Payment Entry → Create** permission, because it creates a payment.
- Because employee records contain **PDPA-sensitive** data (IC/passport, bank account, salary), grant the Employees and Payroll permissions only to staff who genuinely need them.
