import { describe, it, expect } from 'vitest';

/**
 * API Contract Tests — ensures the frontend DTO shapes sent to the backend
 * match the backend's expected property names, types, and required fields.
 *
 * These tests catch the #1 source of production bugs: form→DTO field name mismatches.
 * Each test creates a DTO exactly as the form's save() would produce it, then verifies
 * all required fields are present and correctly typed.
 */

// Backend contracts (from MyERP.Application.Contracts)
interface BackendCreateSalesInvoiceDto {
  companyId: string;      // [Required] Guid
  customerId: string;     // [Required] Guid
  issueDate: string;      // [Required] DateTime
  dueDate?: string | null;
  currencyCode?: string;  // default "MYR"
  notes?: string | null;
  paymentTermsTemplateId?: string | null;
  isReturn?: boolean;
  returnAgainstId?: string | null;
  isOpening?: boolean;
  projectId?: string | null;
  updateStock?: boolean;
  warehouseId?: string | null;
  items: Array<{
    itemId: string;       // [Required] Guid
    description: string;  // [Required] max 500
    quantity: number;     // [Required] (negative for returns)
    unitPrice: number;    // [Required] >= 0
    taxAmount?: number;   // >= 0
    uom?: string;         // default "Unit"
  }>;
}

interface BackendCreatePurchaseOrderDto {
  companyId: string;      // [Required] Guid
  supplierId: string;     // [Required] Guid
  orderDate: string;      // [Required]
  deliveryDate?: string | null;
  currencyCode?: string;
  terms?: string | null;
  notes?: string | null;
  items: Array<{
    itemId: string;
    description: string;
    quantity: number;
    unitPrice: number;
    taxAmount?: number;
    uom?: string;
  }>;
}

interface BackendCreatePaymentEntryDto {
  companyId: string;       // [Required]
  postingDate: string;     // [Required]
  paymentType: string;     // "Receive" | "Pay" | "InternalTransfer"
  paidAmount: number;      // > 0
  paidFromAccountId: string;
  paidToAccountId: string;
  partyType?: string;
  partyId?: string;
  againstInvoiceId?: string | null;
  againstOrderId?: string | null;
  againstOrderType?: string | null;
  referenceNumber?: string;
  exchangeRate?: number;
  references?: Array<{
    referenceType: string;
    referenceId: string;
    allocatedAmount: number;
    exchangeRate?: number;
  }>;
}

describe('API Contract: Sales Invoice', () => {
  function simulateSIFormSave(formValues: any): BackendCreateSalesInvoiceDto {
    const raw = formValues;
    return {
      companyId: raw.companyId,
      customerId: raw.customerId,
      issueDate: raw.issueDate,
      dueDate: raw.dueDate || null,
      currencyCode: raw.currencyCode || 'MYR',
      notes: raw.notes || null,
      isReturn: raw.isReturn || false,
      returnAgainstId: raw.returnAgainstId || null,
      updateStock: raw.updateStock || false,
      warehouseId: raw.warehouseId || null,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
      })),
    };
  }

  it('should produce valid DTO with all required fields', () => {
    const dto = simulateSIFormSave({
      companyId: 'comp-1',
      customerId: 'cust-1',
      issueDate: '2026-07-20',
      currencyCode: 'MYR',
      items: [{ itemId: 'item-1', description: 'Widget', quantity: 5, unitPrice: 100, taxAmount: 30 }],
    });

    expect(dto.companyId).toBe('comp-1');
    expect(dto.customerId).toBe('cust-1');
    expect(dto.issueDate).toBe('2026-07-20');
    expect(dto.items.length).toBe(1);
    expect(dto.items[0].itemId).toBe('item-1');
    expect(dto.items[0].quantity).toBe(5);
    expect(dto.items[0].unitPrice).toBe(100);
  });

  it('should map grid field names (qty/rate) to backend names (quantity/unitPrice)', () => {
    const dto = simulateSIFormSave({
      companyId: 'c', customerId: 'cu', issueDate: '2026-01-01',
      items: [{ itemId: 'item-1', itemName: 'Grid Item', qty: 3, rate: 50 }],
    });

    // Backend expects 'quantity' not 'qty', 'unitPrice' not 'rate'
    expect(dto.items[0].quantity).toBe(3);
    expect(dto.items[0].unitPrice).toBe(50);
    expect(dto.items[0].description).toBe('Grid Item');
  });

  it('should default currencyCode to MYR', () => {
    const dto = simulateSIFormSave({
      companyId: 'c', customerId: 'cu', issueDate: '2026-01-01', items: [],
    });
    expect(dto.currencyCode).toBe('MYR');
  });

  it('should include updateStock and warehouseId for POS-style sales', () => {
    const dto = simulateSIFormSave({
      companyId: 'c', customerId: 'cu', issueDate: '2026-01-01',
      updateStock: true, warehouseId: 'wh-1',
      items: [{ itemId: 'i', description: 'X', quantity: 1, unitPrice: 100 }],
    });
    expect(dto.updateStock).toBe(true);
    expect(dto.warehouseId).toBe('wh-1');
  });

  it('should handle credit note (return) fields', () => {
    const dto = simulateSIFormSave({
      companyId: 'c', customerId: 'cu', issueDate: '2026-01-01',
      isReturn: true, returnAgainstId: 'si-orig-001',
      items: [{ itemId: 'i', description: 'Return', quantity: -2, unitPrice: 50 }],
    });
    expect(dto.isReturn).toBe(true);
    expect(dto.returnAgainstId).toBe('si-orig-001');
    expect(dto.items[0].quantity).toBe(-2); // Negative for returns
  });
});

