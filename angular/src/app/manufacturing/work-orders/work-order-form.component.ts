import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CreateWorkOrderDto } from '../../proxy/manufacturing/models';

import { AutoValidationDirective } from '../../shared/directives/auto-validation.directive';
import { SaveShortcutDirective } from '../../shared/directives/save-shortcut.directive';

@Component({
  selector: 'app-work-order-form',
  standalone: true,
  imports: [AutoValidationDirective, SaveShortcutDirective, CommonModule, ReactiveFormsModule, LocalizationPipe, PageModule],
  templateUrl: './work-order-form.component.html',
  styleUrls: ['./work-order-form.component.scss'],
})
export class WorkOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(ManufacturingService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private itemService = inject(ItemService);

  items = signal<any[]>([]);
  boms = signal<any[]>([]);
  bomMaterials = signal<any[]>([]);

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
    // Fallback: auto-fill from company context if not set from query params
    if (!this.form.get('companyId')?.value) {
      const cid = this.companyContext.currentCompanyId();
      if (cid) this.form.patchValue({ companyId: cid });
    }

    // Load items for dropdown
    this.itemService.getList({ skipCount: 0, maxResultCount: 500, sorting: 'itemCode asc' } as any)
      .subscribe(res => this.items.set(res.items ?? []));

    // Load BOMs (all — filtered client-side when item changes)
    this.service.getBomList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any)
      .subscribe(res => this.boms.set(res.items ?? []));
  }

  get filteredBoms(): any[] {
    const selectedItemId = this.form.get('itemId')?.value;
    if (!selectedItemId) return this.boms();
    return this.boms().filter((b: any) => b.itemId === selectedItemId);
  }

  onItemChanged(): void {
    // Reset BOM and materials when item changes
    this.form.patchValue({ bomId: '' });
    this.bomMaterials.set([]);
  }

  onBomChanged(): void {
    const bomId = this.form.get('bomId')?.value;
    if (!bomId) {
      this.bomMaterials.set([]);
      return;
    }
    // Load the selected BOM's items from the boms list (already fetched with items via AutoInclude)
    const bom = this.boms().find((b: any) => b.id === bomId);
    if (bom?.items?.length) {
      this.bomMaterials.set(bom.items);
    } else {
      // Fallback: fetch BOM detail for items
      this.service.getBom(bomId).subscribe({
        next: (detail: any) => this.bomMaterials.set(detail.items ?? []),
        error: () => this.bomMaterials.set([]),
      });
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
