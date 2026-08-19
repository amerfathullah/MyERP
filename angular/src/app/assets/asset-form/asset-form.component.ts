import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { AssetStore } from '../store/asset.store';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';
import { LocationService } from '../../proxy/assets/location.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { AssetCategoryDto, LocationDto } from '../../proxy/assets/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';
import { depreciationMethodOptions } from '../../proxy/assets/depreciation-method.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { ItemPickerComponent } from '../../shared/components/item-picker/item-picker.component';

@Component({
  selector: 'app-asset-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe, ItemPickerComponent],
  templateUrl: './asset-form.component.html',
  styleUrls: ['./asset-form.component.scss'],
})
export class AssetFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private store = inject(AssetStore);
  private assetService = inject(AssetService);
  private assetCategoryService = inject(AssetCategoryService);
  private locationService = inject(LocationService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);
  private employeeService = inject(EmployeeService);

  form!: FormGroup;
  isEdit = signal<boolean>(false);
  assetId = signal<string | null>(null);
  companies = signal<CompanyDto[]>([]);
  categories = signal<AssetCategoryDto[]>([]);
  locations = signal<LocationDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  depreciationMethods = depreciationMethodOptions;

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      assetName: ['', [Validators.required, Validators.maxLength(200)]],
      assetCategoryId: [null],
      itemId: [null],
      locationId: [null],
      custodianEmployeeId: [null],
      purchaseDate: [new Date().toISOString().split('T')[0], Validators.required],
      purchaseAmount: [0, [Validators.required, Validators.min(0)]],
      additionalCost: [0, [Validators.min(0)]],
      calculateDepreciation: [true],
      depreciationMethod: [0],
      usefulLifeMonths: [60, [Validators.min(1)]],
      depreciationRate: [20, [Validators.min(0), Validators.max(100)]],
      frequencyMonths: [12, [Validators.min(1)]],
      availableForUseDate: [null],
      openingAccumulatedDepreciation: [0, [Validators.min(0)]],
      notes: [''],
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));

    this.assetCategoryService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((res) => this.categories.set(res.items ?? []));

    this.locationService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((res) => this.locations.set(res.items ?? []));

    this.employeeService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((res) => this.employees.set(res.items ?? []));

    // Auto-fill companyId from context for new documents
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });

    // Category selection defaults auto-population
    this.form.get('assetCategoryId')?.valueChanges.subscribe((catId) => {
      if (!catId || this.isEdit()) return;
      const cat = this.categories().find((c) => c.id === catId);
      if (cat) {
        if (cat.nonDepreciableCategory) {
          this.form.patchValue({ calculateDepreciation: false });
        } else if (cat.isDepreciable) {
          this.form.patchValue({
            calculateDepreciation: true,
            depreciationMethod: cat.defaultDepreciationMethod ?? 0,
            usefulLifeMonths: cat.defaultUsefulLifeMonths ?? 60,
            depreciationRate: cat.defaultDepreciationRate ?? 20,
            frequencyMonths: cat.defaultFrequencyMonths ?? 12,
          });
        }
      }
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.assetId.set(id);
      this.assetService.get(id).subscribe((asset) => {
        this.form.patchValue({
          companyId: asset.companyId,
          assetName: asset.assetName,
          assetCategoryId: asset.assetCategoryId ?? null,
          itemId: asset.itemId ?? null,
          locationId: asset.locationId ?? null,
          custodianEmployeeId: asset.custodianEmployeeId ?? null,
          purchaseDate: asset.purchaseDate ? asset.purchaseDate.split('T')[0] : '',
          purchaseAmount: asset.purchaseAmount,
          additionalCost: asset.additionalCost,
          calculateDepreciation: asset.calculateDepreciation,
          depreciationMethod: asset.depreciationMethod ?? 0,
          usefulLifeMonths: asset.usefulLifeMonths,
          depreciationRate: asset.depreciationRate,
          frequencyMonths: asset.frequencyMonths ?? 12,
          availableForUseDate: asset.availableForUseDate ? asset.availableForUseDate.split('T')[0] : null,
          openingAccumulatedDepreciation: asset.openingAccumulatedDepreciation ?? 0,
          notes: asset.notes ?? '',
        });
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const rawValue = this.form.getRawValue();
    if (this.isEdit() && this.assetId()) {
      this.assetService.update(this.assetId()!, rawValue as any).subscribe({
        next: () => this.router.navigate(['/assets', this.assetId()]),
        error: () => { /* handled by global error interceptor */ },
      });
    } else {
      this.assetService.create(rawValue as any).subscribe({
        next: () => this.router.navigate(['/assets']),
        error: () => { /* handled by global error interceptor */ },
      });
    }
  }

  cancel(): void {
    if (this.isEdit() && this.assetId()) {
      this.router.navigate(['/assets', this.assetId()]);
    } else {
      this.router.navigate(['/assets']);
    }
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
