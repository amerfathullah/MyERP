# E-Invoice (LHDN MyInvois)

## Overview

Malaysia's tax authority, **LHDN** (Lembaga Hasil Dalam Negeri, the Inland Revenue Board), runs a national electronic invoicing system called **MyInvois**. Under this system, businesses no longer treat an invoice as valid the moment it is printed. Instead, each invoice must be submitted electronically to LHDN, **validated** by MyInvois, and stamped with an official identifier before it is considered a compliant tax document. This is a legal requirement that is being rolled out to Malaysian businesses in phases, and it applies to a pharmaceutical-wholesale operation like ours.

The **E-Invoice** module in MyERP handles this for you. When you issue a sales invoice, MyERP can:

1. Package the invoice into the official format LHDN expects.
2. Digitally sign it so LHDN can trust it came from us and has not been tampered with.
3. Send it to MyInvois and record the response.
4. Track the status of every submission and keep a permanent log for audit.

**Why this matters:** submitting to MyInvois is not optional record-keeping — it is how an invoice becomes legally valid for tax purposes. **When you do it:** typically per invoice, once the invoice has been finalised (posted), and always within LHDN's rules (for example, cancellations are only allowed within 72 hours of validation).

You will find the E-Invoice tools in two places:

- The **E-Invoice** menu (left sidebar) — with the **LHDN Dashboard**, **Submission Logs**, and **e-Invoice Status Report**.
- **Settings → E-Invoice (LHDN)** — where the connection to LHDN is configured before anything can be submitted.

---

## Configuration

Before MyERP can submit anything to LHDN, an administrator must connect it to your MyInvois account. This is done once (and updated when credentials change) under **Settings → E-Invoice (LHDN)**.

The settings page has four sections.

### 1. Connection Status

A read-only card at the top that tells you, at a glance, whether MyERP is ready to submit:

