import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
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
}

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [
    CommonModule, FormsModule, PageModule, LocalizationModule],
  templateUrl: './pos.component.html',
  styleUrls: ['./pos.component.scss'],
})
export class PosComponent implements OnInit {
  private posService = inject(PosService);
  private toaster = inject(ToasterService);

  searchQuery = '';
  cart: CartItem[] = [];
  netTotal = 0;
  grandTotal = 0;
  amountReceived = 0;
  change = 0;
  isProcessing = false;
  lastInvoice: string | null = null;

  items = signal<PosItemDto[]>([]);
  private searchSubject = new Subject<string>();

  companyId = '';

  ngOnInit(): void {
    this.posService.searchItems({ maxResultCount: 30 }).subscribe((result) => {
      this.items.set(result.items ?? []);
    });

    this.searchSubject.pipe(debounceTime(300)).subscribe((query) => {
      this.posService.searchItems({ search: query, maxResultCount: 20 }).subscribe((result) => {
        this.items.set(result.items ?? []);
      });
    });
  }

  onSearchChange(query: string): void {
    this.searchSubject.next(query);
  }

  addToCart(item: PosItemDto): void {
    const existing = this.cart.find(c => c.itemId === item.id);
    if (existing) {
      existing.qty++;
      existing.amount = existing.qty * existing.rate;
    } else {
      this.cart.push({
        itemId: item.id!,
        itemName: item.itemName!,
        qty: 1,
        rate: item.sellingPrice ?? 0,
        amount: item.sellingPrice ?? 0,
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
    this.cart[index].qty = qty;
    this.cart[index].amount = qty * this.cart[index].rate;
    this.recalculate();
  }

  recalculate(): void {
    this.netTotal = this.cart.reduce((s, c) => s + c.amount, 0);
    this.grandTotal = this.netTotal;
    this.change = Math.max(0, this.amountReceived - this.grandTotal);
  }

  completeSale(): void {
    if (this.cart.length === 0) {
      this.toaster.warn('Cart is empty');
      return;
    }

    this.isProcessing = true;
    this.posService.completeSale({
      companyId: this.companyId,
      items: this.cart.map(c => ({
        itemId: c.itemId,
        description: c.itemName,
        quantity: c.qty,
        unitPrice: c.rate,
        taxAmount: 0,
      })),
      paymentMethod: 'Cash',
      amountReceived: this.amountReceived || this.grandTotal,
    }).subscribe({
      next: (result) => {
        this.isProcessing = false;
        this.lastInvoice = result.invoiceNumber ?? null;
        this.toaster.success(`Sale completed! Invoice: ${result.invoiceNumber}. Change: MYR ${result.change?.toFixed(2)}`);
        this.cart = [];
        this.netTotal = 0;
        this.grandTotal = 0;
        this.amountReceived = 0;
        this.change = 0;
      },
      error: (err: any) => {
        this.isProcessing = false;
        this.toaster.error(err?.error?.error?.message ?? 'Sale failed');
      },
    });
  }
}
