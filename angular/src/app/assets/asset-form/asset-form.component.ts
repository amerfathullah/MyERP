import { Component, inject, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetStore } from '../store/asset.store';
import { AssetService } from '../../proxy/assets/asset.service';
import { AssetCategoryService } from '../../proxy/assets/asset-category.service';
import { LocationService } from '../../proxy/assets/location.service';
import { CompanyService } from '../../proxy/core/company.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';
import { PurchaseReceiptService } from '../../proxy/purchasing/purchase-receipt.service';
import { PurchaseInvoiceService } from '../../proxy/purchasing/purchase-invoice.service';
import type { CompanyDto } from '../../proxy/core/models';
import type { AssetCategoryDto, LocationDto } from '../../proxy/assets/models';
import type { EmployeeDto } from '../../proxy/human-resources/models';
import { FormsModule } from '@angular/forms';
import { depreciationMethodOptions } from '../../proxy/assets/depreciation-method.enum';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { ItemPickerComponent } from '../../shared/components/item-picker/item-picker.component';

@Component({
  selector: 'app-asset-form',
  standalone: true,
  imports: [AutoValidationDirective, CommonModule, FormsModule, ReactiveFormsModule, PageModule, LocalizationPipe, ItemPickerComponent],
  templateUrl: './asset-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
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
  private purchaseReceiptService = inject(PurchaseReceiptService);
  private purchaseInvoiceService = inject(PurchaseInvoiceService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);

  form!: FormGroup;
  isEdit = signal<boolean>(false);
  assetId = signal<string | null>(null);
  companies = signal<CompanyDto[]>([]);
  categories = signal<AssetCategoryDto[]>([]);
  locations = signal<LocationDto[]>([]);
  employees = signal<EmployeeDto[]>([]);
  depreciationMethods = depreciationMethodOptions;
  purchaseDocType = signal<'PurchaseReceipt' | 'PurchaseInvoice'>('PurchaseReceipt');
  purchaseDocId = signal<string>('');
  purchaseDocs = signal<Array<{ id: string; number: string }>>([]);
  isFetchingPurchaseDoc = signal(false);

  ngOnInit(): void {
    this.form = this.fb.group({
      companyId: ['', Validators.required],
      assetName: ['', [Validators.required, Validators.maxLength(200)]],
      assetCategoryId: [null],
      itemId: [null],
      locationId: [null],
      custodianEmployeeId: [null],
      costCenterId: [null],
      purchaseReceiptId: [null],
      purchaseReceiptItemId: [null],
      purchaseInvoiceId: [null],
      purchaseInvoiceItemId: [null],
      purchaseDate: [new Date().toISOString().split('T')[0], Validators.required],
      purchaseAmount: [0, [Validators.required, Validators.min(0)]],
      additionalCost: [0, [Validators.min(0)]],
      assetQuantity: [1, [Validators.required, Validators.min(1)]],
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

        // Per ERPNext PR #47093 / commit e41720f1a3: allow_on_submit enabled for asset_name field
        // If asset is submitted/depreciating, lock all controls except allow_on_submit fields (assetName, notes)
        if (asset.status !== 0) {
          Object.keys(this.form.controls).forEach((key) => {
            if (key !== 'assetName' && key !== 'notes') {
              this.form.get(key)?.disable();
            }
          });
        }
      });
    } else {
      this.loadPurchaseDocs('PurchaseReceipt');
      const qp = this.route.snapshot.queryParams;
      if (qp?.['purchaseDocType'] && qp?.['purchaseDocId']) {
        this.purchaseDocType.set(qp['purchaseDocType']);
        this.purchaseDocId.set(qp['purchaseDocId']);
        this.fetchFromPurchaseDoc(qp['purchaseDocType'], qp['purchaseDocId'], qp['itemId']);
      }
    }
  }

  onPurchaseDocTypeChange(docType: 'PurchaseReceipt' | 'PurchaseInvoice'): void {
    this.purchaseDocType.set(docType);
    this.purchaseDocId.set('');
    this.loadPurchaseDocs(docType);
  }

  loadPurchaseDocs(docType: 'PurchaseReceipt' | 'PurchaseInvoice'): void {
    const companyId = this.form?.get('companyId')?.value || this.companyContext.currentCompanyId() || undefined;
    if (docType === 'PurchaseReceipt') {
      this.purchaseReceiptService.getList({ companyId, skipCount: 0, maxResultCount: 100 } as any).subscribe({
        next: res => this.purchaseDocs.set((res.items ?? []).filter(d => !!d.id).map(d => ({ id: d.id!, number: d.receiptNumber ?? d.id! }))),
        error: () => this.purchaseDocs.set([]),
      });
    } else {
      this.purchaseInvoiceService.getList({ companyId, skipCount: 0, maxResultCount: 100 } as any).subscribe({
        next: res => this.purchaseDocs.set((res.items ?? []).filter(d => !!d.id).map(d => ({ id: d.id!, number: d.invoiceNumber ?? d.id! }))),
        error: () => this.purchaseDocs.set([]),
      });
    }
  }

  fetchFromSelectedPurchaseDoc(): void {
    const docId = this.purchaseDocId();
    if (!docId) return;
    const itemId = this.form.get('itemId')?.value || undefined;
    this.fetchFromPurchaseDoc(this.purchaseDocType(), docId, itemId);
  }

  fetchFromPurchaseDoc(docType: string, docId: string, itemId?: string): void {
    if (!docId || this.isFetchingPurchaseDoc()) return;
    this.isFetchingPurchaseDoc.set(true);
    this.assetService.getValuesFromPurchaseDoc(docType, docId, itemId, this.assetId() ?? undefined).subscribe({
      next: vals => {
        this.isFetchingPurchaseDoc.set(false);
        this.form.patchValue({
          companyId: vals.companyId ?? this.form.get('companyId')?.value,
          purchaseDate: vals.purchaseDate ? vals.purchaseDate.split('T')[0] : this.form.get('purchaseDate')?.value,
          availableForUseDate: vals.purchaseDate ? vals.purchaseDate.split('T')[0] : this.form.get('availableForUseDate')?.value,
          purchaseAmount: vals.purchaseAmount ?? 0,
          assetQuantity: vals.assetQuantity ?? 1,
          itemId: vals.itemId ?? this.form.get('itemId')?.value,
          assetName: this.form.get('assetName')?.value || vals.itemName || '',
          locationId: vals.locationId ?? this.form.get('locationId')?.value,
          costCenterId: vals.costCenterId ?? this.form.get('costCenterId')?.value,
          purchaseReceiptId: vals.purchaseReceiptId ?? null,
          purchaseReceiptItemId: vals.purchaseReceiptItemId ?? null,
          purchaseInvoiceId: vals.purchaseInvoiceId ?? null,
          purchaseInvoiceItemId: vals.purchaseInvoiceItemId ?? null,
        });
        this.toaster.success(this.l.instant('::SuccessfullyUpdated'));
      },
      error: (err: any) => {
        this.isFetchingPurchaseDoc.set(false);
        this.toaster.error(err?.error?.error?.message || this.l.instant('::OperationFailed'));
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const rawValue = this.form.getRawValue();
    if (this.isEdit() && this.assetId()) {
      this.assetService.update(this.assetId()!, rawValue as any).subscribe({
        next: () => { this.form.markAsPristine(); this.router.navigate(['/assets', this.assetId()]); },
        error: () => { /* handled by global error interceptor */ },
      });
    } else {
      this.assetService.create(rawValue as any).subscribe({
        next: () => { this.form.markAsPristine(); this.router.navigate(['/assets']); },
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
