import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  standalone: true,
  selector: 'app-document-series-list',
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid my-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-hashtag me-2"></i>{{ '::DocumentSeries' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showForm = !showForm">
            <i class="fas fa-plus me-1"></i>{{ '::New' | abpLocalization }}
          </button>
        </div>
        <div class="card-body">
          @if (showForm) {
            <div class="border rounded p-3 mb-3 bg-light">
              <div class="row g-2">
                <div class="col-md-3">
                  <label class="form-label">{{ '::DocumentType' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" [(ngModel)]="newItem.documentType" [placeholder]="'::Placeholder:DocumentType' | abpLocalization" />
                </div>
                <div class="col-md-3">
                  <label class="form-label">{{ '::Prefix' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" [(ngModel)]="newItem.prefix" [placeholder]="'::Placeholder:SeriesPrefix' | abpLocalization" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::PadDigits' | abpLocalization }}</label>
                  <input class="form-control form-control-sm" type="number" [(ngModel)]="newItem.paddedDigits" />
                </div>
                <div class="col-md-2">
                  <label class="form-label">{{ '::ResetOnFY' | abpLocalization }}</label>
                  <div><input type="checkbox" [(ngModel)]="newItem.resetOnFiscalYear" /></div>
                </div>
                <div class="col-md-2 d-flex align-items-end">
                  <button class="btn btn-primary btn-sm" (click)="save()"><i class="fas fa-save me-1"></i>{{ '::Save' | abpLocalization }}</button>
                </div>
              </div>
            </div>
          }
          @if (items().length === 0) {
            <div class="text-center text-muted py-4">
              <i class="fas fa-hashtag fa-2x mb-2"></i>
              <p>{{ '::NoDocumentSeriesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead><tr>
                <th>{{ '::DocumentType' | abpLocalization }}</th>
                <th>{{ '::Prefix' | abpLocalization }}</th>
                <th>{{ '::CurrentValue' | abpLocalization }}</th>
                <th>{{ '::PadDigits' | abpLocalization }}</th>
                <th>{{ '::ResetOnFY' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (item of items(); track item.id) {
                  <tr>
                    <td><code>{{ item.documentType }}</code></td>
                    <td><code>{{ item.prefix }}</code></td>
                    <td>{{ item.currentValue }}</td>
                    <td>{{ item.paddedDigits }}</td>
                    <td><i [class]="item.resetOnFiscalYear ? 'fas fa-check text-success' : 'fas fa-times text-muted'"></i></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `
})
export class DocumentSeriesListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  items = signal<any[]>([]);
  showForm = false;
  newItem: any = { documentType: '', prefix: '', paddedDigits: 5, resetOnFiscalYear: true };

  ngOnInit() { this.load(); }

  load() {
    this.http.get<any>('/api/app/document-series').subscribe({ next: res => this.items.set(res.items ?? []), error: () => {} });
  }

  save() {
    if (!this.newItem.documentType || !this.newItem.prefix) return;
    this.http.post('/api/app/document-series', this.newItem).subscribe({
      next: () => { this.toaster.success('::SuccessfullySaved'); this.showForm = false; this.newItem = { documentType: '', prefix: '', paddedDigits: 5, resetOnFiscalYear: true }; this.load(); },
      error: () => {}
    });
  }
}
