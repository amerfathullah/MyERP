import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CustomerService } from '../proxy/sales/customer.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe, ReactiveFormsModule, RouterModule],
  templateUrl: './customer-form.component.html',
  styleUrls: ['./customer-form.component.scss'],
})
export class CustomerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private service = inject(CustomerService);
  private toaster = inject(ToasterService);

  outstandingInvoices = signal<any[]>([]);
  totalOutstanding = signal(0);

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    customerCode: [''],
    tin: [''],
    registrationNumber: [''],
    sstRegistrationNumber: [''],
    idType: ['BRN'],
    idValue: [''],
    contactPerson: [''],
    phone: [''],
    email: ['', Validators.email],
    address: [''],
    city: [''],
    state: [''],
    postalCode: [''],
    country: ['MYS'],
    isActive: [true],
  });

  isEditMode = false;
  entityId: string | null = null;

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe((customer) => {
        this.form.patchValue(customer as any);
      });
      // Load outstanding invoices for this customer
      this.http.get<any>('/api/app/payment-reconciliation/outstanding-invoices', {
        params: { partyType: 'Customer', partyId: this.entityId! }
      }).subscribe(invoices => {
        const items = invoices ?? [];
        this.outstandingInvoices.set(items);
        this.totalOutstanding.set(items.reduce((sum: number, i: any) => sum + (i.outstandingAmount ?? 0), 0));
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue() as any;

    const op = this.isEditMode
      ? this.service.update(this.entityId!, value)
      : this.service.create(value);

    op.subscribe({
      next: () => {
        this.toaster.success(this.isEditMode ? 'Customer updated' : 'Customer created');
        this.router.navigate(['/customers']);
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Save failed'),
    });
  }
}
