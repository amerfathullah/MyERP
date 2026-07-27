import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { MaintenanceScheduleDto } from '../../proxy/assets/models';
import { ItemService } from '../../proxy/inventory/item.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-schedule-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, StatusBadgeComponent, ActivityLogComponent],
  templateUrl: './maintenance-schedule-detail.component.html',
})
export class MaintenanceScheduleDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(MaintenanceService);
  private itemService = inject(ItemService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);

  schedule = signal<MaintenanceScheduleDto | null>(null);
  loading = signal(true);
  itemName = signal('—');
  customerName = signal('—');

  get id(): string {
    return this.route.snapshot.paramMap.get('id') ?? '';
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.service.getSchedule(this.id).subscribe({
      next: (res) => {
        this.schedule.set(res);
        this.loading.set(false);
        this.resolveNames(res);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  private resolveNames(s: MaintenanceScheduleDto) {
    if (s.itemId) {
      this.itemService.get(s.itemId).subscribe({
        next: (item: any) => this.itemName.set(item.itemCode || item.itemName || '—'),
        error: () => {}
      });
    }
    if (s.customerId) {
      this.customerService.get(s.customerId).subscribe({
        next: (cust: any) => this.customerName.set(cust.customerName || cust.name || '—'),
        error: () => {}
      });
    }
  }

  submit() {
    this.service.submitSchedule(this.id).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.load();
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

  getCompletedCount(): number {
    return this.schedule()?.details?.filter(d => d.isCompleted).length ?? 0;
  }

  getTotalVisits(): number {
    return this.schedule()?.details?.length ?? 0;
  }

  getProgressPercent(): number {
    const total = this.getTotalVisits();
    if (total === 0) return 0;
    return Math.round((this.getCompletedCount() / total) * 100);
  }
}