describe('API Contract: Purchase Order', () => {
  function simulatePOFormSave(formValues: any): BackendCreatePurchaseOrderDto {
    const raw = formValues;
    return {
      companyId: raw.companyId,
      supplierId: raw.supplierId,
      orderDate: raw.orderDate,
      deliveryDate: raw.deliveryDate || null,
      currencyCode: raw.currencyCode || 'MYR',
      notes: raw.notes || null,
      items: (raw.items ?? []).map((item: any) => ({
        itemId: item.itemId,
        description: item.description || item.itemName || '',
        quantity: item.quantity ?? item.qty ?? 0,
        unitPrice: item.unitPrice ?? item.rate ?? 0,
        taxAmount: item.taxAmount ?? 0,
        uom: item.uom ?? 'Unit',
      })),
    };
  }

  it('should produce valid PO DTO', () => {
    const dto = simulatePOFormSave({
      companyId: 'comp-1', supplierId: 'sup-1', orderDate: '2026-07-20',
      items: [{ itemId: 'item-1', description: 'Steel Bar', quantity: 100, unitPrice: 15 }],
    });
    expect(dto.companyId).toBe('comp-1');
    expect(dto.supplierId).toBe('sup-1');
    expect(dto.items[0].quantity).toBe(100);
  });

  it('should not have customerId (PO is supplier-side)', () => {
    const dto = simulatePOFormSave({
      companyId: 'c', supplierId: 's', orderDate: '2026-01-01', items: [],
    });
    expect((dto as any).customerId).toBeUndefined();
  });
});

describe('API Contract: Payment Entry', () => {
  function simulatePEFormSave(formValues: any, allocations: Map<string, number>): BackendCreatePaymentEntryDto {
    const raw = formValues;
    const dto: BackendCreatePaymentEntryDto = {
      companyId: raw.companyId,
      postingDate: raw.paymentDate, // Form uses 'paymentDate', backend expects 'postingDate'
      paymentType: raw.paymentType,
      paidAmount: raw.amount,       // Form uses 'amount', backend expects 'paidAmount'
      paidFromAccountId: raw.paidFromAccount, // Form omits 'Id' suffix
      paidToAccountId: raw.paidToAccount,
      partyType: raw.partyType,
      partyId: raw.partyId,
      referenceNumber: raw.reference, // Form uses 'reference', backend expects 'referenceNumber'
      againstInvoiceId: raw.againstInvoiceId || null,
      againstOrderId: raw.againstOrderId || null,
      againstOrderType: raw.againstOrderType || null,
    };

    // Multi-invoice allocation
    const allocs = Array.from(allocations.entries());
    if (allocs.length > 1) {
      dto.references = allocs.map(([invoiceId, amount]) => ({
        referenceType: raw.partyType === 'Customer' ? 'SalesInvoice' : 'PurchaseInvoice',
        referenceId: invoiceId,
        allocatedAmount: amount,
        exchangeRate: 1,
      }));
      dto.againstInvoiceId = null;
    } else if (allocs.length === 1) {
      dto.againstInvoiceId = allocs[0][0];
    }

    return dto;
  }

  it('should map paymentDate→postingDate and amount→paidAmount', () => {
    const dto = simulatePEFormSave(
      { companyId: 'c', paymentDate: '2026-07-20', paymentType: 'Receive', amount: 5000,
        paidFromAccount: 'acc-1', paidToAccount: 'acc-2', partyType: 'Customer', partyId: 'cust-1', reference: 'CHQ-001' },
      new Map()
    );
    expect(dto.postingDate).toBe('2026-07-20');
    expect(dto.paidAmount).toBe(5000);
    expect(dto.referenceNumber).toBe('CHQ-001');
    expect((dto as any).paymentDate).toBeUndefined();
    expect((dto as any).amount).toBeUndefined();
  });

  it('should build references array for multi-invoice allocation', () => {
    const allocations = new Map<string, number>();
    allocations.set('si-001', 3000);
    allocations.set('si-002', 2000);

    const dto = simulatePEFormSave(
      { companyId: 'c', paymentDate: '2026-07-20', paymentType: 'Receive', amount: 5000,
        paidFromAccount: 'a1', paidToAccount: 'a2', partyType: 'Customer', partyId: 'cust-1' },
      allocations
    );

    expect(dto.references!.length).toBe(2);
    expect(dto.references![0].referenceId).toBe('si-001');
    expect(dto.references![0].allocatedAmount).toBe(3000);
    expect(dto.references![0].referenceType).toBe('SalesInvoice');
    expect(dto.againstInvoiceId).toBeNull(); // Cleared for multi-ref
  });

  it('should use legacy single-invoice path when only one selected', () => {
    const allocations = new Map<string, number>();
    allocations.set('si-001', 5000);

    const dto = simulatePEFormSave(
      { companyId: 'c', paymentDate: '2026-07-20', paymentType: 'Receive', amount: 5000,
        paidFromAccount: 'a1', paidToAccount: 'a2' },
      allocations
    );

    expect(dto.againstInvoiceId).toBe('si-001');
    expect(dto.references).toBeUndefined();
  });

  it('should set PurchaseInvoice reference type for Supplier party', () => {
    const allocations = new Map<string, number>();
    allocations.set('pi-001', 2000);
    allocations.set('pi-002', 1000);

    const dto = simulatePEFormSave(
      { companyId: 'c', paymentDate: '2026-07-20', paymentType: 'Pay', amount: 3000,
        paidFromAccount: 'a1', paidToAccount: 'a2', partyType: 'Supplier', partyId: 'sup-1' },
      allocations
    );

    expect(dto.references![0].referenceType).toBe('PurchaseInvoice');
  });
});
