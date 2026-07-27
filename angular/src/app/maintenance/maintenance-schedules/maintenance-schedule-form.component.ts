import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';
import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-maintenance-schedule-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe, SaveShortcutDirective, AutoValidationDirective],
  templateUrl: './maintenance-schedule-form.component.html',
})
export class MaintenanceScheduleFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(MaintenanceService);
  private itemService = inject(ItemService);
  private customerService = inject(CustomerService);
  private router = inject(Router);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  form!: FormGroup;
  saving = signal(false);
  items = signal<{ id: string; name: string }[]>([]);
  customers = signal<{ id: string; name: string }[]>([]);

  periodicityOptions = [
    { value: 'Weekly', label: '::Weekly' },
    { value: 'Monthly', label: '::Monthly' },
    { value: 'Quarterly', label: '::Quarterly' },
    { value: 'HalfYearly', label: '::HalfYearly' },
    { value: 'Yearly', label: '::Yearly' },
  ];

  ngOnInit() {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      itemId: [''],
      customerId: [''],
      startDate: ['', Validators.required],
      endDate: ['', Validators.required],
      periodicity: ['Monthly', Validators.required],
    });

    const cid = this.companyContext.currentCompanyId();
    if (cid) {
      this.form.patchValue({ companyId: cid });
    }

    this.loadItems();
    this.loadCustomers();
  }

  private loadItems() {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe({
      next: (res) => this.items.set((res.items ?? []).map((i: any) => ({ id: i.id, name: i.itemCode || i.itemName || i.id }))),
      error: () => {}
    });
  }

  private loadCustomers() {
    this.customerService.getList({ maxResultCount: 200 } as any).subscribe({
      next: (res) => this.customers.set((res.items ?? []).map((c: any) => ({ id: c.id, name: c.customerName || c.name || c.id }))),
      error: () => {}
    });
  }

  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    const val = this.form.value;
    const dto = {
      companyId: val.companyId || undefined,
      itemId: val.itemId || undefined,
      customerId: val.customerId || undefined,
      startDate: val.startDate,
      endDate: val.endDate,
      periodicity: val.periodicity,
    };

    this.service.createSchedule(dto).subscribe({
      next: (res) => {
        this.saving.set(false);
        this.toaster.success('::SuccessfullyCreated');
        this.router.navigate(['/maintenance/schedules', res.id]);
      },
      error: (err: any) => {
        this.saving.set(false);
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
      }
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }
}
