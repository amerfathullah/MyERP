import { describe, it, expect, beforeEach } from 'vitest';

interface CartItem {
  itemId: string;
  itemName: string;
  qty: number;
  rate: number;
  amount: number;
  discount: number;
  taxRate: number;
  taxAmount: number;
}

interface PaymentRow {
  mode: string;
  amount: number;
}

interface HeldOrder {
  id: string;
  customer: string;
  items: CartItem[];
  payments: PaymentRow[];
  heldAt: Date;
}

class MockPosLogic {
  cart: CartItem[] = [];
  netTotal = 0;
  taxTotal = 0;
  discountTotal = 0;
  grandTotal = 0;

  payments: PaymentRow[] = [{ mode: 'Cash', amount: 0 }];
  selectedPaymentMode = '';

  customerId: string | null = null;
  customerName = '';
  lastInvoice: string | null = null;

  heldOrders: HeldOrder[] = [];
  defaultTaxRate = 6;

  addToCart(item: { id: string; itemName: string; sellingPrice: number }): void {
    const existing = this.cart.find(c => c.itemId === item.id);
    if (existing) {
      existing.qty++;
      existing.amount = existing.qty * existing.rate * (1 - existing.discount / 100);
      existing.taxAmount = (existing.amount * existing.taxRate) / 100;
    } else {
      const rate = item.sellingPrice ?? 0;
      const taxRate = this.defaultTaxRate;
      this.cart.push({
        itemId: item.id,
        itemName: item.itemName,
        qty: 1,
        rate,
        amount: rate,
        discount: 0,
        taxRate,
        taxAmount: (rate * taxRate) / 100,
      });
    }
    this.recalculate();
  }

  removeFromCart(index: number): void {
    this.cart.splice(index, 1);
    this.recalculate();
  }

  updateQty(index: number, qty: number): void {
    if (qty <= 0) {
      this.removeFromCart(index);
      return;
    }
    const item = this.cart[index];
    item.qty = qty;
    item.amount = qty * item.rate * (1 - item.discount / 100);
    item.taxAmount = (item.amount * item.taxRate) / 100;
    this.recalculate();
  }

  recalculate(): void {
    this.netTotal = this.cart.reduce((s, c) => s + c.amount, 0);
    this.taxTotal = this.cart.reduce((s, c) => s + c.taxAmount, 0);
    this.discountTotal = this.cart.reduce((s, c) => s + (c.qty * c.rate - c.amount), 0);
    this.grandTotal = this.netTotal + this.taxTotal;

    if (this.payments.length === 1 && this.payments[0].amount === 0) {
      this.payments[0].amount = this.grandTotal;
    }
  }

  selectPaymentMode(mode: string): void {
    this.selectedPaymentMode = mode;
    if (this.payments.length > 0) {
      this.payments[0].mode = mode;
    }
  }

  get changeAmount(): number {
    const paid = this.payments.reduce((s, p) => s + (p.amount || 0), 0);
    return Math.max(0, paid - this.grandTotal);
  }

  get outstandingAmount(): number {
    const paid = this.payments.reduce((s, p) => s + (p.amount || 0), 0);
    return Math.max(0, this.grandTotal - paid);
  }

  holdOrder(): void {
    if (this.cart.length === 0) return;
    const order: HeldOrder = {
      id: 'test-held-1',
      customer: this.customerName || 'Walk-in',
      items: [...this.cart],
      payments: [...this.payments],
      heldAt: new Date(),
    };
    this.heldOrders.push(order);
    this.makeNewInvoice();
  }

  resumeOrder(index: number): void {
    const order = this.heldOrders[index];
    this.cart = [...order.items];
    this.payments = [...order.payments];
    this.selectedPaymentMode = this.payments[0]?.mode ?? '';
    this.customerName = order.customer;
    this.heldOrders.splice(index, 1);
    this.recalculate();
  }

  makeNewInvoice(): void {
    this.clearCart();
    this.lastInvoice = null;
  }

  clearCart(): void {
    this.cart = [];
    this.payments = [{ mode: 'Cash', amount: 0 }];
    this.selectedPaymentMode = '';
    this.customerId = null;
    this.customerName = '';
    this.netTotal = 0;
    this.taxTotal = 0;
    this.discountTotal = 0;
    this.grandTotal = 0;
  }
}

