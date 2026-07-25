import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { BlanketOrderService } from '../../proxy/sales/blanket-order.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-blanket-order-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewBlanketOrder' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ 'Type' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true; onTypeChange()" [(ngModel)]="form.orderType">
              <option value="Selling">{{ 'Selling' | abpLocalization }}</option>
              <option value="Buying">{{ 'Buying' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'Party' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.partyId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (p of parties(); track p.id) {
                <option [value]="p.id">{{ p.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidFrom' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.fromDate" />
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ 'ValidUntil' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.toDate" />
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Rate' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="item.itemId">
                    <option value="">-- {{ 'SelectItem' | abpLocalization }} --</option>
                    @for (i of availableItems(); track i.id) {
                      <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option>
                    }
                  </select>
                </td>
                <td><input type="number" class="form-control form-control-sm" min="1" (ngModelChange)="isDirty=true" [(ngModel)]="item.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="item.rate" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index, 1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addItem()">
          <i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-end gap-2">
          <button class="btn btn-secondary" routerLink="/sales/blanket-orders">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class BlanketOrderFormComponent implements OnInit {
  private service = inject(BlanketOrderService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private itemService = inject(ItemService);

  saving = false;
  isDirty = false;
  parties = signal<{ id: string; name: string }[]>([]);
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);

  form: any = {
    orderType: 'Selling', fromDate: '', toDate: '', partyId: '',
    items: [{ itemId: '', qty: 1, rate: 0 }]
  };

  ngOnInit(): void {
    this.loadParties();
    this.loadItems();
  }

  onTypeChange(): void { this.form.partyId = ''; this.loadParties(); }

  addItem(): void { this.form.items.push({ itemId: '', qty: 1, rate: 0 }); this.isDirty = true; }

  save(): void {
    this.saving = true;
    const companyId = this.companyContext.currentCompanyId();
    const dto = {
      companyId,
      orderType: this.form.orderType,
      partyId: this.form.partyId || undefined,
      fromDate: this.form.fromDate,
      toDate: this.form.toDate,
      items: this.form.items
        .filter((i: any) => i.itemId)
        .map((i: any) => ({ itemId: i.itemId, qty: i.qty || 0, rate: i.rate || 0 }))
    };
    this.service.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/sales/blanket-orders']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }

  private loadParties(): void {
    if (this.form.orderType === 'Selling') {
      this.customerService.getList({ maxResultCount: 200 } as any).subscribe(r =>
        this.parties.set((r.items ?? []).map((c: any) => ({ id: c.id, name: c.customerName ?? c.name })))
      );
    } else {
      this.supplierService.getList({ maxResultCount: 200 } as any).subscribe(r =>
        this.parties.set((r.items ?? []).map((s: any) => ({ id: s.id, name: s.supplierName ?? s.name })))
      );
    }
  }

  private loadItems(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
  }
}