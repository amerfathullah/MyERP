import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { AssetStore } from '../store/asset.store';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';
import { LocationService } from '../../proxy/assets/location.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { AssetCategoryDto, LocationDto } from '../../proxy/assets/models';

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
  private router = inject(Router);
  private store = inject(AssetStore);
  private assetService = inject(AssetService);
  private assetCategoryService = inject(AssetCategoryService);
  private locationService = inject(LocationService);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);
  categories = signal<AssetCategoryDto[]>([]);
  locations = signal<LocationDto[]>([]);

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      assetName: ['', [Validators.required, Validators.maxLength(200)]],
      assetCategoryId: [null],
      itemId: [null],
      locationId: [null],
      purchaseDate: [new Date().toISOString().split('T')[0], Validators.required],
      purchaseAmount: [0, [Validators.required, Validators.min(0)]],
      additionalCost: [0],
      calculateDepreciation: [true],
      depreciationMethod: [0],
      usefulLifeMonths: [60],
      depreciationRate: [20],
      notes: [''],
    });

    this.companyService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' })
      .subscribe((res) => this.companies.set(res.items ?? []));

    this.assetCategoryService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((res) => this.categories.set(res.items ?? []));

    this.locationService.getList({ skipCount: 0, maxResultCount: 1000, sorting: '' })
      .subscribe((res) => this.locations.set(res.items ?? []));

    // Auto-fill companyId from context for new documents
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.assetService.create(this.form.getRawValue() as any).subscribe({
      next: () => this.router.navigate(['/assets']),
      error: () => { /* handled by global error interceptor */ },
    });
  }

  cancel(): void {
    this.router.navigate(['/assets']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
