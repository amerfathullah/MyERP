import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { MaintenanceScheduleDto } from '../../proxy/assets/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { PaginationComponent, PageEvent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-schedule-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, LocalizationPipe, PaginationComponent, StatusBadgeComponent],
  templateUrl: './maintenance-schedule-list.component.html',
})
export class MaintenanceScheduleListComponent implements OnInit {
  private service = inject(MaintenanceService);
  private itemService = inject(ItemService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  private toaster = inject(ToasterService);

  schedules = signal<MaintenanceScheduleDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  itemNames = signal<Record<string, string>>({});
  customerNames = signal<Record<string, string>>({});
  searchTerm = '';
  currentPage = 0;
  pageSize = 20;

  ngOnInit() {
    this.loadLookups();
    this.loadData();
  }

  private loadLookups() {
    this.itemService.getList({ maxResultCount: 500 } as any).subscribe({
      next: (res) => {
        const map: Record<string, string> = {};
        (res.items ?? []).forEach((i: any) => { map[i.id] = i.itemCode || i.itemName || i.id; });
        this.itemNames.set(map);
      },
      error: () => {}
    });
    this.customerService.getList({ maxResultCount: 200 } as any).subscribe({
      next: (res) => {
        const map: Record<string, string> = {};
        (res.items ?? []).forEach((c: any) => { map[c.id] = c.customerName || c.name || c.id; });
        this.customerNames.set(map);
      },
      error: () => {}
    });
  }

  getItemName(id: string | undefined): string {
    if (!id) return '—';
    return this.itemNames()[id] || '—';
  }

  getCustomerName(id: string | undefined): string {
    if (!id) return '—';
    return this.customerNames()[id] || '—';
  }

  loadData() {
    this.loading.set(true);
    this.service.getScheduleList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'startDate desc',
    }).subscribe({
      next: (res) => {
        this.schedules.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  onSearch() {
    this.currentPage = 0;
    this.loadData();
  }

  submitSchedule(id: string) {
    this.service.submitSchedule(id).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.loadData();
      },
      error: (err: any) => this.toaster.error(err?.error?.error?.message || '::OperationFailed')
    });
  }

  getStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Draft';
      case 1: return 'Submitted';
      case 2: return 'Cancelled';
      default: return 'Draft';
    }
  }

  getPeriodicityLabel(periodicity: string | undefined): string {
    if (!periodicity) return '—';
    return periodicity;
  }
}
