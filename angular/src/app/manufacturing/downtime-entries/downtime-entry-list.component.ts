import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';
import { DowntimeEntryService } from '../../proxy/manufacturing/downtime-entry.service';
import type { DowntimeEntryDto } from '../../proxy/manufacturing/models';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { EmployeeService } from '../../proxy/human-resources/employee.service';

@Component({
  selector: 'app-downtime-entry-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  template: `
    <abp-page [title]="'DowntimeEntries' | abpLocalization">
      <div class="d-flex justify-content-end gap-2 mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/manufacturing/downtime-entries/new">
          <i class="fa fa-plus me-1"></i>{{ 'NewDowntimeEntry' | abpLocalization }}
        </button>
      </div>

      @if (isLoading) {
        <app-loading-overlay />
      }

      @if (!isLoading && entries.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-hourglass-half fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">{{ 'NoDowntimeEntriesYet' | abpLocalization }}</p>
          <button class="btn btn-primary" routerLink="/manufacturing/downtime-entries/new">
            <i class="fa fa-plus me-1"></i>{{ 'NewDowntimeEntry' | abpLocalization }}
          </button>
        </div>
      } @else if (!isLoading) {
        <div class="card">
          <div class="card-body">
            <table class="table table-hover mb-0">
              <thead>
                <tr>
                  <th>{{ 'Workstation' | abpLocalization }}</th>
                  <th>{{ 'Operator' | abpLocalization }}</th>
                  <th>{{ 'FromTime' | abpLocalization }}</th>
                  <th>{{ 'ToTime' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Downtime' | abpLocalization }} (min)</th>
                  <th>{{ 'StopReason' | abpLocalization }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                @for (e of entries; track e.id) {
                  <tr>
                    <td>{{ workstationName(e.workstationId) }}</td>
                    <td>{{ operatorName(e.operatorId) }}</td>
                    <td>{{ e.fromTime | date:'short' }}</td>
                    <td>{{ e.toTime | date:'short' }}</td>
                    <td class="text-end fw-bold">{{ e.downtimeMinutes | number:'1.0-1' }}</td>
                    <td>{{ e.stopReason }}</td>
                    <td>
                      <div class="btn-group btn-group-sm">
                        <a class="btn btn-outline-primary" [routerLink]="['/manufacturing/downtime-entries', e.id]">
                          <i class="fa fa-pen"></i>
                        </a>
                        <button class="btn btn-outline-danger" (click)="remove(e)">
                          <i class="fa fa-trash"></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <app-pagination
        [totalCount]="totalCount"
        [pageSize]="pageSize"
        [currentPage]="currentPage"
        (pageChange)="onPageChange($event)" />
    </abp-page>
  `,
})
export class DowntimeEntryListComponent implements OnInit {
  private service = inject(DowntimeEntryService);
  private manufacturingService = inject(ManufacturingService);
  private employeeService = inject(EmployeeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  entries: DowntimeEntryDto[] = [];
  totalCount = 0;
  isLoading = false;
  currentPage = 0;
  pageSize = 20;

  private workstations = new Map<string, string>();
  private operators = new Map<string, string>();

  ngOnInit(): void {
    this.manufacturingService.getWorkstationList({ maxResultCount: 500 } as any).subscribe(r =>
      (r.items ?? []).forEach((w: any) => this.workstations.set(w.id, w.name)));
    this.employeeService.getList({ maxResultCount: 500 } as any).subscribe(r =>
      (r.items ?? []).forEach((e: any) => this.operators.set(e.id, e.fullName ?? `${e.firstName} ${e.lastName ?? ''}`.trim())));
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.entries = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;
        this.isLoading = false;
      },
      error: () => { this.isLoading = false; },
    });
  }

  workstationName(id: string | undefined): string { return (id && this.workstations.get(id)) ?? '—'; }
  operatorName(id: string | undefined): string { return (id && this.operators.get(id)) ?? '—'; }

  remove(e: DowntimeEntryDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(e.id!).subscribe(() => {
        this.toaster.success('::SuccessfullyDeleted');
        this.load();
      });
    });
  }

  onPageChange(event: PageEvent): void { this.currentPage = event.pageIndex; this.load(); }
}
