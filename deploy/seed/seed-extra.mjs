// ============================================================================
// MyERP — Iraqi pharma demo seed: PERIPHERAL modules
// HR/payroll, CRM, projects, assets, automations, purchasing/inventory extras.
// Run AFTER seed.mjs (needs items/customers/suppliers seeded).
//   node deploy/seed/seed-extra.mjs [stage...]
//   stages: employees hr crm projects assets automation purchasing inventory
// Note: payroll needs employee.BasicSalary — set via 03-employee-salaries.sql
//       between the `employees` and `hr` stages.
// ============================================================================
const API = process.env.API_URL || 'http://localhost:5001';
const USER = process.env.ADMIN_USER || 'admin';
const PASS = process.env.ADMIN_PASS || '1q2w3E*';

let _s = 987654;
const rnd = () => { _s = (_s * 1103515245 + 12345) & 0x7fffffff; return _s / 0x7fffffff; };
const pick = (a) => a[Math.floor(rnd() * a.length)];
const pickUnique = (a, n) => { const p = a.slice(), o = []; n = Math.min(n, p.length); for (let i = 0; i < n; i++) o.push(p.splice(Math.floor(rnd() * p.length), 1)[0]); return o; };
const randInt = (lo, hi) => lo + Math.floor(rnd() * (hi - lo + 1));
const iso = (y, m, d) => `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}T00:00:00Z`;

