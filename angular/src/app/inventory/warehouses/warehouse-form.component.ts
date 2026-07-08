import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationModule } from '@abp/ng.core';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ToasterService } from '@abp/ng.theme.shared';
import { WarehouseService } from '../../proxy/inventory/warehouse.service';
import { CompanyService } from '../../proxy/core/company.service';
import type { CompanyDto } from '../../proxy/core/models';

@Component({
  selector: 'app-warehouse-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, PageModule, LocalizationModule,
    MatCardModule, MatSelectModule, MatSlideToggleModule,
  ],
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

  form = this.fb.group({
    companyId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(256)]],
    warehouseCode: [''],
    address: [''],
    city: [''],
    state: [''],
    postalCode: [''],
    country: ['Malaysia'],
    isGroup: [false],
    isActive: [true],
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
      this.service.update(this.entityId!, dto).subscribe(() => {
        this.toaster.success('Warehouse updated');
        this.router.navigate(['/inventory/warehouses']);
      });
    } else {
      this.service.create(dto).subscribe(() => {
        this.toaster.success('Warehouse created');
        this.router.navigate(['/inventory/warehouses']);
      });
    }
  }

  cancel(): void { this.router.navigate(['/inventory/warehouses']); }
}
