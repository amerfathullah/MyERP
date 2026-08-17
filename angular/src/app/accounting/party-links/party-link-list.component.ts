import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { PartyLinkService } from '../../proxy/accounting/party-link.service';
import type { PartyLinkDto } from '../../proxy/accounting/models';
import { CustomerService } from '../../proxy/sales/customer.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';

@Component({
  selector: 'app-party-link-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::PartyLinks' | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <h6 class="mb-3">{{ '::NewPartyLink' | abpLocalization }}</h6>
        <form [formGroup]="form" (ngSubmit)="create()" class="row g-3 align-items-end">
          <div class="col-md-2">
            <label class="form-label">{{ '::PrimaryPartyType' | abpLocalization }}</label>
            <select class="form-select" formControlName="primaryPartyType">
              <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
              <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::PrimaryParty' | abpLocalization }}</label>
            <select class="form-select" formControlName="primaryPartyId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @if (form.value.primaryPartyType === 'Customer') {
                @for (c of customers(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
              } @else {
                @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
              }
            </select>
          </div>
          <div class="col-md-2">
            <label class="form-label">{{ '::SecondaryPartyType' | abpLocalization }}</label>
            <select class="form-select" formControlName="secondaryPartyType">
              <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
              <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
            </select>
          </div>
          <div class="col-md-3">
            <label class="form-label">{{ '::SecondaryParty' | abpLocalization }}</label>
            <select class="form-select" formControlName="secondaryPartyId">
              <option value="">-- {{ 'Select' | abpLocalization }} --</option>
              @if (form.value.secondaryPartyType === 'Customer') {
                @for (c of customers(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
              } @else {
                @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.name }}</option> }
              }
            </select>
          </div>
          <div class="col-md-2">
            <button type="submit" class="btn btn-primary w-100" [disabled]="form.invalid">
              <i class="fa fa-link me-1"></i>{{ '::Link' | abpLocalization }}
            </button>
          </div>
        </form>
      </div></div>

      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ '::PrimaryPartyType' | abpLocalization }}</th>
              <th>{{ '::SecondaryPartyType' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.primaryPartyType }} — {{ partyName(item.primaryPartyType, item.primaryPartyId) }}</td>
                <td>{{ item.secondaryPartyType }} — {{ partyName(item.secondaryPartyType, item.secondaryPartyId) }}</td>
                <td><button class="btn btn-sm btn-outline-danger" (click)="delete(item)"><i class="fa fa-trash"></i></button></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class PartyLinkListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(PartyLinkService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  private customerService = inject(CustomerService);
  private supplierService = inject(SupplierService);

  items = signal<PartyLinkDto[]>([]);
  customers = signal<any[]>([]);
  suppliers = signal<any[]>([]);

  form = this.fb.group({
    primaryPartyType: ['Customer', Validators.required],
    primaryPartyId: ['', Validators.required],
    secondaryPartyType: ['Supplier', Validators.required],
    secondaryPartyId: ['', Validators.required],
  });

  ngOnInit(): void {
    this.load();
    this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe({ next: (r) => this.customers.set(r.items ?? []), error: () => {} });
    this.supplierService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe({ next: (r) => this.suppliers.set(r.items ?? []), error: () => {} });
  }

  load(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  partyName(type: string | undefined, id: string | undefined): string {
    if (!id) return '—';
    const list = type === 'Customer' ? this.customers() : this.suppliers();
    return list.find((p) => p.id === id)?.name ?? id;
  }

  create(): void {
    if (this.form.invalid) return;
    this.service.create(this.form.getRawValue() as any).subscribe({
      next: () => { this.toaster.success('::SuccessfullyCreated'); this.load(); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  delete(item: PartyLinkDto): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        this.service.delete(item.id!).subscribe({
          next: () => { this.toaster.success('::SuccessfullyDeleted'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
        });
      }
    });
  }
}
