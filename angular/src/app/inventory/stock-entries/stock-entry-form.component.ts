import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { HttpClient } from '@angular/common/http';
import { StockEntryService } from '../../proxy/inventory/stock-entry.service';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-stock-entry-form',
  standalone: true,
  imports: [
    AutoValidationDirective, CommonModule, ReactiveFormsModule, PageModule, LocalizationPipe],
  templateUrl: './stock-entry-form.component.html',
  styleUrls: ['./stock-entry-form.component.scss'],
})
export class StockEntryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(StockEntryService);
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  linkedWorkOrderId: string | null = null;
  isLoadingBOM = false;

  form = this.fb.group({
    companyId: [''],
    entryType: ['Receipt', Validators.required],
    entryDate: [new Date(), Validators.required],
    sourceWarehouse: [''],
    targetWarehouse: ['', Validators.required],
    remarks: [''],
    items: this.fb.array([]),
  });
  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  ngOnInit(): void {
    const cid = this.companyContext.currentCompanyId();
    if (cid && !this.form.get('companyId')?.value) this.form.patchValue({ companyId: cid });

    const params = this.route.snapshot.queryParams;
    if (params['purpose']) {
      const purposeMap: Record<string, string> = {
        'MaterialTransferForManufacture': 'Transfer',
        'Manufacture': 'Manufacture',
        'MaterialIssue': 'Issue',
        'MaterialTransfer': 'Transfer',
      };
      this.form.patchValue({ entryType: purposeMap[params['purpose']] ?? params['purpose'] });
    }
    if (params['workOrderId']) {
      this.linkedWorkOrderId = params['workOrderId'];
      this.form.patchValue({ remarks: `Against Work Order: ${params['workOrderId'].substring(0, 8)}...` });
      this.loadBomItems(params['workOrderId']);
    }
    if (params['sourceWarehouse']) {
      this.form.patchValue({ sourceWarehouse: params['sourceWarehouse'] });
    }
    if (params['targetWarehouse']) {
      this.form.patchValue({ targetWarehouse: params['targetWarehouse'] });
    }
  }

  loadBomItems(workOrderId: string): void {
    this.isLoadingBOM = true;
    const produceQty = 1; // Default to 1 unit — user can adjust
    this.http.get<any>('/api/app/stock-entry/manufacture-items', {
      params: { workOrderId, produceQty: produceQty.toString() },
    }).subscribe({
      next: (result) => {
        this.isLoadingBOM = false;
        if (result.sourceWarehouseId) {
          this.form.patchValue({ sourceWarehouse: result.sourceWarehouseId });
        }
        if (result.fgWarehouseId) {
          this.form.patchValue({ targetWarehouse: result.fgWarehouseId });
        }
        // Clear and populate items from BOM
        this.items.clear();
        for (const item of result.items ?? []) {
          this.items.push(this.fb.group({
            itemId: [item.itemId, Validators.required],
            itemName: [item.itemName, Validators.required],
            qty: [item.requiredQty, [Validators.required, Validators.min(0.01)]],
            uom: ['Unit'],
          }));
        }
        this.toaster.info(`Loaded ${result.items?.length ?? 0} items from BOM`);
      },
      error: () => {
        this.isLoadingBOM = false;
        this.toaster.warn('Could not load BOM items — add manually');
      },
    });
  }

  addItem(): void {
    this.items.push(this.fb.group({
      itemId: ['', Validators.required],
      itemName: ['', Validators.required],
      qty: [1, [Validators.required, Validators.min(0.01)]],
      uom: ['Unit'],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  save(): void {
    if (this.form.invalid) return;
    const dto = this.form.getRawValue() as any;
    this.service.create(dto).subscribe({
      next: () => { this.toaster.success('Stock Entry created'); this.router.navigate(['/inventory/stock-entries']); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Create failed'),
    });
  }

  cancel(): void {
    this.router.navigate(['/inventory/stock-entries']);
  }

  hasUnsavedChanges(): boolean { return this.form.dirty; }
}
