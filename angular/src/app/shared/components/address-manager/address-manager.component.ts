import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AddressService } from '../../../proxy/core/address.service';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';

interface AddressDto {
  id: string;
  title: string;
  addressType: string;
  addressLine1: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country: string;
  phone?: string;
  email?: string;
  isPrimaryAddress: boolean;
  isShippingAddress: boolean;
}

/**
 * Address Management Component — reusable panel for managing multiple addresses.
 * Embedded in Customer Detail, Supplier Detail, Company Settings, Employee Detail.
 * 
 * Features:
 * - List all addresses for a party
 * - Add new address with type (Billing, Shipping, Office, Warehouse)
 * - Edit existing addresses inline
 * - Set primary/shipping flags (auto-deselects other primary)
 * - Delete addresses
 * - Malaysian state dropdown for LHDN compliance
 * 
 * Usage: <app-address-manager [partyType]="'Customer'" [partyId]="customerId" />
 */
@Component({
  selector: 'app-address-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h6 class="mb-0"><i class="fas fa-map-marker-alt me-2"></i>{{ '::Addresses' | abpLocalization }}</h6>
        <button class="btn btn-primary btn-sm" (click)="openForm()">
          <i class="fas fa-plus me-1"></i>{{ '::AddAddress' | abpLocalization }}
        </button>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        } @else if (addresses().length === 0 && !showForm()) {
          <div class="text-center text-muted py-3">
            <i class="fas fa-map-marker-alt fa-2x mb-2 d-block opacity-50"></i>
            <p>No addresses added yet.</p>
          </div>
        } @else {
          <div class="row g-3">
            @for (addr of addresses(); track addr.id) {
              <div class="col-md-6">
                <div class="card h-100 border" [class.border-primary]="addr.isPrimaryAddress">
                  <div class="card-body">
                    <div class="d-flex justify-content-between align-items-start mb-2">
                      <div>
                        <strong>{{ addr.title }}</strong>
                        <span class="badge ms-2" [class.bg-primary]="addr.addressType === 'Billing'" [class.bg-success]="addr.addressType === 'Shipping'" [class.bg-secondary]="addr.addressType !== 'Billing' && addr.addressType !== 'Shipping'">
                          {{ addr.addressType }}
                        </span>
                      </div>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-secondary" (click)="editAddress(addr)" title="Edit">
                          <i class="fas fa-pencil-alt"></i>
                        </button>
                        <button class="btn btn-outline-danger" (click)="deleteAddress(addr.id)" title="Delete">
                          <i class="fas fa-trash"></i>
                        </button>
                      </div>
                    </div>
                    <p class="mb-1">{{ addr.addressLine1 }}</p>
                    @if (addr.addressLine2) { <p class="mb-1">{{ addr.addressLine2 }}</p> }
                    <p class="mb-1">
                      {{ addr.city }}@if (addr.state) {, {{ addr.state }}} {{ addr.postalCode }}
                    </p>
                    <p class="mb-1 text-muted">{{ addr.country }}</p>
                    @if (addr.phone) { <p class="mb-0 small"><i class="fas fa-phone me-1"></i>{{ addr.phone }}</p> }
                    @if (addr.email) { <p class="mb-0 small"><i class="fas fa-envelope me-1"></i>{{ addr.email }}</p> }
                    <div class="mt-2">
                      @if (addr.isPrimaryAddress) { <span class="badge bg-primary-subtle text-primary me-1">Primary</span> }
                      @if (addr.isShippingAddress) { <span class="badge bg-success-subtle text-success">Shipping</span> }
                    </div>
                  </div>
                </div>
              </div>
            }
          </div>
        }

        <!-- Add/Edit Form -->
        @if (showForm()) {
          <div class="border rounded p-3 mt-3 bg-light">
            <h6 class="mb-3">{{ editingId() ? ('::EditAddress' | abpLocalization) : ('::NewAddress' | abpLocalization) }}</h6>
            <form [formGroup]="form" (ngSubmit)="saveAddress()">
              <div class="row g-2">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Title' | abpLocalization }} *</label>
                  <input class="form-control form-control-sm" formControlName="title" [placeholder]="'::Placeholder:HeadOffice' | abpLocalization" />
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::Type' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" formControlName="addressType">
                    <option value="Billing">Billing</option>
                    <option value="Shipping">Shipping</option>
                    <option value="Office">Office</option>
                    <option value="Warehouse">Warehouse</option>
                    <option value="Other">Other</option>
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::Country' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="country" />
                </div>
              </div>
              <div class="row g-2 mt-1">
                <div class="col-md-6">
                  <label class="form-label">{{ '::AddressLine1' | abpLocalization }} *</label>
                  <input class="form-control form-control-sm" formControlName="addressLine1" />
                </div>
                <div class="col-md-6">
                  <label class="form-label">{{ '::AddressLine2' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="addressLine2" />
                </div>
              </div>
              <div class="row g-2 mt-1">
                <div class="col-md-3">
                  <label class="form-label">{{ '::City' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="city" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::State' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" formControlName="state">
                    <option value="">—</option>
                    @for (s of malaysianStates; track s) {
                      <option [value]="s">{{ s }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::PostalCode' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="postalCode" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Phone' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="phone" />
                </div>
              </div>
              <div class="row g-2 mt-1">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Email' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="email" formControlName="email" />
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isPrimary" formControlName="isPrimaryAddress" />
                    <label class="form-check-label" for="isPrimary">Primary Address</label>
                  </div>
                </div>
                <div class="col-md-4 d-flex align-items-end">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isShipping" formControlName="isShippingAddress" />
                    <label class="form-check-label" for="isShipping">Shipping Address</label>
                  </div>
                </div>
              </div>
              <div class="mt-3 d-flex gap-2">
                <button type="submit" class="btn btn-primary btn-sm" [disabled]="form.invalid || saving()">
                  @if (saving()) { <span class="spinner-border spinner-border-sm me-1"></span> }
                  <i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
                <button type="button" class="btn btn-secondary btn-sm" (click)="cancelForm()">{{ '::Cancel' | abpLocalization }}</button>
              </div>
            </form>
          </div>
        }
      </div>
    </div>
  `,
})
export class AddressManagerComponent implements OnInit {
  @Input() partyType!: string;
  @Input() partyId!: string;

  private addressService = inject(AddressService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);

  addresses = signal<AddressDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  // Malaysian states per LHDN specification (state codes 01-16 + 17 Federal Territories)
  malaysianStates = [
    'Johor', 'Kedah', 'Kelantan', 'Melaka', 'Negeri Sembilan',
    'Pahang', 'Perak', 'Perlis', 'Pulau Pinang', 'Sabah',
    'Sarawak', 'Selangor', 'Terengganu',
    'W.P. Kuala Lumpur', 'W.P. Labuan', 'W.P. Putrajaya',
  ];

  form = this.fb.group({
    title: ['', Validators.required],
    addressType: ['Billing'],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: [''],
    state: [''],
    postalCode: [''],
    country: ['Malaysia'],
    phone: [''],
    email: [''],
    isPrimaryAddress: [false],
    isShippingAddress: [false],
  });

  ngOnInit(): void {
    this.loadAddresses();
  }

  loadAddresses(): void {
    this.loading.set(true);
    this.addressService.getAddressesForParty(this.partyType, this.partyId).subscribe({
      next: (data) => { this.addresses.set(data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.form.reset({ addressType: 'Billing', country: 'Malaysia', isPrimaryAddress: false, isShippingAddress: false });
    this.editingId.set(null);
    this.showForm.set(true);
  }

  editAddress(addr: AddressDto): void {
    this.editingId.set(addr.id);
    this.form.patchValue({
      title: addr.title,
      addressType: addr.addressType,
      addressLine1: addr.addressLine1,
      addressLine2: addr.addressLine2 || '',
      city: addr.city || '',
      state: addr.state || '',
      postalCode: addr.postalCode || '',
      country: addr.country,
      phone: addr.phone || '',
      email: addr.email || '',
      isPrimaryAddress: addr.isPrimaryAddress,
      isShippingAddress: addr.isShippingAddress,
    });
    this.showForm.set(true);
  }

  saveAddress(): void {
    if (this.form.invalid) return;
    this.saving.set(true);

    const payload = {
      ...this.form.value,
      partyType: this.partyType,
      partyId: this.partyId,
    };

    const request$ = this.editingId()
      ? this.addressService.update(this.editingId()!, payload as any)
      : this.addressService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadAddresses();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.saving.set(false);
      },
    });
  }

  private confirmation = inject(ConfirmationService);

  deleteAddress(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.addressService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadAddresses(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }
}
