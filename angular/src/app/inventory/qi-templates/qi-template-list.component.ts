import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { QualityInspectionTemplateService } from '../../proxy/inventory/quality-inspection-template.service';

@Component({
  standalone: true,
  selector: 'app-qi-template-list',
  imports: [CommonModule, RouterModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0"><i class="fas fa-clipboard-check me-2"></i>{{ 'QualityInspectionTemplates' | abpLocalization }}</h5>
          <a routerLink="new" class="btn btn-primary btn-sm">
            <i class="fas fa-plus me-1"></i>{{ 'NewQITemplate' | abpLocalization }}
          </a>
        </div>
        <div class="card-body">
          @if (templates().length === 0) {
            <div class="text-center py-4 text-muted">
              <i class="fas fa-clipboard-check fa-2x mb-2"></i>
              <p>{{ 'NoQITemplatesYet' | abpLocalization }}</p>
            </div>
          } @else {
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ 'Name' | abpLocalization }}</th>
                  <th>{{ 'Description' | abpLocalization }}</th>
                  <th>{{ 'Parameters' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (item of templates(); track item.id) {
                  <tr>
                    <td>{{ item.name }}</td>
                    <td>{{ item.description || '—' }}</td>
                    <td><span class="badge bg-info">{{ item.parameterCount }}</span></td>
                    <td>
                      @if (item.isEnabled) {
                        <span class="badge bg-success">{{ 'Active' | abpLocalization }}</span>
                      } @else {
                        <span class="badge bg-secondary">{{ 'Disabled' | abpLocalization }}</span>
                      }
                    </td>
                    <td>
                      <button class="btn btn-outline-secondary btn-sm" (click)="toggle(item.id)">
                        <i class="fas" [class.fa-toggle-on]="item.isEnabled" [class.fa-toggle-off]="!item.isEnabled"></i>
                      </button>
                    </td>
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
export class QiTemplateListComponent implements OnInit {
  private templateService = inject(QualityInspectionTemplateService);
  templates = signal<any[]>([]);

  ngOnInit() { this.loadData(); }

  loadData() {
    this.templateService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' } as any)
      .subscribe({ next: res => this.templates.set(res.items ?? []), error: () => {} });
  }

  toggle(id: string) {
    this.templateService.toggle(id)
      .subscribe({ next: () => this.loadData(), error: () => {} });
  }
}
