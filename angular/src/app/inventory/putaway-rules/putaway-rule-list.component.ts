import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe , LocalizationService } from '@abp/ng.core';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Confirmation, ToasterService , ConfirmationService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-putaway-rule-list',
  imports: [CommonModule, FormsModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-warehouse me-2"></i>{{ '::PutawayRules' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showForm) {
            <div class="border rounded p-3 mb-3 bg-light">
              <div class="row g-2">
                <div class="col-md-3">
                  <label class="form-label">{{ '::Item' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="newItem.itemId">
                    <option [ngValue]="undefined">--</option>
                    @for (item of availableItems(); track item.id) {
                      <option [ngValue]="item.id">{{ item.itemCode }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Warehouse' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" [(ngModel)]="newItem.warehouseId">
                    <option [ngValue]="undefined">--</option>
                    @for (wh of warehouses(); track wh.id) {
                      <option [ngValue]="wh.id">{{ wh.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::Capacity' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="number" [(ngModel)]="newItem.stockCapacity" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::Priority' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="number" [(ngModel)]="newItem.priority" />
                </div>
                <div class="col-md-2 d-flex align-items-end">
                  <button class="btn btn-primary btn-sm" (click)="save()"><i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}</button>
                </div>
              </div>
            </div>
          }
          @if (items().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-warehouse fa-2x mb-2"></i>
              <p>{{ '::NoPutawayRulesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead><tr>
                <th>{{ '::Item' | abpLocalization }}</th>
                <th>{{ '::Warehouse' | abpLocalization }}</th>
                <th>{{ '::Capacity' | abpLocalization }}</th>
                <th>{{ '::Priority' | abpLocalization }}</th>
                <th>{{ '::Enabled' | abpLocalization }}</th>
                <th></th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td><a [routerLink]="[item.id]" class="text-decoration-none">{{ getItemName(item.itemId) }}</a></td>
                    <td>{{ getWarehouseName(item.warehouseId) }}</td>
                    <td>{{ item.stockCapacity || '∞' }}</td>
                    <td>{{ item.priority }}</td>
                    <td>
                      <button class="btn btn-sm" [class]="item.isEnabled ? 'btn-outline-success' : 'btn-outline-secondary'"
                              (click)="toggle(item.id)">
                        <i [class]="item.isEnabled ? 'fas fa-check' : 'fas fa-times'"></i>
                      </button>
                    </td>
                    <td><button class="btn btn-outline-danger btn-sm" (click)="remove(item.id)"><i class="fas fa-trash"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class PutawayRuleListComponent implements OnInit {
  private http = inject(HttpClient);
  private localization = inject(LocalizationService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<any[]>([]);
  availableItems = signal<any[]>([]);
  warehouses = signal<any[]>([]);
  showForm = false;
  newItem: any = { itemId: undefined, warehouseId: undefined, stockCapacity: 0, priority: 1 };

  private itemMap: Record<string, string> = {};
  private whMap: Record<string, string> = {};

  l(key: string) { return this.localization.instant(key); }

  ngOnInit() {
    this.load();
    this.http.get<any>('/api/app/item', { params: { maxResultCount: '500' } }).subscribe({
      next: res => {
        this.availableItems.set(res.items ?? []);
        (res.items ?? []).forEach((i: any) => this.itemMap[i.id] = i.itemCode || i.itemName);
      },
      error: () => {}
    });
    this.http.get<any>('/api/app/warehouse', { params: { maxResultCount: '200' } }).subscribe({
      next: res => {
        this.warehouses.set(res.items ?? []);
        (res.items ?? []).forEach((w: any) => this.whMap[w.id] = w.name);
      },
      error: () => {}
    });
  }

  load() {
    this.http.get<any>('/api/app/putaway-rule').subscribe({ next: res => this.items.set(res.items ?? []), error: () => {} });
  }

  getItemName(id: string) { return this.itemMap[id] || id?.substring(0, 8) + '…'; }
  getWarehouseName(id: string) { return this.whMap[id] || id?.substring(0, 8) + '…'; }

  save() {
    if (!this.newItem.itemId || !this.newItem.warehouseId) return;
    this.http.post('/api/app/putaway-rule', this.newItem).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.showForm = false; this.load(); },
      error: () => {}
    });
  }

  toggle(id: string) {
    this.http.post(`/api/app/putaway-rule/${id}/toggle`, {}).subscribe({
      next: () => { this.toaster.success('::SuccessfullyUpdated'); this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Toggle failed'),
    });
  }

  remove(id: string) {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.http.delete(`/api/app/putaway-rule/${id}`).subscribe({
        next: () => { this.toaster.success(this.l('::SuccessfullyDeleted')); this.load(); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message ?? '::DeleteFailed'),
      });
    });
  }
}
