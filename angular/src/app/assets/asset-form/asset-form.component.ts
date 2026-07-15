import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { AssetStore } from '../store/asset.store';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { CompanyDto } from '../../proxy/core/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';

@Component({
  selector: 'app-asset-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './asset-form.component.html',
  styleUrls: ['./asset-form.component.scss'],
})
export class AssetFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private store = inject(AssetStore);
  private companyService = inject(CompanyService);
  private companyContext = inject(CompanyContextService);

  form!: FormGroup;
  companies = signal<CompanyDto[]>([]);

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      assetName: ['', [Validators.required, Validators.maxLength(200)]],
      location: [''],
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

    // Auto-fill companyId from context for new documents
    const cid = this.companyContext.currentCompanyId();
    if (cid) this.form.patchValue({ companyId: cid });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.store.create(this.form.getRawValue());
    this.router.navigate(['/assets']);
  }

  cancel(): void {
    this.router.navigate(['/assets']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
