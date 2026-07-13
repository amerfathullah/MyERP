import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { WorkstationService, type WorkstationDto } from '../../proxy/manufacturing/manufacturing-config.service';

@Component({
  selector: 'app-workstation-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  template: `
    <abp-page [title]="'Workstations' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/workstations/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewWorkstation' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) { <app-loading-overlay /> }

      @if (!isLoading && workstations.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-gear fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoWorkstationsYet' | abpLocalization }}</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Name' | abpLocalization }}</th>
              <th>{{ 'Type' | abpLocalization }}</th>
              <th>{{ 'Capacity' | abpLocalization }}</th>
              <th class="text-end">{{ 'HourRate' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (ws of workstations; track ws.id) {
                <tr>
                  <td>{{ ws.name }}</td>
                  <td>{{ ws.workstationType ?? '—' }}</td>
                  <td>{{ ws.productionCapacity }}</td>
                  <td class="text-end">{{ ws.hourRate | number:'1.2-2' }}</td>
                  <td><span class="badge" [class]="ws.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ ws.isActive ? 'Active' : 'Inactive' }}
                  </span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
      }
    </abp-page>
  `,
})
export class WorkstationListComponent implements OnInit {
  private service = inject(WorkstationService);
  workstations: WorkstationDto[] = [];
  isLoading = false;

  ngOnInit(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (r) => { this.workstations = r.items ?? []; this.isLoading = false; },
      error: () => { this.isLoading = false; },
    });
  }
}
