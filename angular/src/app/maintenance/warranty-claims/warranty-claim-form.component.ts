import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { WarrantyClaimService } from '../../proxy/maintenance/warranty-claim.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { SerialNoService } from '../../proxy/inventory/serial-no.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-warranty-claim-form',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ 'MyERP::NewWarrantyClaim' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Customer' | abpLocalization }} *</label>
              <select class="form-select" formControlName="customerId">
                <option value="">{{ '::SelectCustomer' | abpLocalization }}</option>
                @for (c of customers(); track c.id) {
                  <option [value]="c.id">{{ c.name || c.customerName }}</option>
                }
              </select>
              @if (form.get('customerId')?.invalid && form.get('customerId')?.touched) {
                <div class="text-danger small mt-1">{{ '::RequiredField' | abpLocalization }}</div>
              }
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::Item' | abpLocalization }} *</label>
              <select class="form-select" formControlName="itemId" (change)="onItemChange()">
                <option value="">{{ '::SelectItem' | abpLocalization }}</option>
                @for (item of items(); track item.id) {
                  <option [value]="item.id">{{ item.itemCode }} - {{ item.itemName }}</option>
                }
              </select>
              @if (form.get('itemId')?.invalid && form.get('itemId')?.touched) {
                <div class="text-danger small mt-1">{{ '::RequiredField' | abpLocalization }}</div>
              }
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::ComplaintDate' | abpLocalization }} *</label>
              <input type="date" class="form-control" formControlName="complaintDate" />
              @if (form.get('complaintDate')?.invalid && form.get('complaintDate')?.touched) {
                <div class="text-danger small mt-1">{{ '::RequiredField' | abpLocalization }}</div>
              }
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::SerialNo' | abpLocalization }}</label>
              <select class="form-select" formControlName="serialNoId">
                <option value="">{{ '::SelectSerialNo' | abpLocalization }}</option>
                @for (s of serialNumbers(); track s.id) {
                  <option [value]="s.id">{{ s.serialNo }}</option>
                }
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label">{{ 'MyERP::SalesInvoice' | abpLocalization }}</label>
              <input type="text" class="form-control" formControlName="salesInvoiceId" placeholder="Invoice ID / Reference" />
            </div>
          </div>

          <div class="row mb-3">
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::WarrantyExpiryDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="warrantyExpiryDate" />
            </div>
            <div class="col-md-6">
              <label class="form-label">{{ 'MyERP::AmcExpiryDate' | abpLocalization }}</label>
              <input type="date" class="form-control" formControlName="amcExpiryDate" />
            </div>
          </div>

          <div class="mb-3">
            <label class="form-label">{{ 'MyERP::Complaint' | abpLocalization }} / Defect Description</label>
            <textarea class="form-control" rows="4" formControlName="complaint" placeholder="Describe the customer complaint or product issue..."></textarea>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-4">
            <button type="button" class="btn btn-outline-secondary" routerLink="/maintenance/warranty-claims">
              {{ '::Cancel' | abpLocalization }}
            </button>
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid || isSaving()">
              @if (isSaving()) {
                <i class="fa fa-spinner fa-spin me-1"></i>
              }
              {{ '::Save' | abpLocalization }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class WarrantyClaimFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private claimService = inject(WarrantyClaimService);
  private customerService = inject(CustomerService);
  private itemService = inject(ItemService);
  private serialNoService = inject(SerialNoService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  customers = signal<any[]>([]);
  items = signal<any[]>([]);
  serialNumbers = signal<any[]>([]);
  isSaving = signal(false);

  form: FormGroup = this.fb.group({
    companyId: ['', Validators.required],
    customerId: ['', Validators.required],
    itemId: ['', Validators.required],
    serialNoId: [''],
    salesInvoiceId: [''],
    complaintDate: [new Date().toISOString().substring(0, 10), Validators.required],
    warrantyExpiryDate: [''],
    amcExpiryDate: [''],
    complaint: ['']
  });

  ngOnInit() {
    const companyId = this.companyContext.selectedCompanyId() || '00000000-0000-0000-0000-000000000001';
    this.form.patchValue({ companyId });

    this.customerService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.customers.set(res.items || [])
    });

    this.itemService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (res: any) => this.items.set(res.items || [])
    });
  }

  onItemChange() {
    const itemId = this.form.get('itemId')?.value;
    if (!itemId) {
      this.serialNumbers.set([]);
      return;
    }

    this.serialNoService.getList({ maxResultCount: 100, skipCount: 0, itemId } as any).subscribe({
      next: (res: any) => this.serialNumbers.set(res.items || []),
      error: () => this.serialNumbers.set([])
    });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const val = this.form.value;

    const dto = {
      companyId: val.companyId,
      customerId: val.customerId,
      itemId: val.itemId,
      serialNoId: val.serialNoId || null,
      salesInvoiceId: val.salesInvoiceId || null,
      complaintDate: val.complaintDate,
      warrantyExpiryDate: val.warrantyExpiryDate || null,
      amcExpiryDate: val.amcExpiryDate || null,
      complaint: val.complaint || null
    };

    this.claimService.create(dto).subscribe({
      next: (res) => {
        this.toaster.success('Warranty claim created successfully');
        this.router.navigate(['/maintenance/warranty-claims', res.id]);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.toaster.error(err?.error?.error?.message || 'Failed to create warranty claim');
      }
    });
  }
}