let TOKEN = null;
async function auth() {
  const body = new URLSearchParams({ grant_type: 'password', username: USER, password: PASS, client_id: 'MyERP_App', scope: 'MyERP offline_access' });
  const r = await fetch(`${API}/connect/token`, { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body });
  if (!r.ok) throw new Error(`auth ${r.status}`);
  TOKEN = (await r.json()).access_token;
}
async function api(method, path, body) {
  const r = await fetch(`${API}/api/app/${path}`, { method, headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
  const txt = await r.text();
  if (!r.ok) throw new Error(`${method} ${path} -> ${r.status}: ${txt.replace(/\s+/g, ' ').slice(0, 240)}`);
  return txt ? JSON.parse(txt) : null;
}
const get = (p) => api('GET', p);
const post = (p, b) => api('POST', p, b);
async function listAll(entity, extra = '') { const r = await get(`${entity}?MaxResultCount=1000${extra}`); return Array.isArray(r) ? r : (r.items || []); }
async function submit(entity, id) { return post(`${entity}/${id}/submit`); }

const R = {};
async function loadRefs() {
  R.companyId = (await listAll('company'))[0].id;
  R.accounts = await listAll('account');
  const acc = (c) => (R.accounts.find(a => a.accountCode === c) || {}).id;
  R.accFixedAsset = acc('1210') || acc('1200') || acc('1230');
  R.accDeprExpense = acc('5500') || acc('5510');
  R.accAccumDepr = acc('1220');
  R.warehouses = await listAll('warehouse');
  R.whMain = (R.warehouses.find(w => w.name === 'Main Distribution Center') || R.warehouses.find(w => !w.isGroup) || {}).id;
  R.whStores = (R.warehouses.find(w => w.name === 'Stores') || {}).id || R.whMain;
  R.items = await listAll('item');
  R.customers = await listAll('customer');
  R.suppliers = await listAll('supplier');
  R.employees = await listAll('employee');
  console.log(`refs: items=${R.items.length} cust=${R.customers.length} sup=${R.suppliers.length} emp=${R.employees.length} fixedAsset=${!!R.accFixedAsset}`);
}

// ---- employees -------------------------------------------------------------
const EMPLOYEES = [
  ['Yasir', 'Al-Obaidi', 'General Manager', 'Management', 2500000],
  ['Huda', 'Al-Azzawi', 'Finance Manager', 'Accounting', 1800000],
  ['Omar', 'Al-Dulaimi', 'Sales Manager', 'Sales', 1600000],
  ['Zainab', 'Hussein', 'Senior Sales Rep', 'Sales', 1100000],
  ['Mustafa', 'Kadhim', 'Sales Rep', 'Sales', 950000],
  ['Rana', 'Al-Jubouri', 'Warehouse Manager', 'Warehouse', 1300000],
  ['Ali', 'Abbas', 'Warehouse Staff', 'Warehouse', 750000],
  ['Sara', 'Naji', 'Accountant', 'Accounting', 1000000],
  ['Hassan', 'Al-Maliki', 'Purchasing Officer', 'Purchasing', 1050000],
  ['Dina', 'Salman', 'HR Officer', 'Human Resources', 1000000],
  ['Karrar', 'Jasim', 'Pharmacist (QA)', 'Quality', 1400000],
  ['Noor', 'Fadhil', 'Delivery Driver', 'Logistics', 700000],
];
async function stageEmployees() {
  const have = new Set((await listAll('employee')).map(e => (e.email || '').toLowerCase()));
  let n = 0;
  for (let i = 0; i < EMPLOYEES.length; i++) {
    const [first, last, desig, dept] = EMPLOYEES[i];
    const email = `${first}.${last}`.toLowerCase().replace(/[^a-z.]/g, '') + '@alrafidain-pharma.iq';
    if (have.has(email)) continue;
    await post('employee', {
      companyId: R.companyId, firstName: first, lastName: last,
      dateOfBirth: iso(1985 + (i % 12), 1 + (i % 12), 1 + (i % 27)),
      dateOfJoining: iso(2022 + (i % 3), 1 + (i % 12), 1 + (i % 27)),
      phone: `+964 77${i} 200 30${String(i).padStart(2, '0')}`, email,
      designation: desig, department: dept, taxNumber: `TIN-EMP-${1000 + i}`,
    });
    n++;
  }
  console.log(`employees: +${n}`);
}

// ---- HR: salary structures, leave allocations, payroll runs ----------------
async function stageHr() {
  // Salary structures (feature visibility)
  const comps = await listAll('salary-component');
  const basic = comps.find(c => /basic/i.test(c.name));
  const housing = comps.find(c => /housing/i.test(c.name));
  const existingStructs = await listAll('salary-structure');
  if (!existingStructs.length && basic) {
    for (const [name, b, h] of [['Staff Structure', 1000000, 250000], ['Management Structure', 2000000, 600000]]) {
      const details = [{ salaryComponentId: basic.id, componentName: basic.name, amount: b, formula: '' }];
      if (housing) details.push({ salaryComponentId: housing.id, componentName: housing.name, amount: h, formula: '' });
      try { await post('salary-structure', { companyId: R.companyId, name, isHourlyBased: false, payrollFrequency: 'Monthly', description: name, details }); } catch (e) { console.log('  struct skip', e.message.slice(0, 80)); }
    }
  }
  // Leave allocations (2026) for each employee
  const emps = await listAll('employee');
  const leaveTypes = await listAll('leave-type');
  const annual = leaveTypes.find(l => /annual/i.test(l.name)) || leaveTypes[0];
  let la = 0;
  if (annual) for (const e of emps) {
    try { await post('leave-allocation', { companyId: R.companyId, employeeId: e.id, leaveTypeId: annual.id, fromDate: iso(2026, 1, 1), toDate: iso(2026, 12, 31), totalLeavesAllocated: 21, carryForwardDays: 0 }); la++; } catch (_) {}
  }
  // Payroll runs Jan..Jun 2026 (submit each)
  let pr = 0;
  for (let m = 1; m <= 6; m++) {
    try {
      const run = await post('payroll', { companyId: R.companyId, year: 2026, month: m });
      try { await submit('payroll', run.id); } catch (_) {}
      pr++;
    } catch (e) { if (pr === 0) console.log('  payroll skip:', e.message.slice(0, 150)); }
  }
  console.log(`hr: structures ok, leave allocations ${la}, payroll runs ${pr}`);
}

// ---- CRM: leads, opportunities, quotations ---------------------------------
async function stageCrm() {
  const LEADS = [
    ['Firas', 'Al-Tamimi', 'Al-Yarmouk Pharmacy', 'Baghdad'], ['Suha', 'Kareem', 'City Care Pharmacy', 'Basra'],
    ['Bassam', 'Nouri', 'Al-Zahra Medical Store', 'Najaf'], ['Rania', 'Hadi', 'Sunrise Pharmacy', 'Erbil'],
    ['Wissam', 'Talib', 'Al-Fardous Pharmacy', 'Baghdad'], ['Lina', 'Saeed', 'Health Plus Pharmacy', 'Kirkuk'],
    ['Ahmed', 'Rashid', 'Al-Shorouk Clinic', 'Mosul'], ['Maryam', 'Jawad', 'Wellness Pharmacy', 'Karbala'],
    ['Tariq', 'Aziz', 'Al-Rayan Medical', 'Baghdad'], ['Shatha', 'Mohsen', 'Green Life Pharmacy', 'Basra'],
  ];
  let nl = 0, no = 0;
  const leadIds = [];
  for (let i = 0; i < LEADS.length; i++) {
    const [f, l, comp, city] = LEADS[i];
    try {
      const lead = await post('lead', { firstName: f, lastName: l, companyName: comp, email: `${f}.${l}@example.iq`.toLowerCase(), phone: `+964 78${i} 400 50${i}`, jobTitle: 'Owner', source: i % 8, city, country: 'Iraq', industry: 'Pharmacy Retail', annualRevenue: randInt(50, 400) * 1000000, companyId: R.companyId, notes: 'Inbound enquiry for wholesale supply' });
      leadIds.push(lead.id); nl++;
    } catch (e) { if (nl === 0) console.log('  lead skip:', e.message.slice(0, 150)); }
  }
  // Opportunities (some standalone, tied to customers)
  for (let i = 0; i < 8; i++) {
    const cust = pick(R.customers);
    try {
      await post('opportunity', {
        title: `Wholesale supply — ${cust.name}`, opportunityType: 0, customerId: cust.id,
        contactName: cust.contactPerson || 'Procurement', salesStage: pick(['Prospecting', 'Qualification', 'Proposal', 'Negotiation']),
        probability: randInt(20, 90), expectedClosingDate: iso(2026, 8 + (i % 4), 15), opportunityAmount: randInt(10, 120) * 1000000,
        currencyCode: 'IQD', companyId: R.companyId, territory: 'Iraq', notes: 'Quarterly framework supply agreement',
      });
      no++;
    } catch (e) { if (no === 0) console.log('  opp skip:', e.message.slice(0, 150)); }
  }
  // Quotations (submit some)
  let nq = 0;
  for (let i = 0; i < 10; i++) {
    const cust = pick(R.customers);
    const chosen = pickUnique(R.items, randInt(2, 5));
    try {
      const q = await post('quotation', {
        companyId: R.companyId, customerId: cust.id, issueDate: iso(2026, 3 + (i % 4), 1 + (i % 25)),
        validUntil: iso(2026, 4 + (i % 4), 1 + (i % 25)), currencyCode: 'IQD', terms: 'Valid 30 days. Prices in IQD.',
        notes: 'Quotation for wholesale supply',
        items: chosen.map(it => ({ itemId: it.id, description: it.itemName, quantity: randInt(10, 80), unitPrice: it.standardSellingPrice, taxAmount: 0, uom: it.uom })),
      });
      if (i % 2 === 0) { try { await submit('quotation', q.id); } catch (_) {} }
      nq++;
    } catch (e) { if (nq === 0) console.log('  quote skip:', e.message.slice(0, 150)); }
  }
  console.log(`crm: leads ${nl}, opportunities ${no}, quotations ${nq}`);
}

// ---- projects --------------------------------------------------------------
async function stageProjects() {
  const PROJECTS = [
    'Cold Chain Expansion — Basra Depot', 'ERP Rollout & Staff Training', 'Warehouse Barcode System',
    'Kurdistan Distribution Network', 'Annual GDP Compliance Audit',
  ];
  let n = 0;
  for (let i = 0; i < PROJECTS.length; i++) {
    try {
      await post('project', {
        projectName: PROJECTS[i], priority: i % 4, percentCompleteMethod: 0, companyId: R.companyId,
        customerId: i % 2 === 0 ? pick(R.customers).id : undefined,
        expectedStartDate: iso(2026, 1 + i, 1), expectedEndDate: iso(2026, 6 + i, 28),
        estimatedCost: randInt(20, 150) * 1000000, notes: 'Strategic initiative for 2026',
      });
      n++;
    } catch (e) { if (n === 0) console.log('  project skip:', e.message.slice(0, 150)); }
  }
  console.log(`projects: ${n}`);
}

// ---- assets ----------------------------------------------------------------
async function stageAssets() {
  // categories
  const cats = [];
  const CAT = [['Vehicles', 60, 6], ['Cold Chain Equipment', 84, 6], ['IT Equipment', 36, 12], ['Office Furniture', 120, 4]];
  for (const [name, life, rate] of CAT) {
    try {
      const c = await post('asset-category', {
        categoryName: name, isDepreciable: true, defaultDepreciationMethod: 0, defaultUsefulLifeMonths: life,
        defaultDepreciationRate: rate, assetAccountId: R.accFixedAsset, depreciationAccountId: R.accDeprExpense,
        accumulatedDepreciationAccountId: R.accAccumDepr,
      });
      cats.push(c.id);
    } catch (e) { console.log('  cat skip:', e.message.slice(0, 120)); }
  }
  if (!cats.length) { console.log('assets: no categories, skip'); return; }
  const ASSETS = [
    ['Delivery Van — Toyota HiAce (Baghdad)', 0, 45000000], ['Delivery Truck — Isuzu NPR (Basra)', 0, 78000000],
    ['Walk-in Cold Room 2-8°C', 1, 32000000], ['Pharma Refrigerators (x6)', 1, 18000000],
    ['Server & Network Rack', 2, 12000000], ['Barcode Scanners & Printers', 2, 6500000],
    ['Warehouse Racking System', 3, 22000000], ['Office Furniture Set', 3, 9000000],
  ];
  let n = 0;
  for (let i = 0; i < ASSETS.length; i++) {
    const [name, catIdx, amount] = ASSETS[i];
    try {
      const a = await post('asset', {
        assetName: name, companyId: R.companyId, assetCategoryId: cats[Math.min(catIdx, cats.length - 1)],
        location: pick(['Baghdad HQ', 'Basra Depot', 'Erbil Depot']), purchaseDate: iso(2025, 6 + (i % 6), 10),
        purchaseAmount: amount, additionalCost: 0, calculateDepreciation: true, depreciationMethod: 0,
        usefulLifeMonths: [60, 84, 36, 120][catIdx], frequencyMonths: 1, availableForUseDate: iso(2025, 6 + (i % 6), 15),
        notes: 'Capital asset',
      });
      try { await submit('asset', a.id); } catch (_) {}
      n++;
    } catch (e) { if (n === 0) console.log('  asset skip:', e.message.slice(0, 150)); }
  }
  console.log(`assets: categories ${cats.length}, assets ${n}`);
}

// ---- automation rules ------------------------------------------------------
async function stageAutomation() {
  const RULES = [
    ['Low Stock Alert', 'Notify purchasing when stock hits reorder level', 0, 'Item', 1],
    ['Overdue Invoice Reminder', 'Email customer when a sales invoice is overdue', 1, 'SalesInvoice', 2],
    ['Large Order Approval', 'Flag sales orders above 20M IQD for review', 0, 'SalesOrder', 3],
    ['New Customer Welcome', 'Send welcome email to new customers', 0, 'Customer', 2],
  ];
  let n = 0;
  for (const [name, desc, trigger, docType, action] of RULES) {
    try {
      await post('automation-rule', { name, description: desc, trigger, documentType: docType, conditionExpression: '', action, actionConfig: '{}', companyId: R.companyId, isActive: true, priority: n + 1 });
      n++;
    } catch (e) { if (n === 0) console.log('  automation skip:', e.message.slice(0, 180)); }
  }
  console.log(`automation: ${n} rules`);
}

// ---- purchasing extras: material request, RFQ, supplier quotation ----------
async function stagePurchasing() {
  let mr = 0, rfq = 0;
  for (let i = 0; i < 6; i++) {
    const chosen = pickUnique(R.items, randInt(2, 5));
    try {
      const m = await post('material-request', {
        companyId: R.companyId, requestType: 0, requestDate: iso(2026, 2 + i, 3), requiredByDate: iso(2026, 2 + i, 20),
        targetWarehouseId: R.whMain, notes: 'Replenishment request',
        items: chosen.map(it => ({ itemId: it.id, itemName: it.itemName, quantity: randInt(50, 300), uom: it.uom, warehouseId: R.whMain })),
      });
      try { await submit('material-request', m.id); } catch (_) {}
      mr++;
    } catch (e) { if (mr === 0) console.log('  MR skip:', e.message.slice(0, 150)); }
  }
  for (let i = 0; i < 4; i++) {
    const chosen = pickUnique(R.items, randInt(2, 4));
    const sups = pickUnique(R.suppliers, 3);
    try {
      await post('request-for-quotation', {
        companyId: R.companyId, transactionDate: iso(2026, 3 + i, 5), currencyCode: 'IQD',
        messageForSupplier: 'Please quote your best price and lead time.',
        items: chosen.map(it => ({ itemId: it.id, description: it.itemName, qty: randInt(100, 500), uom: it.uom })),
        suppliers: sups.map(s => ({ supplierId: s.id, email: s.email || 'sales@supplier.iq' })),
      });
      rfq++;
    } catch (e) { if (rfq === 0) console.log('  RFQ skip:', e.message.slice(0, 150)); }
  }
  console.log(`purchasing: material requests ${mr}, RFQs ${rfq}`);
}

// ---- inventory extras: stock transfer between warehouses -------------------
async function stageInventory() {
  let n = 0;
  for (let i = 0; i < 4; i++) {
    const chosen = pickUnique(R.items, randInt(2, 4));
    try {
      const e = await post('stock-entry', {
        companyId: R.companyId, entryType: 2, postingDate: iso(2026, 3 + i, 12), notes: 'Transfer to Stores',
        items: chosen.map(it => ({ itemId: it.id, quantity: randInt(20, 100), sourceWarehouseId: R.whMain, targetWarehouseId: R.whStores, valuationRate: it.standardBuyingPrice || 0 })),
      });
      try { await submit('stock-entry', e.id); } catch (_) {}
      n++;
    } catch (e) { if (n === 0) console.log('  transfer skip:', e.message.slice(0, 160)); }
  }
  console.log(`inventory: stock transfers ${n}`);
}

// ---- operating expenses: monthly salaries/rent/utilities journal entries ---
// (Salaries booked as JEs because the Payroll API is unavailable on the current
//  demo image; the Payroll module itself works once redeployed with the fix.)
async function stageOpex() {
  const fys = await listAll('fiscal-year');
  const fyId = (fys[0] || {}).id;
  const acc = (c) => (R.accounts.find(a => a.accountCode === c) || {}).id;
  const salaries = acc('5200'), rent = acc('5300'), util = acc('5400'), bank = acc('1120');
  const empBasic = (await listAll('employee')).reduce((s, e) => s + (e.basicSalary || 0), 0) || 15150000;
  let n = 0;
  for (let m = 1; m <= 7; m++) {
    const day = m === 7 ? 20 : 28;
    const lines = [
      { accountId: salaries, amount: empBasic, isDebit: true, description: 'Monthly staff salaries' },
      { accountId: rent, amount: 6000000, isDebit: true, description: 'Warehouse & office rent' },
      { accountId: util, amount: 1800000, isDebit: true, description: 'Electricity, water, telecom' },
      { accountId: bank, amount: empBasic + 6000000 + 1800000, isDebit: false, description: 'Paid from bank' },
    ];
    try {
      const je = await post('journal-entry', { companyId: R.companyId, fiscalYearId: fyId, postingDate: iso(2026, m, day), narration: `Operating expenses — ${['', 'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'][m]} 2026`, lines });
      try { await post(`journal-entry/${je.id}`); } catch (_) {}
      n++;
    } catch (e) { if (n === 0) console.log('  opex skip:', e.message.slice(0, 160)); }
  }
  console.log(`opex: ${n} monthly expense journal entries`);
}

const STAGES = { employees: stageEmployees, hr: stageHr, crm: stageCrm, projects: stageProjects, assets: stageAssets, automation: stageAutomation, purchasing: stagePurchasing, inventory: stageInventory, opex: stageOpex };
const DEFAULT = ['employees', 'hr', 'crm', 'projects', 'assets', 'automation', 'purchasing', 'inventory', 'opex'];

async function main() {
  const run = process.argv.slice(2).length ? process.argv.slice(2) : DEFAULT;
  await auth(); await loadRefs();
  for (const s of run) {
    if (!STAGES[s]) { console.log(`unknown stage ${s}`); continue; }
    try { await STAGES[s](); } catch (e) { console.log(`[${s}] FAILED:`, e.message.slice(0, 200)); }
  }
  console.log('EXTRA SEED COMPLETE');
}
main().catch(e => { console.error('EXTRA FAILED:', e.message); process.exit(1); });
