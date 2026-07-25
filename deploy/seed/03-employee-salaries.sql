-- Payroll computes from Employee.BasicSalary, which the create DTO doesn't expose.
-- Set realistic monthly IQD basic salaries by designation for the demo staff.
UPDATE "Hr_Employees" SET "BasicSalary" = CASE "Designation"
  WHEN 'General Manager'     THEN 2500000
  WHEN 'Finance Manager'     THEN 1800000
  WHEN 'Sales Manager'       THEN 1600000
  WHEN 'Senior Sales Rep'    THEN 1100000
  WHEN 'Sales Rep'           THEN 950000
  WHEN 'Warehouse Manager'   THEN 1300000
  WHEN 'Warehouse Staff'     THEN 750000
  WHEN 'Accountant'          THEN 1000000
  WHEN 'Purchasing Officer'  THEN 1050000
  WHEN 'HR Officer'          THEN 1000000
  WHEN 'Pharmacist (QA)'     THEN 1400000
  WHEN 'Delivery Driver'     THEN 700000
  ELSE 900000 END
WHERE "BasicSalary" IS NULL OR "BasicSalary" = 0;

SELECT count(*) AS employees_with_salary FROM "Hr_Employees" WHERE "BasicSalary" > 0;
