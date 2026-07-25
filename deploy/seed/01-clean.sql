-- ============================================================================
-- MyERP demo reset — clean business data, keep users/roles + framework master
-- ----------------------------------------------------------------------------
-- Keeps: AbpUsers/Roles/Permissions/Settings, OpenIddict clients, EF history,
--        and the seeded master data (Chart of Accounts, cost centers, fiscal
--        year, accounting rules, currency exchanges, dimensions, warehouses,
--        price lists, UOMs, item groups, tax categories/rules, payroll master,
--        the company row + customer/supplier groups + territories).
-- Wipes:  every transactional/business/entity table (documents, ledgers, items,
--        batches, parties, employees, series, CRM/projects/assets/mfg, etc.).
-- Also:   flips the company + price lists to IQD (Iraqi Dinar) and relabels the
--        company to the Iraqi pharma distributor used by the demo dataset.
-- Idempotent: safe to run repeatedly.
-- ============================================================================
DO $$
DECLARE
  keep text[] := ARRAY[
    -- Core / org master
    'AppCompanies','AppCustomerGroups','AppSupplierGroups','AppTerritories',
    'AppEmailTemplates','AppAuthorizationRules',
    -- Accounting master
    'Acc_Accounts','Acc_AccountingRules','Acc_CostCenters','Acc_FiscalYears',
    'Acc_ModesOfPayment','Acc_PaymentTermsTemplates','Acc_PaymentTerms',
    'Acc_CurrencyExchanges','Acc_FinanceBooks','Acc_Dimensions','Acc_DimensionFilters',
    'Acc_GlDimensionValues','Acc_AccountingPeriods',
    -- Inventory master
    'Inv_ItemGroups','Inv_Uoms','Inv_UomConversions','Inv_Warehouses','Inv_PriceLists',
    -- HR master
    'Hr_LeaveTypes','Hr_SalaryComponents','Hr_ContributionRules','Hr_HolidayLists','Hr_Holidays',
    -- Tax + Mfg master
    'Tax_Categories','Tax_Rules','Mfg_Settings'
  ];
  tbls text;
BEGIN
  SELECT string_agg(format('%I', table_name), ', ')
  INTO tbls
  FROM information_schema.tables
  WHERE table_schema = 'public'
    AND table_type = 'BASE TABLE'
    AND table_name NOT LIKE 'Abp%'
    AND table_name NOT LIKE 'OpenIddict%'
    AND table_name <> '__EFMigrationsHistory'
    AND NOT (table_name = ANY(keep));

  EXECUTE 'TRUNCATE TABLE ' || tbls || ' RESTART IDENTITY CASCADE';
END $$;

-- Relabel the single demo company to the Iraqi pharma distributor + IQD
UPDATE "AppCompanies" SET
  "Name"        = 'Al-Rafidain Pharmaceutical Distribution Co.',
  "ShortName"   = 'Al-Rafidain Pharma',
  "CurrencyCode"= 'IQD',
  "Country"     = 'Iraq',
  "City"        = 'Baghdad',
  "State"       = 'Baghdad',
  "Phone"       = '+964 780 123 4567',
  "Email"       = 'info@alrafidain-pharma.iq',
  "Website"     = 'https://alrafidain-pharma.iq',
  "Address"     = 'Al-Karrada, Baghdad, Iraq';

-- All price lists to IQD (they were seeded in MYR by the old image)
UPDATE "Inv_PriceLists" SET "CurrencyCode" = 'IQD';

-- Report what survived
SELECT 'company'    AS entity, "Name" AS detail FROM "AppCompanies"
UNION ALL SELECT 'currency', "CurrencyCode" FROM "AppCompanies"
UNION ALL SELECT 'accounts',   count(*)::text FROM "Acc_Accounts"
UNION ALL SELECT 'pricelists', count(*)::text FROM "Inv_PriceLists"
UNION ALL SELECT 'warehouses', count(*)::text FROM "Inv_Warehouses"
UNION ALL SELECT 'users',      count(*)::text FROM "AbpUsers"
UNION ALL SELECT 'items(should be 0)',     count(*)::text FROM "Inv_Items"
UNION ALL SELECT 'sales_inv(should be 0)', count(*)::text FROM "Sal_SalesInvoices";