- **Environment** — either **Sandbox** (LHDN's testing system) or **Production** (the live, official system). Production is shown in red as a reminder that submissions here are real.
- **Credentials** — whether a Client ID / Secret has been saved.
- **Certificate** — whether a digital signing certificate has been uploaded.
- **Token Expires** — LHDN logs you in with an access token that lasts about an hour; this shows when the current session expires. If it lapses, the status changes to **Token Expired** and you simply reconnect.

### 2. API Credentials

These are the login details LHDN issues to your business through the MyInvois portal.

- **Environment** — choose **Sandbox (Testing)** while trialling, or **Production** once you go live.
- **Client ID** — provided by LHDN.
- **Client Secret** — provided by LHDN. For security, this field can be left blank when editing to keep the existing secret unchanged.

**How to use it:**
1. Select the environment and enter the Client ID and Client Secret.
2. Click **Save**.
3. Click **Connect**. MyERP logs in to LHDN and, if successful, the status card turns to **Connected**.

**Tip:** Always start in **Sandbox** and submit a few test invoices before switching to **Production**. Only move to Production once you have real LHDN production credentials and a valid certificate.

### 3. Digital Certificate

LHDN requires every submitted invoice to carry a **digital signature** (see "Understanding XAdES digital signing" below). To produce that signature, MyERP needs your organisation's signing certificate.

- **Certificate File** — a `.pfx` or `.p12` certificate file.
- **Certificate Password** — the password that protects that file.

**How to use it:** choose the certificate file, enter its password, and click **Upload**. Once uploaded, the Connection Status card shows the certificate as **Uploaded**.

**Tip:** Keep the certificate file and its password confidential. MyERP stores them securely and never displays the password back to you.

### 4. TIN Lookup

Every party on an e-Invoice must be identified by a **TIN** (Tax Identification Number). This tool lets you look up a customer's or supplier's TIN directly against the LHDN database so you can confirm it before you submit an invoice.

**How to use it:**
1. Choose the **ID Type** — Business Registration (BRN), MyKad (NRIC), Passport, or Army ID.
2. Enter the **ID Value** (for example a company registration number).
3. Click **Search**. If found, the taxpayer's TIN and registered name are displayed.

**Tip:** Use this whenever you onboard a new customer, so their invoices sail through validation. For walk-in consumers who do not provide a TIN, LHDN's generic consumer TIN `EI00000000020` is used automatically.

### Understanding XAdES digital signing

**XAdES** is the digital-signature standard LHDN mandates for e-Invoices. In plain terms, when MyERP submits an invoice it seals the document with a cryptographic signature created from your uploaded certificate. This does two things: it proves the invoice genuinely came from your business, and it guarantees the contents were not altered in transit. You do not perform any extra steps for this — as long as a certificate is uploaded, MyERP signs each invoice automatically before sending it.

---

## LHDN Dashboard

**What it is:** a visual summary of all your e-Invoice activity, found under **E-Invoice → LHDN Dashboard**.

**Why it exists:** it gives management an at-a-glance picture of compliance health — how many invoices are validated, how many are stuck, and how many have not yet been submitted — without digging through individual records.

**What you see:**
- **Status summary cards** counting invoices in each state: **Valid**, **Invalid**, **Submitted** (awaiting a result), **Cancelled**, **Failed**, and **Not Submitted**.
- **Current Month Breakdown** — a pie chart of this month's submission outcomes.
- **Sales vs Purchase Submissions** — a bar chart comparing submission outcomes across sales and purchase documents.
- **Sales Submissions** and **Purchase Submissions** panels showing totals, a **Success Rate** (the share of submitted invoices that came back valid), and how many are pending or not yet submitted.

**How to use it:** open the page — it loads automatically. Use it as your daily or weekly check. A rising **Not Submitted** count means invoices are being issued but not sent to LHDN; a rising **Invalid** or **Failed** count means something needs fixing.

**Tip:** The success rate is your quickest compliance indicator. If it drops, review the Submission Logs to see which invoices were rejected and why.

---

## Submitting, Validating, and Cancelling an invoice

All three actions happen from an individual **sales invoice**, not from the E-Invoice menu. Open a sales invoice from **Sales → Invoices** and use the action buttons on its detail page.

### Submit to LHDN

**What it is:** sending a finalised sales invoice to MyInvois for official validation.

**Why it exists:** this is the core compliance step — until an invoice is submitted and validated, it is not a recognised tax document.

**How to use it:**
1. Open the sales invoice. The invoice must be in **Posted** status (draft or submitted invoices are not eligible yet) and must not already have been sent to LHDN.
2. Click **Submit to LHDN**.
3. MyERP validates, signs, and sends the invoice. On success you see a confirmation with the LHDN **UUID** (the unique reference LHDN assigns), and the invoice's e-Invoice status updates.

**Tip:** The **Submit to LHDN** button only appears when the invoice is posted and its e-Invoice status is still "Not Submitted." If you do not see it, check the invoice status first.

### Validate (the automatic pre-check)

**What it is:** a set of built-in compliance checks that MyERP runs **automatically the moment you click Submit to LHDN** — you do not run it as a separate step. If any check fails, the submission is stopped and the reason is shown, so nothing incomplete ever reaches LHDN.

**What it checks:**
- Your **company** has a TIN, an **MSIC** business-activity code, and a registration number (BRN).
- The invoice is in a **Submitted or Posted** state, has at least one line item, and a grand total greater than zero.
- A **buyer TIN** is present (the generic consumer TIN is used for walk-in customers).
- The **document type code** is valid (Invoice, Credit Note, Debit Note, Refund Note, or a self-billed variant).
- A currency is set, and every line item has a positive quantity and a non-negative price.

**Why it exists:** LHDN will reject a non-compliant invoice, so MyERP catches the common problems first and tells you exactly what to fix — saving a round trip.

**Tip:** If a submission fails validation, read the message, correct the invoice or the company/customer master data, and submit again. The **TIN Lookup** in settings is the fastest way to resolve TIN-related failures.

### Cancel

**What it is:** formally cancelling an invoice that has already been validated by LHDN.

**Why it exists:** mistakes happen. LHDN provides a cancellation window so a validated invoice can be voided officially rather than just deleted internally.

**How to use it:** cancellation is submitted to LHDN with a **reason**. LHDN only allows cancellation **within 72 hours** of the invoice being validated, and MyERP enforces this window — after 72 hours you must issue a credit note instead. Cancelling requires the dedicated Cancel permission (see Permissions).

**Tip:** Always record a clear cancellation reason; it is stored with the submission and is visible to LHDN. If you are past the 72-hour window, use a **Credit Note** (document type 02) to correct the customer's account.

---

## Submission Logs

**What it is:** a running list of every invoice that has been sent to LHDN, found under **E-Invoice → Submission Logs**.

**Why it exists:** it is your audit trail. Each submission's identifiers and outcome are kept so you can prove, invoice by invoice, that you met your MyInvois obligations.

**What you see (per row):**
- **Invoice Number**
- **Type** of document
- **LHDN UUID** — the unique reference LHDN assigned
- **Status** — shown as a colour-coded badge (Valid, Invalid, Submitted, Cancelled, Failed, Not Submitted)
- **Submitted** date and time
- A **refresh** button to re-check the latest status with LHDN

**How to use it:** open the page to review recent submissions. Only invoices that have actually been sent to LHDN appear here — anything still "Not Submitted" is filtered out. Use the refresh button on a row that is still **Submitted** (pending) to pull its latest result.

**Tip:** When an invoice shows **Submitted** for a while, it means LHDN has received it but not yet returned a final validation result. Refresh it to see whether it has become **Valid** or **Invalid**.

---

## e-Invoice Status Report

**What it is:** a filterable, tabular report of e-Invoice statuses, found under **E-Invoice → e-Invoice Status Report**.

**Why it exists:** the Submission Logs show recent activity; this report lets you answer specific compliance questions — for example, "show me every sales invoice from last quarter that is still Invalid."

**How to use it:**
1. Set a **From** and **To** date range.
2. Choose **Type** — Sales or Purchase.
3. Choose **Status** — All, Valid, Invalid, Submitted, or Not Submitted.
4. Click **Generate**.

The table lists each matching document with its invoice number, date, party (customer or supplier), amount, LHDN status badge, and submission timestamp.

**Tip:** Filter by **Not Submitted** at month-end to catch any posted invoices that still need to be sent to LHDN before a deadline. Filter by **Invalid** to build a to-do list of invoices that need correcting and resubmitting.

---

## Typical workflow

A normal end-to-end flow for a sales invoice looks like this:

1. **Issue the sales invoice.** Create it under Sales → Invoices, then move it through its normal life cycle to **Posted**.
2. **Submit to LHDN.** On the posted invoice, click **Submit to LHDN**. MyERP automatically runs its validation checks, digitally signs the invoice, and sends it to MyInvois.
3. **Wait for the result.** The invoice's status becomes **Submitted** (pending). LHDN then returns either **Valid** (the invoice is now an official, compliant e-Invoice, with a UUID recorded) or **Invalid** (with a reason).
4. **Fix and resubmit if needed.** If it came back Invalid, correct the invoice or the customer/company data and submit again.
5. **Cancel only if necessary.** If the validated invoice was wrong, cancel it **within 72 hours** with a reason. Past that window, issue a **Credit Note** instead.

Throughout, use the **LHDN Dashboard** to monitor overall health, the **Submission Logs** to track individual submissions, and the **e-Invoice Status Report** for period reviews and audits.

### Status lifecycle at a glance

| Status | Meaning |
|--------|---------|
| **Not Submitted** | Invoice exists in MyERP but has not been sent to LHDN. |
| **Submitted** (Pending) | Sent to LHDN and awaiting a validation result. |
| **Valid** | Accepted and validated by LHDN — an official e-Invoice. |
| **Invalid** | Rejected by LHDN; the reason is recorded. |
| **Cancelled** | A previously validated invoice was cancelled (within 72 hours). |
| **Failed** | The submission itself did not complete (for example, a connection problem). |

---

## Permissions

Access to E-Invoice features is controlled by three permissions, which an administrator grants under **Administration → Identity Management → Roles**:

- **E-Invoice (view)** — see the LHDN Dashboard, Submission Logs, and Status Report, and open E-Invoice settings. This is the base permission required for the whole module.
- **E-Invoice → Submit** — submit invoices to LHDN, save API credentials, connect/authenticate, and upload the signing certificate.
- **E-Invoice → Cancel** — cancel a validated invoice within the allowed window.

**Tip:** Give the **view** permission broadly to finance and management staff who need visibility, but restrict **Submit** and **Cancel** to the specific people responsible for LHDN compliance, since those actions carry legal weight.
