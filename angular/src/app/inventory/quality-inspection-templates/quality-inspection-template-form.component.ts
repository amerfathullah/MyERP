import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityInspectionTemplateService } from '../../proxy/inventory/quality-inspection-template.service';
import { ItemService } from '../../proxy/inventory/item.service';
import type { CreateQiTemplateDto } from '../../proxy/inventory/models';

@Component({
  selector: 'app-quality-inspection-template-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewQITemplate' | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row g-3 mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ '::Name' | abpLocalization }} *</label>
            <input class="form-control" [(ngModel)]="form.name" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'Item' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.itemId">
              <option [ngValue]="undefined">-- {{ 'SelectItem' | abpLocalization }} --</option>
              @for (i of availableItems(); track i.id) {
                <option [value]="i.id">{{ i.itemCode }} — {{ i.itemName }}</option>
              }
            </select>
          </div>
          <div class="col-12">
            <label class="form-label">{{ '::Description' | abpLocalization }}</label>
            <textarea class="form-control" rows="2" [(ngModel)]="form.description"></textarea>
          </div>
        </div>

        <div class="card bg-light mb-3">
          <div class="card-header d-flex justify-content-between align-items-center py-2">
            <span class="fw-semibold">{{ 'AddParameter' | abpLocalization }}</span>
            <button type="button" class="btn btn-outline-primary btn-sm" (click)="addParameter()">
              <i class="fa fa-plus me-1"></i>{{ '::Add' | abpLocalization }}
            </button>
          </div>
          <div class="card-body p-0">
            <table class="table table-sm mb-0">
              <thead>
                <tr>
                  <th>{{ 'Specification' | abpLocalization }} *</th>
                  <th style="width:110px">Numeric</th>
                  <th style="width:120px">Min</th>
                  <th style="width:120px">Max</th>
                  <th>{{ 'AcceptanceCriteria' | abpLocalization }}</th>
                  <th style="width:60px"></th>
                </tr>
              </thead>
              <tbody>
                @for (p of form.parameters; track $index) {
                  <tr>
                    <td><input class="form-control form-control-sm" [(ngModel)]="p.specification" /></td>
                    <td class="text-center"><input type="checkbox" [(ngModel)]="p.isNumeric" /></td>
                    <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.minValue" [disabled]="!p.isNumeric" /></td>
                    <td><input type="number" class="form-control form-control-sm" [(ngModel)]="p.maxValue" [disabled]="!p.isNumeric" /></td>
                    <td><input class="form-control form-control-sm" [(ngModel)]="p.acceptanceCriteria" [disabled]="p.isNumeric" /></td>
                    <td class="text-center">
                      <button type="button" class="btn btn-outline-danger btn-sm" (click)="removeParameter($index)">
                        <i class="fa fa-trash"></i>
                      </button>
                    </td>
                  </tr>
                }
                @if (form.parameters.length === 0) {
                  <tr><td colspan="6" class="text-center text-muted py-3">No parameters yet.</td></tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <div class="d-flex justify-content-end gap-2">
          <a routerLink="/inventory/quality-inspection-templates" class="btn btn-secondary btn-sm">{{ '::Cancel' | abpLocalization }}</a>
          <button type="button" class="btn btn-primary btn-sm" [disabled]="!form.name || saving" (click)="save()">
            <i class="fa fa-save me-1"></i>{{ '::Save' | abpLocalization }}
          </button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class QualityInspectionTemplateFormComponent implements OnInit {
  private service = inject(QualityInspectionTemplateService);
  private itemService = inject(ItemService);
  private router = inject(Router);

  saving = false;
  availableItems = signal<{ id: string; itemCode: string; itemName: string }[]>([]);
  form: {
    name: string;
    description?: string;
    itemId?: string;
    parameters: { specification: string; expectedValue?: string; minValue?: number; maxValue?: number; isNumeric: boolean; formulaBased: boolean; formula?: string; acceptanceCriteria?: string }[];
  } = { name: '', parameters: [] };

  ngOnInit() {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe((r) =>
      this.availableItems.set((r.items ?? []).map((i: any) => ({ id: i.id, itemCode: i.itemCode, itemName: i.itemName })))
    );
  }

  addParameter() {
    this.form.parameters.push({ specification: '', isNumeric: true, formulaBased: false });
  }

  removeParameter(index: number) {
    this.form.parameters.splice(index, 1);
  }

  save() {
    if (!this.form.name) return;
    this.saving = true;
    const input: CreateQiTemplateDto = {
      name: this.form.name,
      description: this.form.description,
      itemId: this.form.itemId || undefined,
      parameters: this.form.parameters.filter((p) => p.specification),
    };
    this.service.create(input).subscribe({
      next: () => this.router.navigate(['/inventory/quality-inspection-templates']),
      error: () => { this.saving = false; },
    });
  }
}
