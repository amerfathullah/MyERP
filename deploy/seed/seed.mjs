// ============================================================================
// MyERP — Iraqi pharma demo seed (Al-Rafidain Pharmaceutical Distribution Co.)
// Currency: IQD (primary) / USD (secondary). ~6 months of data across modules.
// Idempotent-ish: skips master entities that already exist by code/name.
// Run:  node deploy/seed/seed.mjs [stage...]
//   stages: series items parties opening purchases sales payments  (default: all core)
// Env:  API_URL (default http://localhost:5001)  ADMIN_USER  ADMIN_PASS
// ============================================================================
const API = process.env.API_URL || 'http://localhost:5001';
const USER = process.env.ADMIN_USER || 'admin';
const PASS = process.env.ADMIN_PASS || '1q2w3E*';

// ---- deterministic RNG (stable re-runs; no Date.now/Math.random) -----------
let _s = 1234567;
const rnd = () => { _s = (_s * 1103515245 + 12345) & 0x7fffffff; return _s / 0x7fffffff; };
const pick = (a) => a[Math.floor(rnd() * a.length)];
const pickUnique = (a, n) => { // up to n distinct elements
  const pool = a.slice(); const out = [];
  n = Math.min(n, pool.length);
  for (let i = 0; i < n; i++) out.push(pool.splice(Math.floor(rnd() * pool.length), 1)[0]);
  return out;
};
const randInt = (lo, hi) => lo + Math.floor(rnd() * (hi - lo + 1));
const round = (n, d = 0) => { const f = 10 ** d; return Math.round(n * f) / f; };

