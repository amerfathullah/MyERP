import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ContactService } from '../../../proxy/core/contact.service';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';

interface ContactDto {
  id: string;
  firstName: string;
  lastName?: string;
  salutation?: string;
  designation?: string;
  department?: string;
  email?: string;
  phone?: string;
  mobileNo?: string;
  isPrimaryContact: boolean;
  isBillingContact: boolean;
}

/**
 * Contact Management Component — reusable panel for managing contacts per party.
 * Embedded in Customer Detail, Supplier Detail, Company Settings.
 * 
 * Usage: <app-contact-manager [partyType]="'Customer'" [partyId]="customerId" />
 */
@Component({
  selector: 'app-contact-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, LocalizationPipe],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h6 class="mb-0"><i class="fas fa-users me-2"></i>{{ '::Contacts' | abpLocalization }}</h6>
        <button class="btn btn-primary btn-sm" (click)="openForm()">
          <i class="fas fa-plus me-1"></i>{{ '::AddContact' | abpLocalization }}
        </button>
      </div>
      <div class="card-body">
        @if (loading()) {
          <div class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div></div>
        } @else if (contacts().length === 0 && !showForm()) {
          <div class="text-center text-muted py-3">
            <i class="fas fa-user-plus fa-2x mb-2 d-block opacity-50"></i>
            <p>No contacts added yet.</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-sm table-hover mb-0">
              <thead class="table-light">
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::Designation' | abpLocalization }}</th>
                  <th>{{ '::Email' | abpLocalization }}</th>
                  <th>{{ '::Phone' | abpLocalization }}</th>
                  <th>{{ '::Flags' | abpLocalization }}</th>
                  <th style="width:80px"></th>
                </tr>
              </thead>
              <tbody>
                @for (c of contacts(); track c.id) {
                  <tr>
                    <td>
                      @if (c.salutation) { <span class="text-muted">{{ c.salutation }}</span> }
                      <strong>{{ c.firstName }}</strong> {{ c.lastName }}
                    </td>
                    <td>{{ c.designation || '—' }}</td>
                    <td>
                      @if (c.email) { <a [href]="'mailto:' + c.email">{{ c.email }}</a> }
                      @if (!c.email) { — }
                    </td>
                    <td>{{ c.phone || c.mobileNo || '—' }}</td>
                    <td>
                      @if (c.isPrimaryContact) { <span class="badge bg-primary-subtle text-primary me-1">Primary</span> }
                      @if (c.isBillingContact) { <span class="badge bg-info-subtle text-info">Billing</span> }
                    </td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-secondary" (click)="editContact(c)"><i class="fas fa-pencil-alt"></i></button>
                        <button class="btn btn-outline-danger" (click)="deleteContact(c.id)"><i class="fas fa-trash"></i></button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }

        @if (showForm()) {
          <div class="border rounded p-3 mt-3 bg-light">
            <h6 class="mb-3">{{ editingId() ? ('::EditContact' | abpLocalization) : ('::NewContact' | abpLocalization) }}</h6>
            <form [formGroup]="form" (ngSubmit)="saveContact()">
              <div class="row g-2">
                <div class="col-md-2">
                  <label class="form-label">{{ '::Salutation' | abpLocalization }}</label>
                  <select class="form-select form-select-sm" formControlName="salutation">
                    <option value="">—</option>
                    <option value="Mr">Mr</option>
                    <option value="Mrs">Mrs</option>
                    <option value="Ms">Ms</option>
                    <option value="Dr">Dr</option>
                    <option value="Dato'">Dato'</option>
                    <option value="Tan Sri">Tan Sri</option>
                  </select>
                </div>
                <div class="col-md-4">
                  <label class="form-label">{{ '::FirstName' | abpLocalization }} *</label>
                  <input class="form-control form-control-sm" formControlName="firstName" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::LastName' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="lastName" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Designation' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="designation" [placeholder]="'::Placeholder:FinanceManager' | abpLocalization" />
                </div>
              </div>
              <div class="row g-2 mt-1">
                <div class="col-md-4">
                  <label class="form-label">{{ '::Email' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="email" formControlName="email" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Phone' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="phone" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Mobile' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="mobileNo" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::Department' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" formControlName="department" />
                </div>
              </div>
              <div class="row g-2 mt-2">
                <div class="col-md-3">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isPrimary_c" formControlName="isPrimaryContact" />
                    <label class="form-check-label" for="isPrimary_c">Primary Contact</label>
                  </div>
                </div>
                <div class="col-md-3">
                  <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="isBilling_c" formControlName="isBillingContact" />
                    <label class="form-check-label" for="isBilling_c">Billing Contact</label>
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
export class ContactManagerComponent implements OnInit {
  @Input() partyType!: string;
  @Input() partyId!: string;

  private contactService = inject(ContactService);
  private fb = inject(FormBuilder);
  private toaster = inject(ToasterService);

  contacts = signal<ContactDto[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editingId = signal<string | null>(null);

  form = this.fb.group({
    salutation: [''],
    firstName: ['', Validators.required],
    lastName: [''],
    designation: [''],
    department: [''],
    email: ['', Validators.email],
    phone: [''],
    mobileNo: [''],
    isPrimaryContact: [false],
    isBillingContact: [false],
  });

  ngOnInit(): void {
    this.loadContacts();
  }

  loadContacts(): void {
    this.loading.set(true);
    this.contactService.getContactsForParty(this.partyType, this.partyId).subscribe({
      next: (data) => { this.contacts.set(data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openForm(): void {
    this.form.reset({ isPrimaryContact: false, isBillingContact: false });
    this.editingId.set(null);
    this.showForm.set(true);
  }

  editContact(c: ContactDto): void {
    this.editingId.set(c.id);
    this.form.patchValue({
      salutation: c.salutation || '',
      firstName: c.firstName,
      lastName: c.lastName || '',
      designation: c.designation || '',
      department: c.department || '',
      email: c.email || '',
      phone: c.phone || '',
      mobileNo: c.mobileNo || '',
      isPrimaryContact: c.isPrimaryContact,
      isBillingContact: c.isBillingContact,
    });
    this.showForm.set(true);
  }

  saveContact(): void {
    if (this.form.invalid) return;
    this.saving.set(true);
    const payload = { ...this.form.value, partyType: this.partyType, partyId: this.partyId };

    const request$ = this.editingId()
      ? this.contactService.update(this.editingId()!, payload as any)
      : this.contactService.create(payload as any);

    request$.subscribe({
      next: () => {
        this.toaster.success(this.editingId() ? '::SuccessfullyUpdated' : '::SuccessfullyCreated');
        this.showForm.set(false);
        this.saving.set(false);
        this.loadContacts();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.saving.set(false);
      },
    });
  }

  private confirmation = inject(ConfirmationService);

  deleteContact(id: string): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.contactService.delete(id).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadContacts(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }
}
