import { Component, OnInit, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SubscriptionService } from '../../proxy/sales/subscription.service';
import { CompanyService } from '../../proxy/core/company.service';
import { MasterDataService } from '../../proxy/core/master-data.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CompanyDto, CostCenterLookupDto } from '../../proxy/core/models';
import type { CustomerDto, CreateSubscriptionDto } from '../../proxy/sales/models';
import type { SupplierDto } from '../../proxy/purchasing/models';
import type { ItemDto } from '../../proxy/inventory/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface SubscriptionPlanRow {
  itemId: string;
  itemName: string;
  qty: number;
  rate: number;
  costCenterId?: string | null;
  costCenterName?: string;
}

@Component({
  selector: 'app-subscription-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'::NewSubscription' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row g-3 mb-3">
          <div class="col-md-3">
            <label class="form-label">{{ '::Company' | abpLocalization }} *</label>
            <select class="form-select" [(ngModel)]="form.companyId" (ngModelChange)="onCompanyChange()">
              <option value="">{{ '::SelectCompany' | abpLocalization }}</option>
              @for (c of companies(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::PartyType' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.partyType" (ngModelChange)="onPartyTypeChange()">
              <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
              <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::Party' | abpLocalization }} *</label>
            <select class="form-select" [(ngModel)]="form.partyId" (ngModelChange)="onPartyChange()">
              <option value="">{{ '::SelectParty' | abpLocalization }}</option>
              @if (form.partyType === 'Supplier') {
                @for (s of suppliers(); track s.id) {
                  <option [value]="s.id">{{ s.name }}</option>
                }
              } @else {
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::BillingInterval' | abpLocalization }}</label>
            <select class="form-select" (ngModelChange)="isDirty=true" [(ngModel)]="form.billingInterval">
              <option value="Monthly">{{ '::Monthly' | abpLocalization }}</option>
              <option value="Quarterly">{{ '::Quarterly' | abpLocalization }}</option>
              <option value="Half-Yearly">Half-Yearly</option>
              <option value="Yearly">{{ '::Yearly' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::StartDate' | abpLocalization }}</label>
            <input type="date" class="form-control" (ngModelChange)="isDirty=true" [(ngModel)]="form.startDate" />
          </div>
        </div>

        <h6 class="mb-2">{{ '::Plans' | abpLocalization }}</h6>
        <table class="table table-sm align-middle">
          <thead>
            <tr>
              <th>{{ '::Item' | abpLocalization }}</th>
              <th>{{ '::CostCenter' | abpLocalization }}</th>
              <th style="width: 110px">{{ '::Quantity' | abpLocalization }}</th>
              <th style="width: 140px">{{ '::Rate' | abpLocalization }}</th>
              <th style="width: 48px"></th>
            </tr>
          </thead>
          <tbody>
            @for (p of form.plans; track $index; let i = $index) {
              <tr>
                <td>
                  <select class="form-select form-select-sm" [(ngModel)]="p.itemId" (ngModelChange)="onPlanItemChange(i)">
                    <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                    @for (it of items(); track it.id) {
                      <option [value]="it.id">{{ it.itemCode }} — {{ it.itemName }}</option>
                    }
                  </select>
                </td>
                <td>
                  @if (p.costCenterName) {
                    <span class="badge bg-info text-dark" [title]="'::AutoResolved' | abpLocalization">{{ p.costCenterName }}</span>
                  } @else {
                    <span class="text-muted small">—</span>
                  }
                </td>
                <td><input type="number" class="form-control form-control-sm" min="0.01" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="p.qty" /></td>
                <td><input type="number" class="form-control form-control-sm" min="0" step="0.01" (ngModelChange)="isDirty=true" [(ngModel)]="p.rate" /></td>
                <td>
                  <button class="btn btn-sm btn-outline-danger" type="button" (click)="removePlan(i)">
                    <i class="fa fa-trash"></i>
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
        <button class="btn btn-sm btn-outline-primary mb-3" type="button" (click)="addPlan()">
          <i class="fa fa-plus me-1"></i>{{ '::AddItem' | abpLocalization }}
        </button>

        <div class="d-flex justify-content-between align-items-center">
          <span class="fw-bold">{{ '::Total' | abpLocalization }}: {{ getTotal() | number:'1.2-2' }} / {{ form.billingInterval }}</span>
          <div class="d-flex gap-2">
            <a class="btn btn-secondary" routerLink="/sales/subscriptions">{{ '::Cancel' | abpLocalization }}</a>
            <button class="btn btn-primary" type="button" (click)="save()" [disabled]="saving || !isValid()">
              <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
            </button>
          </div>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class SubscriptionFormComponent implements OnInit {
  private service = inject(SubscriptionService);
  private companyService = inject(CompanyService);
  private masterDataService = inject(MasterDataService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private itemService = inject(ItemService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private router = inject(Router);

  saving = false;
  isDirty = false;
  companies = signal<CompanyDto[]>([]);
  customers = signal<CustomerDto[]>([]);
  suppliers = signal<SupplierDto[]>([]);
  items = signal<ItemDto[]>([]);
  costCenters = signal<CostCenterLookupDto[]>([]);

  form: {
    companyId: string;
    partyId: string;
    partyType: string;
    partyName: string;
    billingInterval: string;
    startDate: string;
    plans: SubscriptionPlanRow[];
  } = {
    companyId: '',
    partyId: '',
    partyType: 'Customer',
    partyName: '',
    billingInterval: 'Monthly',
    startDate: new Date().toISOString().split('T')[0],
    plans: [{ itemId: '', itemName: '', qty: 1, rate: 0 }],
  };

  ngOnInit(): void {
    const defaultCompanyId = this.companyContext.currentCompanyId() ?? '';
    if (defaultCompanyId) {
      this.form.companyId = defaultCompanyId;
      this.loadCostCenters(defaultCompanyId);
    }

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' }).subscribe({
      next: res => {
        const list = res.items ?? [];
        this.companies.set(list);
        if (!this.form.companyId && list.length > 0 && list[0].id) {
          this.form.companyId = list[0].id;
          this.loadCostCenters(list[0].id);
        }
      },
      error: () => {},
    });

    this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => this.customers.set(res.items ?? []),
      error: () => {},
    });

    this.supplierService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => this.suppliers.set(res.items ?? []),
      error: () => {},
    });

    this.itemService.getList({ skipCount: 0, maxResultCount: 500 } as any).subscribe({
      next: res => this.items.set(res.items ?? []),
      error: () => {},
    });
  }

  onCompanyChange(): void {
    this.isDirty = true;
    this.loadCostCenters(this.form.companyId);
    this.refreshAllPlanDimensions();
  }

  onPartyTypeChange(): void {
    this.isDirty = true;
    this.form.partyId = '';
    this.form.partyName = '';
    this.refreshAllPlanDimensions();
  }

  onPartyChange(): void {
    this.isDirty = true;
    const list = this.form.partyType === 'Supplier' ? this.suppliers() : this.customers();
    const match = list.find(p => p.id === this.form.partyId);
    this.form.partyName = match?.name ?? '';
  }

  onPlanItemChange(index: number): void {
    this.isDirty = true;
    const plan = this.form.plans[index];
    if (!plan) return;
    const item = this.items().find(i => i.id === plan.itemId);
    if (item) {
      plan.itemName = item.itemName ?? item.itemCode ?? '';
      const defaultPrice = this.form.partyType === 'Supplier'
        ? (item.standardBuyingPrice ?? item.standardSellingPrice ?? 0)
        : (item.standardSellingPrice ?? item.standardBuyingPrice ?? 0);
      if (!plan.rate && defaultPrice > 0) {
        plan.rate = defaultPrice;
      }
    } else {
      plan.itemName = '';
    }
    this.resolvePlanDimension(index);
  }

  addPlan(): void {
    this.isDirty = true;
    this.form.plans.push({ itemId: '', itemName: '', qty: 1, rate: 0 });
  }

  removePlan(index: number): void {
    this.isDirty = true;
    this.form.plans.splice(index, 1);
  }

  getTotal(): number {
    return this.form.plans.reduce((s, p) => s + (p.qty || 0) * (p.rate || 0), 0);
  }

  isValid(): boolean {
    return !!this.form.companyId
      && !!this.form.partyId
      && this.form.plans.length > 0
      && this.form.plans.every(p => !!p.itemId && (p.qty ?? 0) > 0);
  }

  save(): void {
    if (!this.isValid()) return;
    this.saving = true;
    const payload: CreateSubscriptionDto = {
      companyId: this.form.companyId,
      partyId: this.form.partyId,
      partyType: this.form.partyType,
      partyName: this.form.partyName,
      billingInterval: this.form.billingInterval,
      billingIntervalCount: 1,
      startDate: this.form.startDate,
      trialPeriodDays: 0,
      plans: this.form.plans.map(p => ({
        itemId: p.itemId,
        itemName: p.itemName,
        qty: p.qty,
        rate: p.rate,
      })),
    };
    this.service.create(payload).subscribe({
      next: () => {
        this.isDirty = false;
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/sales/subscriptions']);
      },
      error: (err: any) => {
        this.saving = false;
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      },
    });
  }

  hasUnsavedChanges(): boolean { return this.isDirty && !this.saving; }

  private loadCostCenters(companyId: string): void {
    if (!companyId) {
      this.costCenters.set([]);
      return;
    }
    this.masterDataService.getCostCenters(companyId).subscribe({
      next: list => {
        this.costCenters.set(list ?? []);
        for (const p of this.form.plans) {
          if (p.costCenterId) {
            p.costCenterName = (list ?? []).find(c => c.id === p.costCenterId)?.name ?? p.costCenterId;
          }
        }
      },
      error: () => {},
    });
  }

  private refreshAllPlanDimensions(): void {
    for (let i = 0; i < this.form.plans.length; i++) {
      this.resolvePlanDimension(i);
    }
  }

  private resolvePlanDimension(index: number): void {
    const plan = this.form.plans[index];
    if (!plan || !plan.itemId || !this.form.companyId) {
      if (plan) {
        plan.costCenterId = null;
        plan.costCenterName = '';
      }
      return;
    }
    this.service.getPlanDimensions(plan.itemId, this.form.companyId, this.form.partyType).subscribe({
      next: dims => {
        plan.costCenterId = dims?.costCenterId ?? null;
        plan.costCenterName = dims?.costCenterId
          ? (this.costCenters().find(c => c.id === dims.costCenterId)?.name ?? dims.costCenterId)
          : '';
      },
      error: () => {
        plan.costCenterId = null;
        plan.costCenterName = '';
      },
    });
  }
}
