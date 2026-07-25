import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FinancialReportTemplateService } from '../../proxy/accounting/financial-report-template.service';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

interface FinancialReportTemplateDto {
  id?: string;
  name?: string;
  reportType?: number;
  companyId?: string;
  isStandard?: boolean;
  isEnabled?: boolean;
  description?: string;
  rows?: any[];
}

@Component({
  standalone: true,
  selector: 'app-financial-report-template-list',
  imports: [CommonModule, FormsModule, LocalizationPipe, PaginationComponent],
  template: `
    <div class="container-fluid py-3">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fa fa-file-alt me-2"></i>{{ 'FinancialReportTemplates' | abpLocalization }}</h5>
          <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
            <i class="fa fa-plus me-1"></i>{{ 'NewTemplate' | abpLocalization }}
          </button>
        </div>

        @if (showCreateForm) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-2">
              <div class="col-md-4">
                <label class="form-label">{{ 'Name' | abpLocalization }}</label>
                <input class="form-control form-control-sm" [(ngModel)]="newName" [placeholder]="'Name' | abpLocalization">
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'ReportType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newReportType">
                  <option [value]="0">Profit & Loss</option>
                  <option [value]="1">Balance Sheet</option>
                  <option [value]="2">Cash Flow</option>
                  <option [value]="3">Custom</option>
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ 'Description' | abpLocalization }}</label>
                <input class="form-control form-control-sm" [(ngModel)]="newDescription">
              </div>
              <div class="col-md-2 d-flex align-items-end">
                <button class="btn btn-success btn-sm w-100" (click)="create()" [disabled]="!newName">
                  <i class="fa fa-check me-1"></i>{{ 'Save' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <div class="card-body p-0">
          @if (templates().length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-file-alt fa-3x mb-3 opacity-25"></i>
              <p>{{ 'NoFinancialReportTemplatesYet' | abpLocalization }}</p>
              <button class="btn btn-outline-primary btn-sm" (click)="showCreateForm = true">
                <i class="fa fa-plus me-1"></i>{{ 'NewTemplate' | abpLocalization }}
              </button>
            </div>
          } @else {
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Name' | abpLocalization }}</th>
                  <th>{{ 'ReportType' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Rows' | abpLocalization }}</th>
                  <th class="text-center">{{ 'Status' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (t of templates(); track t.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/accounting/report-templates', t.id]" class="text-decoration-none fw-medium">{{ t.name }}</a>
                      @if (t.isStandard) { <span class="badge bg-info ms-1">Standard</span> }
                    </td>
                    <td>{{ getReportTypeName(t.reportType) }}</td>
                    <td class="text-center">{{ t.rows.length }}</td>
                    <td class="text-center">
                      <span class="badge" [class.bg-success]="t.isEnabled" [class.bg-secondary]="!t.isEnabled">
                        {{ t.isEnabled ? 'Active' : 'Disabled' }}
                      </span>
                    </td>
                    <td class="text-end">
                      <div class="btn-group btn-group-sm">
                        <button class="btn btn-outline-secondary" (click)="toggle(t)" [title]="t.isEnabled ? 'Disable' : 'Enable'">
                          <i class="fa" [class.fa-toggle-on]="t.isEnabled" [class.fa-toggle-off]="!t.isEnabled"></i>
                        </button>
                        @if (!t.isStandard) {
                          <button class="btn btn-outline-danger" (click)="remove(t)">
                            <i class="fa fa-trash"></i>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>

        @if (totalCount() > 10) {
          <div class="card-footer">
            <app-pagination [totalCount]="totalCount()" [pageSize]="10" [currentPage]="currentPage" (pageChange)="onPageChange($event)"></app-pagination>
          </div>
        }
      </div>
    </div>
  `
})
export class FinancialReportTemplateListComponent implements OnInit {
  private templateService = inject(FinancialReportTemplateService);
  private toaster = inject(ToasterService);
  private router = inject(Router);

  templates = signal<FinancialReportTemplateDto[]>([]);
  totalCount = signal(0);
  currentPage = 0;
  showCreateForm = false;

  newName = '';
  newReportType = 0;
  newDescription = '';

  ngOnInit() {
    this.load();
  }

  load() {
    this.templateService.getList({
      skipCount: this.currentPage * 10, maxResultCount: 10, sorting: ''
    }).subscribe(res => {
      this.templates.set(res.items ?? []);
      this.totalCount.set(res.totalCount ?? 0);
    });
  }

  create() {
    if (!this.newName) return;
    this.templateService.create({
      name: this.newName,
      reportType: +this.newReportType,
      description: this.newDescription || undefined,
      rows: []
    }).subscribe({
      next: () => {
        this.toaster.success('Template created');
        this.showCreateForm = false;
        this.newName = '';
        this.newDescription = '';
        this.load();
      },
      error: () => {}
    });
  }

  toggle(t: FinancialReportTemplateDto) {
    this.templateService.toggle(t.id).subscribe({
      next: () => this.load(),
      error: () => {}
    });
  }

  remove(t: FinancialReportTemplateDto) {
    if (!confirm(`Delete template "${t.name}"?`)) return;
    this.templateService.delete(t.id).subscribe({
      next: () => { this.toaster.success('Deleted'); this.load(); },
      error: () => {}
    });
  }

  onPageChange(event: any) {
    this.currentPage = event.pageIndex;
    this.load();
  }

  getReportTypeName(type: number): string {
    switch (type) {
      case 0: return 'Profit & Loss';
      case 1: return 'Balance Sheet';
      case 2: return 'Cash Flow';
      case 3: return 'Custom';
      default: return 'Unknown';
    }
  }
}
