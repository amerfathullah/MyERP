import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatListModule } from '@angular/material/list';
import { TaxCalculationService } from '../../shared/services/tax-calculation.service';

interface CartItem {
  itemName: string;
  qty: number;
  rate: number;
  amount: number;
}

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatDividerModule, MatListModule],
  templateUrl: './pos.component.html',
  styleUrls: ['./pos.component.scss'],
})
export class PosComponent {
  private fb = inject(FormBuilder);
  private taxCalc = inject(TaxCalculationService);

  searchQuery = '';
  cart: CartItem[] = [];
  netTotal = 0;
  grandTotal = 0;

  addToCart(itemName: string, rate: number): void {
    const existing = this.cart.find(c => c.itemName === itemName);
    if (existing) {
      existing.qty++;
      existing.amount = existing.qty * existing.rate;
    } else {
      this.cart.push({ itemName, qty: 1, rate, amount: rate });
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
    this.grandTotal = this.netTotal; // TODO: apply tax rules
  }

  completeSale(): void {
    // TODO: Create POS invoice, print receipt
    console.log('POS sale:', this.cart, this.grandTotal);
    this.cart = [];
    this.netTotal = 0;
    this.grandTotal = 0;
  }

  // Mock items for quick-add grid
  quickItems = [
    { name: 'Nasi Lemak', rate: 8.50 },
    { name: 'Teh Tarik', rate: 3.00 },
    { name: 'Roti Canai', rate: 2.50 },
    { name: 'Milo Ais', rate: 4.00 },
    { name: 'Nasi Goreng', rate: 10.00 },
    { name: 'Kopi O', rate: 2.00 },
  ];
}
