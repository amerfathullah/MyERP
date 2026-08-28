import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LandedCostVoucherService } from '../../proxy/inventory';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { AccountService } from '../../proxy/accounting/account.service';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import { ProjectService } from '../../proxy/projects/project.service';

@Component({
  selector: 'app-lcv-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewLandedCostVoucher' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-4">
            <label class="form-label">{{ 'PostingDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.postingDate" />
          </div>
          <div class="col-md-4">
            <label class="form-label">{{ 'DistributionMethod' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.distributionMethod">
              <option [ngValue]="0">{{ 'ByQuantity' | abpLocalization }}</option>
              <option [ngValue]="1">{{ 'ByAmount' | abpLocalization }}</option>
              <option [ngValue]="2">{{ 'Manual' | abpLocalization }}</option>
            </select>
          </div>
        </div>

        <h6 class="mb-2">{{ 'Items' | abpLocalization }} (Receipt Items)</h6>
        <div class="row mb-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label">{{ 'ReceiptType' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="loadReceiptType" (ngModelChange)="loadReceiptId=''" [ngModelOptions]="{standalone: true}">
              <option value="PurchaseReceipt">PurchaseReceipt</option>
              <option value="PurchaseInvoice">PurchaseInvoice</option>
            </select>
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'SubmittedReceipt' | abpLocalization }}</label>
            <select class="form-select form-select-sm" [(ngModel)]="loadReceiptId" [ngModelOptions]="{standalone: true}">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @for (r of filteredReceipts(); track r.id) {
                <option [value]="r.id">{{ r.number }}</option>
              }
            </select>
          </div>
          <div class="col-md-3">
            <button class="btn btn-sm btn-outline-primary" [disabled]="!loadReceiptId" (click)="loadItemsFromReceipt()">
              <i class="fa fa-download me-1"></i>{{ 'LoadItems' | abpLocalization }}
            </button>
          </div>
        </div>
        <table class="table table-sm">
          <thead><tr><th>{{ 'ReceiptType' | abpLocalization }}</th><th>{{ 'Item' | abpLocalization }}</th><th>{{ 'Quantity' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of form.items; track $index) {
              <tr>
                <td>{{ item.receiptType }}</td>
                <td>{{ itemLabel(item.itemId) }}</td>
                <td><input type="number" class="form-control form-control-sm" min="0" (ngModelChange)="isDirty=true" [(ngModel)]="item.quantity" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="item.amount" /></td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.items.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
            @empty {
              <tr><td colspan="5" class="text-muted">{{ 'NoItemsLoaded' | abpLocalization }}</td></tr>
            }
          </tbody>
        </table>

        <h6 class="mb-2">{{ 'Charges' | abpLocalization }}</h6>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Account' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th>{{ 'Amount' | abpLocalization }}</th><th>{{ 'CostCenter' | abpLocalization }}</th><th>{{ 'Project' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (ch of form.charges; track $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="ch.expenseAccountId">
                    <option value="">-- {{ 'SelectAccount' | abpLocalization }} --</option>
                    @for (a of accounts(); track a.id) {
                      <option [value]="a.id">{{ a.accountCode }} — {{ a.accountName }}</option>
                    }
                  </select>
                </td>
                <td><input class="form-control form-control-sm" (ngModelChange)="isDirty=true" [(ngModel)]="ch.description" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="ch.amount" /></td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="ch.costCenterId">
                    <option [ngValue]="null">--</option>
                    @for (cc of costCenters(); track cc.id) {
                      <option [value]="cc.id">{{ cc.name }}</option>
                    }
                  </select>
                </td>
                <td>
                  <select class="form-select form-select-sm" (ngModelChange)="isDirty=true" [(ngModel)]="ch.projectId">
                    <option [ngValue]="null">--</option>
                    @for (p of projects(); track p.id) {
                      <option [value]="p.id">{{ p.projectName }}</option>
                    }
                  </select>
                </td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="form.charges.splice($index,1); isDirty=true"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" (click)="addCharge()"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>

        <div class="d-flex justify-content-between align-items-center">
          <span class="fw-bold">{{ 'TotalCharges' | abpLocalization }}: {{ getTotalCharges() | number:'1.2-2' }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/inventory/landed-costs">{{ 'Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" (click)="save()" [disabled]="saving"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class LandedCostFormComponent implements OnInit {
  private landedCostVoucherService = inject(LandedCostVoucherService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);
  private accountService = inject(AccountService);
  private purchaseReceiptService = inject(PurchaseReceiptService);
  private purchaseInvoiceService = inject(PurchaseInvoiceService);
  private costCenterService = inject(CostCenterService);
  private projectService = inject(ProjectService);

  saving = false;
  isDirty = false;
  loadReceiptType = 'PurchaseReceipt';
  loadReceiptId = '';
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  accounts = signal<{ id: string; accountCode: string; accountName: string }[]>([]);
  receipts = signal<{ id: string; number: string; status?: string }[]>([]);
  invoices = signal<{ id: string; number: string; status?: string }[]>([]);
  costCenters = signal<{ id: string; name: string }[]>([]);
  projects = signal<{ id: string; projectName: string }[]>([]);

  filteredReceipts = computed(() => {
    const source = this.loadReceiptType === 'PurchaseInvoice' ? this.invoices() : this.receipts();
    return source.filter(r => r.status === 'Submitted');
  });

  form: any = {
    postingDate: new Date().toISOString().split('T')[0], distributionMethod: 1,
    items: [] as any[],
    charges: [{ expenseAccountId: '', description: '', amount: 0, costCenterId: null, projectId: null }]
  };

  ngOnInit(): void {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
    this.accountService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe(r =>
      this.accounts.set((r.items ?? []).map((a: any) => ({ id: a.id, accountCode: a.accountCode, accountName: a.accountName })))
    );
    this.purchaseReceiptService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (r) => this.receipts.set((r.items ?? []).map((p: any) => ({ id: p.id, number: p.receiptNumber ?? p.id, status: p.status }))),
      error: () => {}
    });
    this.purchaseInvoiceService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe({
      next: (r) => this.invoices.set((r.items ?? []).map((p: any) => ({ id: p.id, number: p.invoiceNumber ?? p.id, status: p.status }))),
      error: () => {}
    });
    this.costCenterService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe({
      next: (r) => this.costCenters.set((r.items ?? []).map((cc: any) => ({ id: cc.id, name: cc.name }))),
      error: () => {}
    });
    this.projectService.getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any).subscribe({
      next: (r) => this.projects.set((r.items ?? []).map((p: any) => ({ id: p.id, projectName: p.projectName }))),
      error: () => {}
    });
  }

  itemLabel(itemId: string): string {
    const i = this.availableItems().find(x => x.id === itemId);
    return i ? `${i.itemCode} — ${i.itemName}` : itemId;
  }

  loadItemsFromReceipt() {
    if (!this.loadReceiptId) return;
    const companyId = this.companyContext.currentCompanyId();
    this.landedCostVoucherService.getReceiptItems({
      companyId, receiptType: this.loadReceiptType, receiptIds: [this.loadReceiptId]
    }).subscribe({
      next: (items) => {
        for (const it of items) {
          this.form.items.push({
            receiptId: it.receiptId, receiptType: it.receiptType, itemId: it.itemId,
            quantity: it.quantity || 0, amount: it.amount || 0,
          });
        }
        this.isDirty = true;
        this.loadReceiptId = '';
      }
    });
  }

  addCharge() { this.form.charges.push({ expenseAccountId: '', description: '', amount: 0, costCenterId: null, projectId: null }); this.isDirty = true; }
  getTotalCharges(): number { return this.form.charges.reduce((s: number, c: any) => s + (c.amount || 0), 0); }

  save() {
    this.saving = true;
    const companyId = this.companyContext.currentCompanyId();
    const dto = {
      companyId,
      postingDate: this.form.postingDate,
      distributionMethod: this.form.distributionMethod,
      items: this.form.items
        .filter((i: any) => i.itemId && i.receiptId)
        .map((i: any) => ({
          receiptId: i.receiptId,
          receiptType: i.receiptType,
          itemId: i.itemId,
          quantity: i.quantity || 0,
          amount: i.amount || 0,
        })),
      charges: this.form.charges
        .filter((c: any) => c.expenseAccountId && c.amount > 0)
        .map((c: any) => ({
          expenseAccountId: c.expenseAccountId, description: c.description || '', amount: c.amount,
          costCenterId: c.costCenterId || null, projectId: c.projectId || null,
        }))
    };
    this.landedCostVoucherService.create(dto).subscribe({
      next: () => { this.isDirty = false; this.router.navigate(['/inventory/landed-costs']); },
      error: () => { this.saving = false; }
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }
}