import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { AppointmentService } from '../../proxy/crm/appointment.service';
import type { AppointmentDto } from '../../proxy/crm/models';
import { CompanyContextService } from '../../shared/services/company-context.service';

const STATUS_LABELS = ['Unverified', 'Open', 'Closed'];

@Component({
  selector: 'app-appointment-list',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::Appointments' | abpLocalization">
      <div class="d-flex justify-content-end mb-3">
        <button class="btn btn-primary btn-sm" routerLink="/crm/appointments/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead>
            <tr>
              <th>{{ '::Customer' | abpLocalization }}</th>
              <th>{{ '::ScheduledTime' | abpLocalization }}</th>
              <th>{{ '::AssignedAgent' | abpLocalization }}</th>
              <th>{{ 'Status' | abpLocalization }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.customerName }}</td>
                <td>{{ item.scheduledTime | date:'dd/MM/yyyy HH:mm' }}</td>
                <td class="text-truncate" style="max-width:180px">{{ item.assignedAgentUserId ?? '—' }}</td>
                <td><span class="badge bg-info">{{ statusLabel(item.status) }}</span></td>
                <td>
                  <a class="btn btn-sm btn-outline-primary" [routerLink]="'/crm/appointments/' + item.id"><i class="fa fa-eye"></i></a>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class AppointmentListComponent implements OnInit {
  private service = inject(AppointmentService);
  private companyContext = inject(CompanyContextService);

  items = signal<AppointmentDto[]>([]);

  ngOnInit(): void {
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({ companyId: companyId ?? undefined, skipCount: 0, maxResultCount: 200, sorting: '' } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  statusLabel(status: number | undefined): string { return STATUS_LABELS[status ?? 0] ?? 'Open'; }
}
