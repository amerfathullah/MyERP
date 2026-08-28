import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';
import { WarehouseType, warehouseTypeOptions } from '../../proxy/inventory/warehouse-type.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-warehouse-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './warehouse-form.component.html',
  styleUrls: ['./warehouse-form.component.scss'],
})
export class WarehouseFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(WarehouseService);
  private companyService = inject(CompanyService);
  private toaster = inject(ToasterService);

  companies = signal<CompanyDto[]>([]);
  isEditMode = false;
  entityId: string | null = null;
  warehouseTypeOptions = warehouseTypeOptions;

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(256)]],
    warehouseCode: ['', Validators.maxLength(20)],
    address: ['', Validators.maxLength(500)],
    city: ['', Validators.maxLength(100)],
    state: ['', Validators.maxLength(100)],
    postalCode: ['', Validators.maxLength(10)],
    country: ['Malaysia', Validators.maxLength(100)],
    isGroup: [false],
    isActive: [true],
    warehouseType: [WarehouseType.Standard],
  });

  ngOnInit(): void {
    this.entityId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.entityId;

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe(r => this.companies.set(r.items ?? []));

    if (this.isEditMode) {
      this.service.get(this.entityId!).subscribe(w => this.form.patchValue(w as any));
    }
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const dto = this.form.getRawValue() as any;
    if (this.isEditMode) {
      this.service.update(this.entityId!, dto).subscribe({
        next: () => { this.form.markAsPristine(); this.toaster.success('::SuccessfullySaved'); this.router.navigate(['/inventory/warehouses']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    } else {
      this.service.create(dto).subscribe({
        next: () => { this.form.markAsPristine(); this.toaster.success('::SuccessfullySaved'); this.router.navigate(['/inventory/warehouses']); },
        error: (err: any) => this.toaster.error(err?.error?.error?.message || '::SaveFailed'),
      });
    }
  }

  cancel(): void { this.router.navigate(['/inventory/warehouses']); }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}