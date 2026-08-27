import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DriverService } from '../../proxy/assets/driver.service';
import { DrivingLicenseCategoryService } from '../../proxy/assets/driving-license-category.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { SupplierService } from '../../proxy/purchasing/supplier.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { DrivingLicenseCategoryDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-driver-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'EditDriver' : 'NewDriver') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row g-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'FullName' | abpLocalization }} *</label>
            <input class="form-control" [(ngModel)]="form.fullName" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'CellNumber' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.cellNumber" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'LicenseNumber' | abpLocalization }} *</label>
            <input class="form-control" [(ngModel)]="form.licenseNumber" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'LicenseExpiryDate' | abpLocalization }}</label>
            <input type="date" class="form-control" [(ngModel)]="form.licenseExpiryDate" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'Employee' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.employeeId">
              <option value="">—</option>
              @for (e of employees(); track e.id) { <option [value]="e.id">{{ e.fullName }}</option> }
            </select>
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'Transporter' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.transporterId">
              <option value="">—</option>
              @for (s of suppliers(); track s.id) { <option [value]="s.id">{{ s.supplierName }}</option> }
            </select>
          </div>
          <div class="col-md-12">
            <label class="form-label">{{ 'Address' | abpLocalization }}</label>
            <textarea class="form-control" rows="2" [(ngModel)]="form.address"></textarea>
          </div>
          <div class="col-md-12">
            <label class="form-label d-block">{{ 'LicenseCategories' | abpLocalization }}</label>
            @for (cat of categories(); track cat.id) {
              <div class="form-check form-check-inline">
                <input class="form-check-input" type="checkbox"
                  [checked]="form.licenseCategoryIds.includes(cat.id!)"
                  (change)="toggleCategory(cat.id!)" [id]="'cat-' + cat.id" />
                <label class="form-check-label" [for]="'cat-' + cat.id">{{ cat.categoryName }}</label>
              </div>
            }
          </div>
        </div>
        <hr />
        <div class="d-flex gap-2">
          <button type="button" class="btn btn-primary" [disabled]="!form.fullName || !form.licenseNumber || isSaving()" (click)="save()">
            @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
            {{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-secondary" routerLink="/assets/drivers">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class DriverFormComponent implements OnInit {
  private service = inject(DriverService);
  private categoryService = inject(DrivingLicenseCategoryService);
  private employeeService = inject(EmployeeService);
  private supplierService = inject(SupplierService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  isEdit = signal(false);
  isSaving = signal(false);
  editId = signal<string | null>(null);
  employees = signal<{ id: string; fullName: string }[]>([]);
  suppliers = signal<{ id: string; supplierName: string }[]>([]);
  categories = signal<DrivingLicenseCategoryDto[]>([]);

  form: {
    fullName: string; cellNumber: string; licenseNumber: string; licenseExpiryDate: string;
    employeeId: string; transporterId: string; address: string; licenseCategoryIds: string[];
  } = {
    fullName: '', cellNumber: '', licenseNumber: '', licenseExpiryDate: '',
    employeeId: '', transporterId: '', address: '', licenseCategoryIds: [],
  };

  ngOnInit(): void {
    this.employeeService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.employees.set((r.items ?? []).map((e: any) => ({ id: e.id!, fullName: e.fullName ?? e.firstName ?? '' }))));
    this.supplierService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      this.suppliers.set((r.items ?? []).map((s: any) => ({ id: s.id!, supplierName: s.name ?? '' }))));
    this.categoryService.getList({ maxResultCount: 200 } as any).subscribe(r => this.categories.set(r.items ?? []));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.editId.set(id);
      this.service.get(id).subscribe(d => {
        this.form = {
          fullName: d.fullName ?? '', cellNumber: d.cellNumber ?? '', licenseNumber: d.licenseNumber ?? '',
          licenseExpiryDate: d.licenseExpiryDate ? d.licenseExpiryDate.substring(0, 10) : '',
          employeeId: d.employeeId ?? '', transporterId: d.transporterId ?? '', address: d.address ?? '',
          licenseCategoryIds: [...(d.licenseCategoryIds ?? [])],
        };
      });
    }
  }

  toggleCategory(categoryId: string): void {
    const idx = this.form.licenseCategoryIds.indexOf(categoryId);
    if (idx >= 0) this.form.licenseCategoryIds.splice(idx, 1);
    else this.form.licenseCategoryIds.push(categoryId);
  }

  save(): void {
    if (!this.form.fullName || !this.form.licenseNumber) return;
    this.isSaving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      fullName: this.form.fullName,
      cellNumber: this.form.cellNumber || undefined,
      licenseNumber: this.form.licenseNumber,
      licenseExpiryDate: this.form.licenseExpiryDate || undefined,
      employeeId: this.form.employeeId || undefined,
      transporterId: this.form.transporterId || undefined,
      address: this.form.address || undefined,
      licenseCategoryIds: this.form.licenseCategoryIds,
    };
    const req$ = this.isEdit() ? this.service.update(this.editId()!, dto) : this.service.create(dto);
    req$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/drivers']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
