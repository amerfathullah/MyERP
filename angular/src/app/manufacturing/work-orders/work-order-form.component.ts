import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/manufacturing/manufacturing.service';
import type { CreateWorkOrderDto } from '../../proxy/manufacturing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-work-order-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './work-order-form.component.html',
  styleUrls: ['./work-order-form.component.scss'],
})
export class WorkOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(ManufacturingService);
  private toaster = inject(ToasterService);

  form = this.fb.group({
    companyId: ['', Validators.required],
    itemId: ['', Validators.required],
    bomId: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]],
    salesOrderId: [''],
    plannedStartDate: [new Date().toISOString().split('T')[0]],
    plannedEndDate: [''],
    notes: [''],
  });

  ngOnInit(): void {
    // Pre-fill from query params (when navigating from SO "Make Work Order")
    const params = this.route.snapshot.queryParams;
    if (params['salesOrderId']) {
      this.form.patchValue({ salesOrderId: params['salesOrderId'] });
    }
    if (params['companyId']) {
      this.form.patchValue({ companyId: params['companyId'] });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const dto = this.form.getRawValue() as unknown as CreateWorkOrderDto;
    this.service.createWorkOrder(dto).subscribe({
      next: () => {
        this.toaster.success('Work Order created');
        this.router.navigate(['/manufacturing/work-orders']);
      },
      error: (err) => this.toaster.error(err?.error?.error?.message ?? 'Failed to create'),
    });
  }

  cancel(): void {
    this.router.navigate(['/manufacturing/work-orders']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}