describe('PosComponent Logic', () => {
  let pos: MockPosLogic;

  beforeEach(() => {
    pos = new MockPosLogic();
  });

  describe('makeNewInvoice and Payment Mode Reset (PR #59308)', () => {
    it('resets stale selected mode of payment on makeNewInvoice', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Coffee', sellingPrice: 10 });
      pos.selectPaymentMode('Credit Card');
      expect(pos.selectedPaymentMode).toBe('Credit Card');
      expect(pos.payments[0].mode).toBe('Credit Card');

      pos.makeNewInvoice();

      expect(pos.selectedPaymentMode).toBe('');
      expect(pos.payments.length).toBe(1);
      expect(pos.payments[0].mode).toBe('Cash');
      expect(pos.payments[0].amount).toBe(0);
      expect(pos.lastInvoice).toBeNull();
    });

    it('resets cart, totals, and customer when clearing cart', () => {
      pos.addToCart({ id: 'item-2', itemName: 'Tea', sellingPrice: 5 });
      pos.customerId = 'cust-1';
      pos.customerName = 'Alice';

      pos.clearCart();

      expect(pos.cart.length).toBe(0);
      expect(pos.customerId).toBeNull();
      expect(pos.customerName).toBe('');
      expect(pos.grandTotal).toBe(0);
      expect(pos.selectedPaymentMode).toBe('');
    });
  });

  describe('Cart and Calculations', () => {
    it('calculates totals with 6% SST tax correctly', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Burger', sellingPrice: 20 });
      expect(pos.netTotal).toBe(20);
      expect(pos.taxTotal).toBe(1.2); // 6% of 20
      expect(pos.grandTotal).toBe(21.2);
      expect(pos.payments[0].amount).toBe(21.2);
    });

    it('increments quantity when existing item added again', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Burger', sellingPrice: 20 });
      pos.addToCart({ id: 'item-1', itemName: 'Burger', sellingPrice: 20 });

      expect(pos.cart.length).toBe(1);
      expect(pos.cart[0].qty).toBe(2);
      expect(pos.netTotal).toBe(40);
      expect(pos.grandTotal).toBe(42.4);
    });

    it('updates quantity and recalculates totals', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Fries', sellingPrice: 10 });
      pos.updateQty(0, 3);

      expect(pos.cart[0].qty).toBe(3);
      expect(pos.netTotal).toBe(30);
      expect(pos.grandTotal).toBe(31.8);
    });

    it('calculates change amount and outstanding correctly', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Meal', sellingPrice: 50 }); // grand total = 53
      pos.payments = [{ mode: 'Cash', amount: 60 }];

      expect(pos.changeAmount).toBe(7);
      expect(pos.outstandingAmount).toBe(0);

      pos.payments = [{ mode: 'Cash', amount: 40 }];
      expect(pos.changeAmount).toBe(0);
      expect(pos.outstandingAmount).toBe(13);
    });
  });

  describe('Hold and Resume Orders', () => {
    it('holds an active order and clears the current cart and payment mode', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Latte', sellingPrice: 15 });
      pos.selectPaymentMode('E-Wallet');
      pos.customerName = 'Bob';

      pos.holdOrder();

      expect(pos.heldOrders.length).toBe(1);
      expect(pos.heldOrders[0].customer).toBe('Bob');
      expect(pos.heldOrders[0].items.length).toBe(1);
      expect(pos.cart.length).toBe(0);
      expect(pos.selectedPaymentMode).toBe('');
    });

    it('resumes a held order restoring cart and payment mode', () => {
      pos.addToCart({ id: 'item-1', itemName: 'Latte', sellingPrice: 15 });
      pos.selectPaymentMode('E-Wallet');
      pos.customerName = 'Bob';
      pos.holdOrder();

      pos.resumeOrder(0);

      expect(pos.cart.length).toBe(1);
      expect(pos.cart[0].itemName).toBe('Latte');
      expect(pos.customerName).toBe('Bob');
      expect(pos.selectedPaymentMode).toBe('E-Wallet');
      expect(pos.heldOrders.length).toBe(0);
    });
  });
});
