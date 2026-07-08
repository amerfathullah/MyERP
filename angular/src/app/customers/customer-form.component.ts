import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { CustomerService } from '../proxy/sales/customer.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationModule, ReactiveFormsModule, RouterModule,
    MatCardModule, MatSlideToggleModule,
  ],
  templateUrl: './customer-form.component.html',
  styleUrls: ['./customer-form.component.scss'],
})
export class CustomerFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(CustomerService);
  private toaster = inject(ToasterService);

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
