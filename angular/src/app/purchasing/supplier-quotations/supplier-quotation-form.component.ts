import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { SupplierQuotationService } from '../../proxy/purchasing/supplier-quotation.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';

@Component({
  selector: 'app-supplier-quotation-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewSupplierQuotation' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'Supplier' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.supplierId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (s of suppliers(); track s.id) {
                <option [value]="s.id">{{ s.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'Date' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.transactionDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'ValidTill' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.validTill" />
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
                <td><input type="number" class="form-control form-control-sm" min="0" (ngModelChange)="isDirty=true" [(ngModel)]="item.qty" /></td>
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
          <button class="btn btn-secondary" routerLink="/purchasing/supplier-quotations">{{ 'Cancel' | abpLocalization }}</button>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SupplierQuotationFormComponent implements OnInit {
  private supplierService = inject(SupplierService);
  private sqService = inject(SupplierQuotationService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  saving = false;
  isDirty = false;
  suppliers = signal<{ id: string; name: string }[]>([]);
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  form: any = {
    transactionDate: new Date().toISOString().split('T')[0], supplierId: '', validTill: '',
    items: [{ itemId: '', qty: 1, rate: 0 }]
  };

  ngOnInit() {
    this.supplierService.getList({ maxResultCount: 200 } as any).subscribe(r =>
      this.suppliers.set((r.items ?? []).map((s: any) => ({ id: s.id, name: s.supplierName ?? s.name })))
    );
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
  }

  addItem() { this.form.items.push({ itemId: '', qty: 1, rate: 0 }); this.isDirty = true; }

  save() {
    this.saving = true;
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      supplierId: this.form.supplierId || undefined,
      transactionDate: this.form.transactionDate,
      validTill: this.form.validTill || undefined,
      currency: 'MYR',
      items: this.form.items
        .filter((i: any) => i.itemId)
        .map((i: any) => ({ itemId: i.itemId, qty: i.qty || 0, rate: i.rate || 0 }))
    };
    this.sqService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/purchasing/supplier-quotations']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}
