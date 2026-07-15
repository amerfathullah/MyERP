import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-item-attribute-list',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ItemAttributes' | abpLocalization">
      <!-- Inline Create Form -->
      <div class="card mb-3"><div class="card-body">
        <h6 class="card-title">{{ 'CreateAttribute' | abpLocalization }}</h6>
        <div class="row g-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label">{{ 'Name' | abpLocalization }}</label>
            <input type="text" class="form-control form-control-sm" [(ngModel)]="newName" placeholder="e.g. Color, Size">
          </div>
          <div class="col-md-2">
            <div class="form-check mt-4">
              <input type="checkbox" class="form-check-input" id="isNumeric" [(ngModel)]="isNumeric">
              <label class="form-check-label" for="isNumeric">Numeric</label>
            </div>
          </div>
          @if (isNumeric) {
            <div class="col-md-2">
              <label class="form-label">From</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="fromRange">
            </div>
            <div class="col-md-2">
              <label class="form-label">To</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="toRange">
            </div>
            <div class="col-md-1">
              <label class="form-label">Step</label>
              <input type="number" class="form-control form-control-sm" [(ngModel)]="increment">
            </div>
          }
          <div class="col-md-2">
            <button class="btn btn-primary btn-sm" (click)="createAttribute()" [disabled]="!newName.trim()">
              <i class="fa fa-plus me-1"></i>{{ 'Create' | abpLocalization }}
            </button>
          </div>
        </div>
      </div></div>

      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && attributes.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-palette fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No item attributes configured yet. Create one to enable variant items.</p>
        </div>
      } @else if (!isLoading) {
        @for (attr of attributes; track attr.id) {
          <div class="card mb-2"><div class="card-body py-2">
            <div class="d-flex justify-content-between align-items-center">
              <div>
                <strong>{{ attr.name }}</strong>
                @if (attr.isNumeric) {
                  <span class="badge bg-info ms-2">Numeric: {{ attr.fromRange }}–{{ attr.toRange }} (step {{ attr.increment }})</span>
                } @else {
                  <span class="badge bg-light text-dark ms-2">{{ attr.values?.length ?? 0 }} values</span>
                }
              </div>
              <button class="btn btn-sm btn-outline-danger" (click)="deleteAttribute(attr.id)">
                <i class="fa fa-trash"></i>
              </button>
            </div>
            @if (!attr.isNumeric) {
              <div class="mt-2 d-flex gap-1 flex-wrap align-items-center">
                @for (v of attr.values; track v.value) {
                  <span class="badge bg-secondary">{{ v.value }} ({{ v.abbreviation }})</span>
                }
                <div class="d-inline-flex gap-1 ms-2">
                  <input type="text" class="form-control form-control-sm" style="width:80px" placeholder="Value" #valInput>
                  <input type="text" class="form-control form-control-sm" style="width:50px" placeholder="Abbr" #abbrInput>
                  <button class="btn btn-sm btn-outline-primary" (click)="addValue(attr.id, valInput, abbrInput)">+</button>
                </div>
              </div>
            }
          </div></div>
        }
      }
    </abp-page>
  `
})
export class ItemAttributeListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  attributes: any[] = [];
  isLoading = false;

  newName = '';
  isNumeric = false;
  fromRange = 0;
  toRange = 100;
  increment = 1;

  ngOnInit() { this.loadData(); }

  loadData() {
    this.isLoading = true;
    this.http.get<any[]>('/api/app/item-attribute').subscribe({
      next: res => { this.attributes = res ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  createAttribute() {
    const dto: any = {
      name: this.newName.trim(),
      isNumeric: this.isNumeric,
      values: []
    };
    if (this.isNumeric) {
      dto.fromRange = this.fromRange;
      dto.toRange = this.toRange;
      dto.increment = this.increment;
    }
    this.http.post('/api/app/item-attribute', dto).subscribe({
      next: () => {
        this.toaster.success('Attribute created');
        this.newName = '';
        this.isNumeric = false;
        this.loadData();
      }
    });
  }

  addValue(attrId: string, valInput: HTMLInputElement, abbrInput: HTMLInputElement) {
    const val = valInput.value.trim();
    const abbr = abbrInput.value.trim();
    if (!val || !abbr) return;

    this.http.put(`/api/app/item-attribute/${attrId}/add-value`, { value: val, abbreviation: abbr }).subscribe({
      next: () => {
        valInput.value = '';
        abbrInput.value = '';
        this.loadData();
      }
    });
  }

  deleteAttribute(id: string) {
    if (!confirm('Delete this attribute?')) return;
    this.http.delete(`/api/app/item-attribute/${id}`).subscribe({ next: () => this.loadData() });
  }
}