// ---- http ------------------------------------------------------------------
let TOKEN = null;
async function auth() {
  const body = new URLSearchParams({
    grant_type: 'password', username: USER, password: PASS,
    client_id: 'MyERP_App', scope: 'MyERP offline_access',
  });
  const r = await fetch(`${API}/connect/token`, {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body,
  });
  if (!r.ok) throw new Error(`auth failed ${r.status}: ${await r.text()}`);
  TOKEN = (await r.json()).access_token;
}
async function api(method, path, body) {
  const r = await fetch(`${API}/api/app/${path}`, {
    method,
    headers: {
      'Authorization': `Bearer ${TOKEN}`,
      'Content-Type': 'application/json',
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const txt = await r.text();
  if (!r.ok) {
    const short = txt.replace(/\s+/g, ' ').slice(0, 300);
    throw new Error(`${method} ${path} -> ${r.status}: ${short}`);
  }
  return txt ? JSON.parse(txt) : null;
}
const get = (p) => api('GET', p);
const post = (p, b) => api('POST', p, b);
async function listAll(entity, extra = '') {
  const r = await get(`${entity}?MaxResultCount=1000${extra}`);
  return Array.isArray(r) ? r : (r.items || []);
}

// ---- date helpers (6-month window ending ~today 2026-07-20) -----------------
const iso = (y, m, d) => `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}T00:00:00Z`;
// window: 2026-01-15 .. 2026-07-20
const START = new Date('2026-01-15T00:00:00Z').getTime();
const END = new Date('2026-07-20T00:00:00Z').getTime();
const dayMs = 86400000;
function dateAt(frac) { // frac 0..1 across window -> ISO
  const t = START + Math.floor((END - START) * frac);
  const dt = new Date(t);
  return iso(dt.getUTCFullYear(), dt.getUTCMonth() + 1, dt.getUTCDate());
}
function addDaysIso(isoStr, days) {
  const dt = new Date(isoStr); dt.setUTCDate(dt.getUTCDate() + days);
  return iso(dt.getUTCFullYear(), dt.getUTCMonth() + 1, dt.getUTCDate());
}

// ---- workflow helpers ------------------------------------------------------
async function submit(entity, id) { return post(`${entity}/${id}/submit`); }
async function postDoc(entity, id) { return post(`${entity}/${id}`); } // Draft->..->Post
async function createSubmitPost(entity, dto) {
  const c = await post(entity, dto);
  await submit(entity, c.id);
  await postDoc(entity, c.id);
  return c;
}
async function createSubmit(entity, dto) {
  const c = await post(entity, dto);
  await submit(entity, c.id);
  return c;
}

// ============================================================================
// reference data (loaded at runtime)
// ============================================================================
const R = {};
async function loadRefs() {
  const companies = await listAll('company');
  R.company = companies[0];
  R.companyId = R.company.id;
  R.accounts = await listAll('account');
  const acc = (code) => (R.accounts.find(a => a.accountCode === code) || {}).id;
  R.accTemporaryOpening = acc('3900');
  R.accBank = acc('1120');
  R.accCash = acc('1110');
  R.accReceivable = acc('1130');
  R.accPayable = acc('2110');
  R.accInventory = acc('1140');
  R.accShareCapital = acc('3100');
  R.warehouses = await listAll('warehouse');
  const wh = (name) => (R.warehouses.find(w => w.name === name) || {}).id;
  R.whMain = wh('Main Distribution Center') || wh('Stores');
  R.whCold = wh('Cold Chain Store (2-8°C)') || R.whMain;
  R.costCenters = await listAll('cost-center');
  R.costCenterMain = (R.costCenters.find(c => !c.isGroup) || R.costCenters[0] || {}).id;
  R.priceLists = await listAll('pricing/price-lists');
  R.plSelling = (R.priceLists.find(p => p.isSelling) || {}).id;
  R.plBuying = (R.priceLists.find(p => p.isBuying) || {}).id;
  R.fiscalYears = await listAll('fiscal-year');
  R.fiscalYearId = (R.fiscalYears[0] || {}).id;
  R.paymentTerms = await listAll('payment-terms-template');
  R.ptNet30 = (R.paymentTerms.find(p => /net 30/i.test(p.name)) || R.paymentTerms[0] || {}).id;
  console.log(`refs: company=${R.company.name} (${R.company.currencyCode}) accts=${R.accounts.length} wh=${R.warehouses.length} main=${!!R.whMain} opening=${!!R.accTemporaryOpening} cc=${!!R.costCenterMain}`);
  if (!R.whMain || !R.accTemporaryOpening) throw new Error('missing warehouse or opening account');
}

// ============================================================================
// catalogs
// ============================================================================
const DOC_SERIES = [
  ['SalesInvoice', 'SINV-'], ['SalesOrder', 'SO-'], ['Quotation', 'QTN-'],
  ['DeliveryNote', 'DN-'], ['PurchaseOrder', 'PO-'], ['PurchaseInvoice', 'PINV-'],
  ['PurchaseReceipt', 'PREC-'], ['RFQ', 'RFQ-'], ['MR', 'MR-'], ['StockEntry', 'STE-'],
  ['SCR', 'SCR-'], ['JournalEntry', 'JE-'], ['PaymentEntry', 'PAY-'], ['POS', 'POS-'],
  ['Asset', 'AST-'], ['Employee', 'EMP-'], ['Payroll', 'PAY-RUN-'], ['Project', 'PRJ-'],
  ['WO', 'WO-'], ['BOM', 'BOM-'], ['PP', 'PP-'], ['SCO', 'SCO-'], ['IN', 'IN-'], ['LOAN', 'LOAN-'],
];

// Iraqi pharma catalog — [code, name, group, uom, buyIQD, sellIQD, cold]
const ITEMS = [
  ['MED-0001', 'Paracetamol 500mg Tablets (Box/100)', 'Analgesics', 'Box', 2200, 3200, false],
  ['MED-0002', 'Ibuprofen 400mg Tablets (Box/50)', 'Analgesics', 'Box', 2600, 3800, false],
  ['MED-0003', 'Aspirin 100mg Tablets (Box/30)', 'Analgesics', 'Box', 1500, 2400, false],
  ['MED-0004', 'Diclofenac 50mg Tablets (Box/30)', 'Analgesics', 'Box', 1900, 3000, false],
  ['MED-0005', 'Amoxicillin 500mg Capsules (Box/21)', 'Antibiotics', 'Box', 3800, 5500, false],
  ['MED-0006', 'Azithromycin 500mg Tablets (Box/6)', 'Antibiotics', 'Box', 6500, 9500, false],
  ['MED-0007', 'Ciprofloxacin 500mg Tablets (Box/10)', 'Antibiotics', 'Box', 4200, 6200, false],
  ['MED-0008', 'Cephalexin 500mg Capsules (Box/20)', 'Antibiotics', 'Box', 5200, 7600, false],
  ['MED-0009', 'Metronidazole 500mg Tablets (Box/20)', 'Antibiotics', 'Box', 2400, 3600, false],
  ['MED-0010', 'Omeprazole 20mg Capsules (Box/14)', 'Gastro', 'Box', 3100, 4700, false],
  ['MED-0011', 'Ranitidine 150mg Tablets (Box/30)', 'Gastro', 'Box', 2000, 3100, false],
  ['MED-0012', 'Metformin 850mg Tablets (Box/30)', 'Diabetes', 'Box', 2800, 4200, false],
  ['MED-0013', 'Gliclazide 80mg Tablets (Box/30)', 'Diabetes', 'Box', 3400, 5000, false],
  ['MED-0014', 'Insulin Glargine 100IU/mL Pen', 'Diabetes', 'Unit', 18000, 25000, true],
  ['MED-0015', 'Amlodipine 5mg Tablets (Box/30)', 'Cardio', 'Box', 2600, 3900, false],
  ['MED-0016', 'Bisoprolol 5mg Tablets (Box/30)', 'Cardio', 'Box', 3000, 4500, false],
  ['MED-0017', 'Atorvastatin 20mg Tablets (Box/30)', 'Cardio', 'Box', 4800, 7000, false],
  ['MED-0018', 'Lisinopril 10mg Tablets (Box/30)', 'Cardio', 'Box', 2900, 4400, false],
  ['MED-0019', 'Losartan 50mg Tablets (Box/30)', 'Cardio', 'Box', 3600, 5300, false],
  ['MED-0020', 'Salbutamol Inhaler 100mcg', 'Respiratory', 'Unit', 5500, 8000, false],
  ['MED-0021', 'Budesonide Inhaler 200mcg', 'Respiratory', 'Unit', 12000, 17000, false],
  ['MED-0022', 'Cetirizine 10mg Tablets (Box/20)', 'Respiratory', 'Box', 1800, 2900, false],
  ['MED-0023', 'Loratadine 10mg Tablets (Box/20)', 'Respiratory', 'Box', 2100, 3300, false],
  ['MED-0024', 'Vitamin C 1000mg Effervescent (Tube/20)', 'Supplements', 'Tube', 2500, 3900, false],
  ['MED-0025', 'Vitamin D3 5000IU Softgels (Box/60)', 'Supplements', 'Box', 4600, 6800, false],
  ['MED-0026', 'Ferrous Sulphate 200mg (Box/30)', 'Supplements', 'Box', 1700, 2700, false],
  ['MED-0027', 'Multivitamin Syrup 150mL', 'Supplements', 'Bottle', 3200, 4900, false],
  ['MED-0028', 'ORS Sachets Orange (Box/20)', 'Gastro', 'Box', 1400, 2300, false],
  ['MED-0029', 'Amoxicillin/Clavulanate 625mg (Box/14)', 'Antibiotics', 'Box', 5800, 8400, false],
  ['MED-0030', 'Pantoprazole 40mg Tablets (Box/14)', 'Gastro', 'Box', 3300, 5000, false],
  ['MED-0031', 'Hepatitis B Vaccine 1mL Vial', 'Vaccines', 'Vial', 22000, 30000, true],
  ['MED-0032', 'Influenza Vaccine 0.5mL Pre-filled', 'Vaccines', 'Unit', 15000, 21000, true],
  ['SUP-0001', 'Nitrile Examination Gloves (Box/100)', 'Consumables', 'Box', 6000, 8500, false],
  ['SUP-0002', 'Surgical Face Masks 3-ply (Box/50)', 'Consumables', 'Box', 2500, 4000, false],
  ['SUP-0003', 'Digital Thermometer', 'Devices', 'Unit', 4000, 6500, false],
  ['SUP-0004', 'Blood Pressure Monitor (Automatic)', 'Devices', 'Unit', 28000, 40000, false],
];

// suppliers — [name, code, contact, phone, city]
const SUPPLIERS = [
  ['Samarra Drug Industries (SDI)', 'SUP-001', 'Ahmed Al-Samarrai', '+964 780 111 2233', 'Samarra'],
  ['Pioneer Company for Pharmaceutical Industries', 'SUP-002', 'Layla Hassan', '+964 781 222 3344', 'Sulaymaniyah'],
  ['Julphar Gulf Pharmaceuticals', 'SUP-003', 'Omar Khalid', '+971 6 553 4444', 'Ras Al Khaimah'],
  ['Hikma Pharmaceuticals', 'SUP-004', 'Nour Abdullah', '+962 6 580 0400', 'Amman'],
  ['Awamedica Pharmaceutical Co.', 'SUP-005', 'Zaid Karim', '+964 750 333 4455', 'Erbil'],
  ['Sanofi Middle East (Distributor)', 'SUP-006', 'Rana Fadel', '+964 782 444 5566', 'Baghdad'],
  ['GSK Iraq Agency', 'SUP-007', 'Bilal Yousif', '+964 783 555 6677', 'Baghdad'],
  ['Medline Medical Supplies', 'SUP-008', 'Hana Salim', '+964 784 666 7788', 'Basra'],
];

// customers — [name, code, type, contact, phone, city]
const CUSTOMERS = [
  ['Saydaliyat Al-Shifa (Al-Shifa Pharmacy)', 'CUST-001', 'Retail', 'Mohammed Jasim', '+964 770 100 2001', 'Baghdad'],
  ['Al-Rahma Pharmacy', 'CUST-002', 'Retail', 'Sara Adnan', '+964 770 100 2002', 'Baghdad'],
  ['Al-Noor Pharmacy', 'CUST-003', 'Retail', 'Kareem Waleed', '+964 771 100 2003', 'Baghdad'],
  ['Ibn Sina Pharmacy', 'CUST-004', 'Retail', 'Dina Faisal', '+964 771 100 2004', 'Baghdad'],
  ['Al-Hayat Pharmacy', 'CUST-005', 'Retail', 'Rami Sabah', '+964 772 100 2005', 'Basra'],
  ['Basra Central Pharmacy', 'CUST-006', 'Retail', 'Noor Jabbar', '+964 772 100 2006', 'Basra'],
  ['Al-Kindi Pharmacy', 'CUST-007', 'Retail', 'Fahad Nasser', '+964 773 100 2007', 'Mosul'],
  ['Nineveh Pharmacy', 'CUST-008', 'Retail', 'Aya Ghanim', '+964 773 100 2008', 'Mosul'],
  ['Erbil Family Pharmacy', 'CUST-009', 'Retail', 'Dana Aziz', '+964 750 100 2009', 'Erbil'],
  ['Kurdistan Pharmacy', 'CUST-010', 'Retail', 'Hemin Rashid', '+964 750 100 2010', 'Erbil'],
  ['Al-Amal Pharmacy', 'CUST-011', 'Retail', 'Yusra Talib', '+964 774 100 2011', 'Najaf'],
  ['Karbala Pharmacy Center', 'CUST-012', 'Retail', 'Haider Kadhim', '+964 774 100 2012', 'Karbala'],
  ['Baghdad Teaching Hospital', 'CUST-013', 'Hospital', 'Dr. Salim Mahdi', '+964 775 100 2013', 'Baghdad'],
  ['Medical City Complex', 'CUST-014', 'Hospital', 'Dr. Huda Nazar', '+964 775 100 2014', 'Baghdad'],
  ['Basra General Hospital', 'CUST-015', 'Hospital', 'Dr. Ali Hassan', '+964 776 100 2015', 'Basra'],
  ['Al-Jumhori Teaching Hospital', 'CUST-016', 'Hospital', 'Dr. Ban Saad', '+964 776 100 2016', 'Mosul'],
  ['Rizgary Teaching Hospital', 'CUST-017', 'Hospital', 'Dr. Shwan Aziz', '+964 750 100 2017', 'Erbil'],
  ['Al-Waffa Medical Clinic', 'CUST-018', 'Clinic', 'Dr. Maha Riyadh', '+964 777 100 2018', 'Baghdad'],
  ['Al-Salam Polyclinic', 'CUST-019', 'Clinic', 'Dr. Firas Amer', '+964 777 100 2019', 'Baghdad'],
  ['Tigris Medical Center', 'CUST-020', 'Clinic', 'Dr. Zahra Uday', '+964 778 100 2020', 'Baghdad'],
  ['Al-Furat Pharmacy Chain (Wholesale)', 'CUST-021', 'Wholesale', 'Mustafa Adel', '+964 778 100 2021', 'Baghdad'],
  ['Green Cross Pharmacy Group', 'CUST-022', 'Wholesale', 'Lubna Hadi', '+964 779 100 2022', 'Baghdad'],
  ['Al-Diwaniyah Pharmacy', 'CUST-023', 'Retail', 'Sami Turki', '+964 779 100 2023', 'Diwaniyah'],
  ['Kirkuk City Pharmacy', 'CUST-024', 'Retail', 'Alan Jamal', '+964 771 100 2024', 'Kirkuk'],
];

// ============================================================================
// stages
// ============================================================================
async function stageSeries() {
  const existing = await listAll('document-series');
  const have = new Set(existing.map(s => s.documentType));
  let n = 0;
  for (const [documentType, prefix] of DOC_SERIES) {
    if (have.has(documentType)) continue;
    await post('document-series', { companyId: R.companyId, name: documentType, documentType, prefix, numberPadding: 5 });
    n++;
  }
  console.log(`series: +${n} (had ${have.size})`);
}

async function stageItems() {
  const existing = await listAll('item');
  const have = new Map(existing.map(i => [i.itemCode, i.id]));
  R.items = [];
  let n = 0;
  for (const [itemCode, itemName, group, uom, buy, sell, cold] of ITEMS) {
    let id = have.get(itemCode);
    if (!id) {
      const dto = {
        companyId: R.companyId, itemCode, itemName, description: itemName,
        itemType: 0, itemGroup: group, brand: '', uom,
        valuationMethod: 1, standardSellingPrice: sell, standardBuyingPrice: buy,
        maintainStock: true, isActive: true,
        reorderLevel: 50, reorderQty: 200, safetyStock: 20,
        defaultWarehouseId: cold ? R.whCold : R.whMain,
      };
      const c = await post('item', dto);
      id = c.id; n++;
    }
    R.items.push({ id, itemCode, itemName, group, uom, buy, sell, cold });
  }
  console.log(`items: +${n} total ${R.items.length}`);
}

async function stageParties() {
  const exSup = new Map((await listAll('supplier')).map(s => [s.supplierCode, s.id]));
  R.suppliers = []; let ns = 0;
  for (const [name, code, contact, phone, city] of SUPPLIERS) {
    let id = exSup.get(code);
    if (!id) {
      const c = await post('supplier', {
        companyId: R.companyId, name, supplierCode: code, contactPerson: contact,
        phone, country: 'Iraq', city, isActive: true,
      });
      id = c.id; ns++;
    }
    R.suppliers.push({ id, name, code });
  }
  const exCust = new Map((await listAll('customer')).map(c => [c.customerCode, c.id]));
  R.customers = []; let nc = 0;
  for (const [name, code, type, contact, phone, city] of CUSTOMERS) {
    let id = exCust.get(code);
    if (!id) {
      const c = await post('customer', {
        companyId: R.companyId, name, customerCode: code, contactPerson: contact,
        phone, country: 'Iraq', city, isActive: true,
      });
      id = c.id; nc++;
    }
    R.customers.push({ id, name, code, type });
  }
  console.log(`parties: +${ns} suppliers (${R.suppliers.length}), +${nc} customers (${R.customers.length})`);
}

async function stageOpening() {
  if (!R.items) await stageItems();
  // one opening stock reconciliation for all items @ buying rate
  const existing = await listAll('stock-reconciliation');
  if (existing.some(r => /opening/i.test(r.purpose || '') || /opening/i.test(r.notes || ''))) {
    console.log('opening: already present, skip'); return;
  }
  let openingValue = 0;
  const items = R.items.map(it => {
    const qty = randInt(6000, 12000);
    openingValue += qty * it.buy;
    return { itemId: it.id, warehouseId: R.whMain, newQuantity: qty, newValuationRate: it.buy, currentQuantity: 0, currentValuationRate: 0 };
  });
  const dto = {
    companyId: R.companyId, postingDate: iso(2026, 1, 10), purpose: 'Opening Stock',
    notes: 'Opening Stock — Al-Rafidain Pharma (Jan 2026)',
    expenseAccountId: R.accTemporaryOpening, costCenterId: R.costCenterMain, items,
  };
  const c = await createSubmit('stock-reconciliation', dto);
  // Stock reconciliation posts the stock ledger but not the GL, so book the
  // opening inventory value into the GL: DR Inventory, CR Temporary Opening (equity).
  const je = await post('journal-entry', {
    companyId: R.companyId, fiscalYearId: R.fiscalYearId, postingDate: iso(2026, 1, 10),
    narration: 'Opening Stock — inventory brought forward (Jan 2026)',
    lines: [
      { accountId: R.accInventory, amount: openingValue, isDebit: true, description: 'Opening inventory' },
      { accountId: R.accTemporaryOpening, amount: openingValue, isDebit: false, description: 'Opening balance offset' },
    ],
  });
  await postDoc('journal-entry', je.id);
  // Opening capital injection so the company starts with working cash: DR Bank, CR Share Capital.
  const capital = 250000000; // 250M IQD paid-in capital (cash at bank)
  const cap = await post('journal-entry', {
    companyId: R.companyId, fiscalYearId: R.fiscalYearId, postingDate: iso(2026, 1, 5),
    narration: 'Opening paid-in share capital (cash at bank)',
    lines: [
      { accountId: R.accBank, amount: capital, isDebit: true, description: 'Cash at bank' },
      { accountId: R.accShareCapital, amount: capital, isDebit: false, description: 'Share capital' },
    ],
  });
  await postDoc('journal-entry', cap.id);
  console.log(`opening: reconciliation ${c.id} + inventory JE ${round(openingValue).toLocaleString()} + capital 250,000,000 IQD`);
}

async function stagePurchases() {
  if (!R.items) await stageItems();
  if (!R.suppliers) await stageParties();
  let po = 0, pi = 0;
  // ~14 purchase runs across the window, replenishing stock
  for (let i = 0; i < 14; i++) {
    const frac = (i + 0.5) / 14;
    const orderDate = dateAt(frac);
    const supplier = pick(R.suppliers);
    const chosen = pickUnique(R.items, randInt(3, 7));
    const lines = chosen.map(it => ({
      itemId: it.id, description: it.itemName, quantity: randInt(1000, 3000),
      unitPrice: it.buy, taxAmount: 0, uom: it.uom, warehouseId: R.whMain,
    }));
    // Purchase Order -> submit
    const order = await createSubmit('purchase-order', {
      companyId: R.companyId, supplierId: supplier.id, orderDate,
      expectedDeliveryDate: addDaysIso(orderDate, 7), notes: 'Stock replenishment', items: lines,
    });
    po++;
    // Purchase Invoice (updateStock) -> submit -> post
    const inv = await createSubmitPost('purchase-invoice', {
      companyId: R.companyId, supplierId: supplier.id, issueDate: addDaysIso(orderDate, 5),
      dueDate: addDaysIso(orderDate, 35), supplierInvoiceNumber: `SUP-INV-${1000 + i}`,
      currencyCode: 'IQD', notes: 'Goods received', updateStock: true, warehouseId: R.whMain,
      items: lines.map(l => ({ itemId: l.itemId, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, taxAmount: 0, uom: l.uom })),
    });
    pi++;
    R.purchaseInvoices = R.purchaseInvoices || [];
    R.purchaseInvoices.push({ id: inv.id, supplierId: supplier.id, date: addDaysIso(orderDate, 5), total: inv.grandTotal });
  }
  console.log(`purchases: ${po} POs, ${pi} purchase invoices`);
}

async function stageSales() {
  if (!R.items) await stageItems();
  if (!R.customers) await stageParties();
  R.salesInvoices = [];
  let so = 0, dn = 0, si = 0;
  const N = 84; // ~ 3-4 per week across ~26 weeks
  let skipped = 0;
  for (let i = 0; i < N; i++) {
    const frac = (i + 0.5) / N;
    const orderDate = dateAt(frac);
    const customer = pick(R.customers);
    const chosen = pickUnique(R.items, randInt(2, 6));
    const lines = chosen.map(it => ({
      itemId: it.id, description: it.itemName, quantity: randInt(300, 650),
      unitPrice: it.sell, costPrice: it.buy, taxAmount: 0, uom: it.uom, warehouseId: R.whMain,
    }));
    const viaDelivery = true; // full SO -> Delivery -> Invoice cycle (delivery posts COGS)
    try {
      if (viaDelivery) {
        const order = await createSubmit('sales-order', {
          companyId: R.companyId, customerId: customer.id, orderDate,
          deliveryDate: addDaysIso(orderDate, 3), customerPoNumber: `PO-${customer.code}-${i}`,
          currencyCode: 'IQD', notes: 'Customer order',
          items: lines.map(l => ({ itemId: l.itemId, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, taxAmount: 0, uom: l.uom, warehouseId: l.warehouseId })),
        });
        so++;
        // Delivery posts the stock movement (goods leave inventory) + COGS.
        // COGS is booked from the DeliveryNote amount, so value the DN lines at COST.
        await createSubmit('delivery-note', {
          companyId: R.companyId, customerId: customer.id, warehouseId: R.whMain,
          postingDate: addDaysIso(orderDate, 2), salesOrderId: order.id, notes: 'Delivered',
          items: lines.map(l => ({ itemId: l.itemId, description: l.description, quantity: l.quantity, unitPrice: l.costPrice, taxAmount: 0, uom: l.uom })),
        });
        dn++;
      }
      // If delivered already, invoice bills only (no stock update); else invoice updates stock
      const inv = await createSubmitPost('sales-invoice', {
        companyId: R.companyId, customerId: customer.id, issueDate: addDaysIso(orderDate, 2),
        dueDate: addDaysIso(orderDate, 32), currencyCode: 'IQD', notes: 'Thank you for your business',
        updateStock: !viaDelivery, warehouseId: R.whMain,
        items: lines.map(l => ({ itemId: l.itemId, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, taxAmount: 0, uom: l.uom })),
      });
      si++;
      R.salesInvoices.push({ id: inv.id, customerId: customer.id, date: addDaysIso(orderDate, 2), total: inv.grandTotal });
    } catch (e) {
      skipped++;
      if (skipped <= 5) console.log('  sale skip:', e.message.slice(0, 130));
    }
  }
  if (skipped) console.log(`sales: skipped ${skipped} on stock/validation`);
  console.log(`sales: ${so} SOs, ${dn} deliveries, ${si} sales invoices`);
}

async function stagePayments() {
  // pay ~75% of sales invoices (collections) and ~70% of purchase invoices
  let recv = 0, paid = 0;
  const sis = R.salesInvoices || (await listAll('sales-invoice')).map(x => ({ id: x.id, customerId: x.customerId, date: x.issueDate, total: x.grandTotal }));
  let rskip = 0;
  for (const inv of sis) {
    if (rnd() > 0.75) continue; // ~25% left outstanding for AR aging
    try {
      // Receive: DR Bank (paidTo), CR Receivable (paidFrom = party AR)
      await createSubmitPost('payment-entry', {
        companyId: R.companyId, paymentType: 0, postingDate: addDaysIso(inv.date, randInt(5, 25)),
        paidAmount: inv.total, paidFromAccountId: R.accReceivable, paidToAccountId: R.accBank,
        modeOfPayment: 'Wire Transfer', partyType: 'Customer', partyId: inv.customerId,
        referenceNumber: `RCPT-${recv + 1}`,
        references: [{ referenceType: 'SalesInvoice', referenceId: inv.id, allocatedAmount: inv.total }],
      });
      recv++;
    } catch (e) { rskip++; if (rskip <= 3) console.log('  recv skip:', e.message.slice(0, 150)); }
  }
  const pis = R.purchaseInvoices || (await listAll('purchase-invoice')).map(x => ({ id: x.id, supplierId: x.supplierId, date: x.issueDate, total: x.grandTotal }));
  let pskip = 0;
  for (const inv of pis) {
    if (rnd() > 0.70) continue;
    try {
      // Supplier payment as a Journal Entry: DR Accounts Payable, CR Bank.
      // (The PaymentEntry GL rule only handles receive-direction; booking the
      //  supplier settlement as a JE keeps AP paydown + bank outflow correct.)
      const je = await post('journal-entry', {
        companyId: R.companyId, fiscalYearId: R.fiscalYearId,
        postingDate: addDaysIso(inv.date, randInt(10, 30)),
        narration: `Supplier payment — PurchaseInvoice settlement`,
        lines: [
          { accountId: R.accPayable, amount: inv.total, isDebit: true, description: 'Settle supplier payable' },
          { accountId: R.accBank, amount: inv.total, isDebit: false, description: 'Bank payment' },
        ],
      });
      await postDoc('journal-entry', je.id);
      paid++;
    } catch (e) { pskip++; if (pskip <= 3) console.log('  pay skip:', e.message.slice(0, 150)); }
  }
  console.log(`payments: ${recv} receipts, ${paid} supplier payments`);
}

// ============================================================================
const STAGES = {
  series: stageSeries, items: stageItems, parties: stageParties, opening: stageOpening,
  purchases: stagePurchases, sales: stageSales, payments: stagePayments,
};
const DEFAULT = ['series', 'items', 'parties', 'opening', 'purchases', 'sales', 'payments'];

async function main() {
  const args = process.argv.slice(2);
  const run = args.length ? args : DEFAULT;
  await auth();
  await loadRefs();
  for (const s of run) {
    if (!STAGES[s]) { console.log(`unknown stage ${s}`); continue; }
    const t0 = Date.now();
    await STAGES[s]();
    console.log(`  [${s}] done in ${((Date.now() - t0) / 1000).toFixed(1)}s`);
  }
  console.log('SEED COMPLETE');
}
main().catch(e => { console.error('SEED FAILED:', e.message); process.exit(1); });
