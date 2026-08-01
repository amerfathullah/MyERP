import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AccountingPeriodService } from '../../proxy/accounting/accounting-period.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  standalone: true,
  selector: 'app-accounting-period-list',
  imports: [CommonModule, FormsModule, LocalizationPipe, StatusBadgeComponent],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">{{ '::AccountingPeriods' | abpLocalization }}</h5>
      </div>
      <div class="card-body">
        @if (periods().length === 0) {
          <div class="text-center text-muted py-5">
            <i class="fas fa-calendar-xmark fa-3x mb-3 d-block"></i>
            <p>{{ '::NoAccountingPeriodsYet' | abpLocalization }}</p>
          </div>
        } @else {
          <div class="table-responsive">
            <table class="table table-hover table-sm">
              <thead>
                <tr>
                  <th>{{ '::Name' | abpLocalization }}</th>
                  <th>{{ '::StartDate' | abpLocalization }}</th>
                  <th>{{ '::EndDate' | abpLocalization }}</th>
                  <th>{{ '::Status' | abpLocalization }}</th>
                  <th>{{ '::ClosedDocumentTypes' | abpLocalization }}</th>
                  <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                </tr>
              </thead>
              <tbody>
                @for (p of periods(); track p.id) {
                  <tr>
                    <td class="fw-medium">{{ p.periodName }}</td>
                    <td>{{ p.startDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ p.endDate | date:'dd/MM/yyyy' }}</td>
                    <td><app-status-badge [status]="p.isClosed ? 'Closed' : 'Open'" /></td>
                    <td>
                      @if (p.closedDocumentTypes) {
                        <span class="badge bg-secondary">{{ p.closedDocumentTypes }}</span>
                      } @else {
                        <span class="text-muted">—</span>
                      }
                    </td>
                    <td class="text-end">
                      @if (!p.isClosed) {
                        <button class="btn btn-outline-danger btn-sm" (click)="closeperiod(p.id)">
                          <i class="fas fa-lock me-1"></i>{{ '::Close' | abpLocalization }}
                        </button>
                      } @else {
                        <span class="badge bg-dark"><i class="fas fa-lock me-1"></i>{{ '::Closed' | abpLocalization }}</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>
  `,
})
export class AccountingPeriodListComponent implements OnInit {
  private periodService = inject(AccountingPeriodService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  periods = signal<any[]>([]);

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    const companyId = this.companyContext.currentCompanyId();
    this.periodService.getList({ skipCount: 0, maxResultCount: 100, sorting: '' } as any).subscribe({
      next: (res) => this.periods.set(res.items ?? []),
      error: () => {},
    });
  }

  closeperiod(id: string) {
    this.periodService.close(id).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyClosed');
        this.loadData();
      },
      error: () => {},
    });
  }
}
