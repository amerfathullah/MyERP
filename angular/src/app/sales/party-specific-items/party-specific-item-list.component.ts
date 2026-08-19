import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { PartySpecificItemService } from '../../proxy/sales/party-specific-item.service';
import { PartySpecificItemPartyType } from '../../proxy/sales/party-specific-item-party-type.enum';
import { PartySpecificItemRestrictBasedOn } from '../../proxy/sales/party-specific-item-restrict-based-on.enum';
import { PartySpecificItemDto } from '../../proxy/sales/models';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { HierarchyMasterDataService } from '../../proxy/core/hierarchy-master-data.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { ItemGroupService } from '../../proxy/inventory/item-group.service';
import { BrandService } from '../../proxy/inventory/brand.service';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService, ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

interface LookupOption {
  id: string;
  label: string;
}

/**
 * Party Specific Item — restricts which items are selectable for a Customer/Customer Group/
 * Supplier/Supplier Group in item search on sales/purchase transaction rows.
 * Per ERPNext: Party Specific Item (selling/doctype/party_specific_item).
 * Enforcement: MyERP.Application.Inventory.ItemAppService.GetListAsync via PartySpecificItemFilterService.
 */
@Component({
  selector: 'app-party-specific-item-list',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-filter me-2"></i>{{ '::PartySpecificItems' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="openForm()">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>

        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
          } @else if (rules().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fas fa-filter fa-3x mb-2 d-block opacity-50"></i>
              <p>{{ '::NoPartySpecificItemsConfigured' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::PartyType' | abpLocalization }}</th>
                  <th>{{ '::PartyName' | abpLocalization }}</th>
                  <th>{{ '::RestrictItemsBasedOn' | abpLocalization }}</th>
                  <th>{{ '::BasedOnValue' | abpLocalization }}</th>
                  <th style="width:100px"></th>
                </tr>
              </thead>
              <tbody>
                @for (r of rules(); track r.id) {
                  <tr>
                    <td>{{ partyTypeLabel(r.partyType) }}</td>
                    <td class="fw-medium">{{ r.partyName || r.partyId }}</td>
                    <td>{{ restrictBasedOnLabel(r.restrictBasedOn) }}</td>
                    <td>{{ r.basedOnValueName || r.basedOnValueId }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-danger" (click)="deleteRule(r.id!)" title="Delete"><i class="fas fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>

      @if (showForm()) {
        <div class="card mt-3">
          <div class="card-header">
            <h6 class="mb-0">{{ '::NewPartySpecificItem' | abpLocalization }}</h6>
          </div>
          <div class="card-body">
            <form [formGroup]="form" (ngSubmit)="save()">
              <div class="row g-3 mb-3">
                <div class="col-md-3">
                  <label class="form-label">{{ '::PartyType' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="partyType" (change)="onPartyTypeChange()">
                    <option [ngValue]="PartyType.Customer">{{ '::Customer' | abpLocalization }}</option>
                    <option [ngValue]="PartyType.CustomerGroup">{{ '::CustomerGroup' | abpLocalization }}</option>
                    <option [ngValue]="PartyType.Supplier">{{ '::Supplier' | abpLocalization }}</option>
                    <option [ngValue]="PartyType.SupplierGroup">{{ '::SupplierGroup' | abpLocalization }}</option>
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::PartyName' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="partyId">
                    <option [ngValue]="null">—</option>
                    @for (p of partyOptions(); track p.id) {
                      <option [ngValue]="p.id">{{ p.label }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::RestrictItemsBasedOn' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="restrictBasedOn" (change)="onRestrictBasedOnChange()">
                    <option [ngValue]="RestrictBasedOn.Item">{{ '::Item' | abpLocalization }}</option>
                    <option [ngValue]="RestrictBasedOn.ItemGroup">{{ '::ItemGroup' | abpLocalization }}</option>
                    <option [ngValue]="RestrictBasedOn.Brand">{{ '::Brand' | abpLocalization }}</option>
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::BasedOnValue' | abpLocalization }} *</label>
                  <select class="form-select" formControlName="basedOnValueId">
                    <option [ngValue]="null">—</option>
                    @for (v of basedOnValueOptions(); track v.id) {
                      <option [ngValue]="v.id">{{ v.label }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="d-flex gap-2">
                <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">
                  @if (saving()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
                <button type="button" class="btn btn-secondary" (click)="cancelForm()">{{ '::Cancel' | abpLocalization }}</button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
})
export class PartySpecificItemListComponent implements OnInit {
  private ruleService = inject(PartySpecificItemService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);
  private hierarchyService = inject(HierarchyMasterDataService);
  private itemService = inject(ItemService);
  private itemGroupService = inject(ItemGroupService);
  private brandService = inject(BrandService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  readonly PartyType = PartySpecificItemPartyType;
  readonly RestrictBasedOn = PartySpecificItemRestrictBasedOn;

  rules = signal<PartySpecificItemDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);

  partyOptions = signal<LookupOption[]>([]);
  basedOnValueOptions = signal<LookupOption[]>([]);

  form = this.fb.group({
    partyType: [PartySpecificItemPartyType.Customer, Validators.required],
    partyId: [null as string | null, Validators.required],
    restrictBasedOn: [PartySpecificItemRestrictBasedOn.Item, Validators.required],
    basedOnValueId: [null as string | null, Validators.required],
  });

  ngOnInit(): void {
    this.loadRules();
  }

  partyTypeLabel(type?: PartySpecificItemPartyType): string {
    return type !== undefined ? PartySpecificItemPartyType[type] : '';
  }

  restrictBasedOnLabel(basedOn?: PartySpecificItemRestrictBasedOn): string {
    return basedOn !== undefined ? PartySpecificItemRestrictBasedOn[basedOn] : '';
  }

  loadRules(): void {
    this.loading.set(true);
    this.ruleService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' }).subscribe({
      next: res => { this.rules.set(res.items ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.form.reset({
      partyType: PartySpecificItemPartyType.Customer,
      restrictBasedOn: PartySpecificItemRestrictBasedOn.Item,
      partyId: null,
      basedOnValueId: null,
    });
    this.showForm.set(true);
    this.onPartyTypeChange();
    this.onRestrictBasedOnChange();
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  onPartyTypeChange(): void {
    const partyType = this.form.value.partyType!;
    this.form.patchValue({ partyId: null });
    this.partyOptions.set([]);

    switch (partyType) {
      case PartySpecificItemPartyType.Customer:
        this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' } as any).subscribe(res =>
          this.partyOptions.set((res.items ?? []).map(c => ({ id: c.id!, label: c.name || c.id! }))));
        break;
      case PartySpecificItemPartyType.CustomerGroup:
        this.hierarchyService.getCustomerGroups().subscribe(groups =>
          this.partyOptions.set((groups ?? []).map(g => ({ id: g.id!, label: g.name || g.id! }))));
        break;
      case PartySpecificItemPartyType.Supplier:
        this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' } as any).subscribe(res =>
          this.partyOptions.set((res.items ?? []).map(s => ({ id: s.id!, label: s.name || s.id! }))));
        break;
      case PartySpecificItemPartyType.SupplierGroup:
        this.hierarchyService.getSupplierGroups().subscribe(groups =>
          this.partyOptions.set((groups ?? []).map(g => ({ id: g.id!, label: g.name || g.id! }))));
        break;
    }
  }

  onRestrictBasedOnChange(): void {
    const restrictBasedOn = this.form.value.restrictBasedOn!;
    this.form.patchValue({ basedOnValueId: null });
    this.basedOnValueOptions.set([]);

    switch (restrictBasedOn) {
      case PartySpecificItemRestrictBasedOn.Item:
        this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'itemName asc' } as any).subscribe(res =>
          this.basedOnValueOptions.set((res.items ?? []).map(i => ({ id: i.id!, label: `${i.itemCode} - ${i.itemName}` }))));
        break;
      case PartySpecificItemRestrictBasedOn.ItemGroup:
        this.itemGroupService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' }).subscribe(res =>
          this.basedOnValueOptions.set((res.items ?? []).map(g => ({ id: g.id!, label: g.name || g.id! }))));
        break;
      case PartySpecificItemRestrictBasedOn.Brand:
        this.brandService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'name asc' }).subscribe(res =>
          this.basedOnValueOptions.set((res.items ?? []).map(b => ({ id: b.id!, label: b.name || b.id! }))));
        break;
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      partyType: this.form.value.partyType!,
      partyId: this.form.value.partyId!,
      restrictBasedOn: this.form.value.restrictBasedOn!,
      basedOnValueId: this.form.value.basedOnValueId!,
    };

    this.ruleService.create(payload).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadRules();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.saving.set(false);
      },
    });
  }

  deleteRule(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.ruleService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadRules(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }
}
