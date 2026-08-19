import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ContractService } from '../../proxy/crm/contract.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CustomerService } from '../../proxy/sales/customer.service';

@Component({
  selector: 'app-contract-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header">
        <h5 class="mb-0">{{ (isEditMode ? 'MyERP::EditContract' : 'MyERP::NewContract') | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ContractNumber' | abpLocalization }} *</label>
              <input type="text" class="form-control" formControlName="contractNumber" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::PartyType' | abpLocalization }} *</label>
              <select class="form-select" formControlName="partyType">
                <option value="Customer">{{ 'MyERP::Customer' | abpLocalization }}</option>
                <option value="Supplier">{{ 'MyERP::Supplier' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::Party' | abpLocalization }}</label>
              <select class="form-select" formControlName="partyId">
                <option value="">{{ '::Select' | abpLocalization }}</option>
                @for (p of parties(); track p.id) {
                  <option [value]="p.id">{{ p.name }}</option>
                }
              </select>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::StartDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="startDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::EndDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="endDate" />
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ContractValue' | abpLocalization }}</label>
              <input type="number" class="form-control" formControlName="contractValue" step="0.01" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-8">
              <label class="form-label">{{ 'MyERP::ContractTerms' | abpLocalization }}</label>
              <textarea class="form-control" formControlName="notes" rows="4"></textarea>
            </div>
            <div class="col-md-4">
              <div class="form-check mt-4">
                <input type="checkbox" class="form-check-input" formControlName="isAutoRenewal" id="autoRenew" />
                <label class="form-check-label" for="autoRenew">{{ 'MyERP::AutoRenew' | abpLocalization }}</label>
              </div>
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <div class="form-check mt-4">
                <input type="checkbox" class="form-check-input" formControlName="requiresFulfilment" id="requiresFulfilment" />
                <label class="form-check-label" for="requiresFulfilment">{{ 'MyERP::RequiresFulfilment' | abpLocalization }}</label>
              </div>
            </div>
            @if (form.get('requiresFulfilment')!.value) {
              <div class="col-md-4">
                <label class="form-label">{{ 'MyERP::FulfilmentDeadline' | abpLocalization }}</label>
                <input type="date" class="form-control" formControlName="fulfilmentDeadline" />
              </div>
            }
          </div>

          <div class="d-flex justify-content-end gap-2">
            <a routerLink=".." class="btn btn-secondary">{{ 'MyERP::Cancel' | abpLocalization }}</a>
            <button type="submit" class="btn btn-primary" [disabled]="!form.valid || saving">
              {{ 'MyERP::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class ContractFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ContractService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);
  private supplierService = inject(SupplierService);
  private customerService = inject(CustomerService);

  parties = signal<any[]>([]);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  editId: string | null = null;

  ngOnInit() {
    this.form = this.fb.group({
      contractNumber: ['', Validators.required],
      partyType: ['Customer', Validators.required],
      partyId: [null],
      startDate: [new Date().toISOString().substring(0, 10), Validators.required],
      endDate: [null],
      contractValue: [null],
      notes: [''],
      isAutoRenewal: [false],
      requiresFulfilment: [false],
      fulfilmentDeadline: [null],
    });

    this.loadParties(this.form.get('partyType')!.value);
    this.form.get('partyType')!.valueChanges.subscribe(t => {
      this.form.patchValue({ partyId: null });
      this.loadParties(t);
    });

    this.editId = this.route.snapshot.paramMap.get('id');
    if (this.editId) {
      this.isEditMode = true;
      this.service.get(this.editId).subscribe({
        next: (c) => {
          this.form.patchValue({
            contractNumber: c.contractNumber,
            partyType: c.partyType,
            partyId: c.partyId,
            startDate: c.startDate?.substring(0, 10),
            endDate: c.endDate?.substring(0, 10),
            contractValue: c.contractValue,
            notes: c.notes,
            isAutoRenewal: c.isAutoRenewal,
            requiresFulfilment: c.requiresFulfilment,
            fulfilmentDeadline: c.fulfilmentDeadline?.substring(0, 10),
          });
        },
      });
    }
  }

  private loadParties(type: string) {
    if (type === 'Supplier') {
      this.supplierService.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any).subscribe({
        next: (r: any) => this.parties.set(r.items ?? []),
        error: () => {}
      });
    } else {
      this.customerService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
        next: (r: any) => this.parties.set(r.items ?? []),
        error: () => {}
      });
    }
  }

  save() {
    if (!this.form.valid) return;
    this.saving = true;

    const payload = {
      ...this.form.value,
      companyId: this.companyContext.currentCompanyId,
    };

    const action$ = this.isEditMode
      ? this.service.update(this.editId!, payload)
      : this.service.create(payload);

    action$.subscribe({
      next: () => {
        this.toaster.success('MyERP::SuccessfullySaved');
        this.router.navigate(['../'], { relativeTo: this.route });
      },
      error: () => { this.saving = false; },
    });
  }
}
