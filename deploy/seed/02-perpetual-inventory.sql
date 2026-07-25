-- Make Purchase Invoice post to Inventory (perpetual) instead of COGS/expense.
-- Original seeded rule: PI DR uses ItemExpense (AccountSource=4) -> hits COGS.
-- For a stock-only pharma distributor we want: PI DR Inventory (asset), CR AP.
-- AccountSource: 0 = FixedAccount. Inventory account = 1140.
UPDATE "Acc_AccountingRules" r
SET "AccountSource" = 0,
    "FixedAccountId" = (SELECT "Id" FROM "Acc_Accounts" WHERE "AccountCode" = '1140' LIMIT 1)
WHERE r."DocumentType" = 'PurchaseInvoice' AND r."IsDebit" = true;

SELECT "DocumentType","Name","IsDebit","AccountSource","FixedAccountId"
FROM "Acc_AccountingRules" WHERE "DocumentType"='PurchaseInvoice';
