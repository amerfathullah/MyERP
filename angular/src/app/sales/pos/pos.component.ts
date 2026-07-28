import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { PosService } from '../../proxy/sales/pos.service';
import type { PosItemDto } from '../../proxy/sales/models';
import { debounceTime, Subject } from 'rxjs';

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

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  templateUrl: './pos.component.html',
  styleUrls: ['./pos.component.scss'],
})
export class PosComponent implements OnInit {
  private posService = inject(PosService);
  private toaster = inject(ToasterService);

  // Search & Items
  searchQuery = '';
  items = signal<PosItemDto[]>([]);
  private searchSubject = new Subject<string>();

  // Cart
  cart: CartItem[] = [];
  netTotal = 0;
  taxTotal = 0;
  discountTotal = 0;
  grandTotal = 0;

  // Payment
  payments: PaymentRow[] = [{ mode: 'Cash', amount: 0 }];
  totalPaid = computed(() => this.payments.reduce((s, p) => s + (p.amount || 0), 0));

  // Customer
  customerId: string | null = null;
  customerName = '';
  customerSearch = '';

  // State
  isProcessing = false;
  lastInvoice: string | null = null;
  companyId = '';

  // Hold / Resume
  heldOrders: HeldOrder[] = [];
  showHeldOrders = false;

  // Tax rate from settings (SST for Malaysia)
  defaultTaxRate = 6; // SST 6% — should come from company settings

  // Payment modes available
  paymentModes = ['Cash', 'Credit Card', 'Bank Transfer', 'E-Wallet', 'Cheque'];

  ngOnInit(): void {
    this.posService.searchItems({ maxResultCount: 30 }).subscribe((result) => {
      this.items.set(result.items ?? []);
    });

    this.searchSubject.pipe(debounceTime(300)).subscribe((query) => {
      this.posService.searchItems({ search: query, maxResultCount: 20 }).subscribe((result) => {
        this.items.set(result.items ?? []);
      });
    });

    // Load held orders from session storage
    const saved = sessionStorage.getItem('pos_held_orders');
    if (saved) this.heldOrders = JSON.parse(saved);
  }

  onSearchChange(query: string): void {
    this.searchSubject.next(query);
  }

  addToCart(item: PosItemDto): void {
    const existing = this.cart.find(c => c.itemId === item.id);
    if (existing) {
      existing.qty++;
      existing.amount = existing.qty * existing.rate * (1 - existing.discount / 100);
      existing.taxAmount = existing.amount * existing.taxRate / 100;
    } else {
      const rate = item.sellingPrice ?? 0;
      const taxRate = this.defaultTaxRate;
      this.cart.push({
        itemId: item.id!,
        itemName: item.itemName!,
        qty: 1,
        rate,
        amount: rate,
        discount: 0,
        taxRate,
        taxAmount: rate * taxRate / 100,
      });
    }
    this.recalculate();
  }

  removeFromCart(index: number): void {
    this.cart.splice(index, 1);
    this.recalculate();
  }

  updateQty(index: number, qty: number): void {
    if (qty <= 0) { this.removeFromCart(index); return; }
    const item = this.cart[index];
    item.qty = qty;
    item.amount = qty * item.rate * (1 - item.discount / 100);
    item.taxAmount = item.amount * item.taxRate / 100;
    this.recalculate();
  }

  updateDiscount(index: number, discount: number): void {
    const item = this.cart[index];
    item.discount = Math.min(100, Math.max(0, discount));
    item.amount = item.qty * item.rate * (1 - item.discount / 100);
    item.taxAmount = item.amount * item.taxRate / 100;
    this.recalculate();
  }

  recalculate(): void {
    this.netTotal = this.cart.reduce((s, c) => s + c.amount, 0);
    this.taxTotal = this.cart.reduce((s, c) => s + c.taxAmount, 0);
    this.discountTotal = this.cart.reduce((s, c) => s + (c.qty * c.rate - c.amount), 0);
    this.grandTotal = this.netTotal + this.taxTotal;

    // Auto-fill first payment row with grand total if only one payment mode
    if (this.payments.length === 1 && this.payments[0].amount === 0) {
      this.payments[0].amount = this.grandTotal;
    }
  }

  // Payment methods
  addPaymentRow(): void {
    this.payments.push({ mode: 'Cash', amount: 0 });
  }

  removePaymentRow(index: number): void {
    if (this.payments.length > 1) this.payments.splice(index, 1);
  }

  get changeAmount(): number {
    const paid = this.payments.reduce((s, p) => s + (p.amount || 0), 0);
    return Math.max(0, paid - this.grandTotal);
  }

  get outstandingAmount(): number {
    const paid = this.payments.reduce((s, p) => s + (p.amount || 0), 0);
    return Math.max(0, this.grandTotal - paid);
  }

  // Hold / Resume
  holdOrder(): void {
    if (this.cart.length === 0) return;
    const order: HeldOrder = {
      id: Date.now().toString(36),
      customer: this.customerName || 'Walk-in',
      items: [...this.cart],
      payments: [...this.payments],
      heldAt: new Date(),
    };
    this.heldOrders.push(order);
    sessionStorage.setItem('pos_held_orders', JSON.stringify(this.heldOrders));
    this.clearCart();
    this.toaster.info('Order held');
  }

  resumeOrder(index: number): void {
    const order = this.heldOrders[index];
    this.cart = [...order.items];
    this.payments = [...order.payments];
    this.customerName = order.customer;
    this.heldOrders.splice(index, 1);
    sessionStorage.setItem('pos_held_orders', JSON.stringify(this.heldOrders));
    this.showHeldOrders = false;
    this.recalculate();
  }

  deleteHeldOrder(index: number): void {
    this.heldOrders.splice(index, 1);
    sessionStorage.setItem('pos_held_orders', JSON.stringify(this.heldOrders));
  }

  clearCart(): void {
    this.cart = [];
    this.payments = [{ mode: 'Cash', amount: 0 }];
    this.customerId = null;
    this.customerName = '';
    this.netTotal = 0;
    this.taxTotal = 0;
    this.discountTotal = 0;
    this.grandTotal = 0;
    this.lastInvoice = null;
  }

  completeSale(): void {
    if (this.cart.length === 0) {
      this.toaster.warn('Cart is empty');
      return;
    }

    if (this.outstandingAmount > 0.01) {
      this.toaster.warn('Payment amount is less than total');
      return;
    }

    this.isProcessing = true;
    this.posService.completeSale({
      companyId: this.companyId,
      customerId: this.customerId || undefined,
      items: this.cart.map(c => ({
        itemId: c.itemId,
        description: c.itemName,
        quantity: c.qty,
        unitPrice: c.rate,
        discountPercentage: c.discount,
        taxAmount: c.taxAmount,
      })),
      paymentMethod: this.payments[0]?.mode ?? 'Cash',
      amountReceived: this.payments.reduce((s, p) => s + (p.amount || 0), 0),
    } as any).subscribe({
      next: (result) => {
        this.isProcessing = false;
        this.lastInvoice = result.invoiceNumber ?? null;
        this.toaster.success(`Sale completed: ${this.lastInvoice}`);
        this.clearCart();
      },
      error: (err) => {
        this.isProcessing = false;
        this.toaster.error(err?.error?.error?.message ?? 'Sale failed');
      },
    });
  }
}
