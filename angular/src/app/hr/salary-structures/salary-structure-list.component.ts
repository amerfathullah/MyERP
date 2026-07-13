import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { SalaryStructureService, type SalaryStructureDto } from '../../proxy/human-resources/hr-config.service';

@Component({
  selector: 'app-salary-structure-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'SalaryStructures' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/hr/salary-structures/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewSalaryStructure' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <app-loading-overlay /> }

      @if (!isLoading && structures.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-money-bill-wave fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoSalaryStructuresYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'PayrollFrequency' | abpLocalization }}</th>
              <th>{{ 'Components' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr></thead>
            <tbody>
              @for (ss of structures; track ss.id) {
                <tr>
                  <td>{{ ss.name }}</td>
                  <td>{{ ss.payrollFrequency }}</td>
                  <td>{{ (ss.details ?? []).length }}</td>
                  <td><span class="badge" [class]="ss.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ ss.isActive ? 'Active' : 'Inactive' }}
                  </span></td>
                  <td>
                    <a class="btn btn-sm btn-outline-primary" [routerLink]="['/hr/salary-structures', ss.id]">
                      <i class="fa fa-eye"></i>
                    </a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class SalaryStructureListComponent implements OnInit {
  private service = inject(SalaryStructureService);
  structures: SalaryStructureDto[] = [];
  isLoading = false;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (r) => { this.structures = r.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }
}
