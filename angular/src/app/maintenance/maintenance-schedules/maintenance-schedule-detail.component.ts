import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MaintenanceService } from '../../proxy/assets/maintenance.service';
import { MaintenanceScheduleDto } from '../../proxy/assets/models';
import { MaintenanceScheduleService } from '../../proxy/maintenance/maintenance-schedule.service';
import { ItemService } from '../../proxy/inventory/item.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-maintenance-schedule-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe, StatusBadgeComponent, ActivityLogComponent],
  templateUrl: './maintenance-schedule-detail.component.html',
})
export class MaintenanceScheduleDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(MaintenanceService);
  private scheduleService = inject(MaintenanceScheduleService);
  private itemService = inject(ItemService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);
  private confirmation = inject(ConfirmationService);

  schedule = signal<MaintenanceScheduleDto | null>(null);
  loading = signal(true);
  actionLoading = signal(false);
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
    this.actionLoading.set(true);
    this.service.submitSchedule(this.id).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.actionLoading.set(false);
        this.load();
      },
      error: (err: any) => { this.actionLoading.set(false); this.toaster.error(err?.error?.error?.message || '::OperationFailed'); }
    });
  }

  generateSchedule() {
    this.confirmation.warn('::RegenerateScheduleWarning', '::AreYouSure').subscribe((res) => {
      if (res !== Confirmation.Status.confirm) return;
      this.actionLoading.set(true);
      this.scheduleService.generateSchedule(this.id).subscribe({
        next: () => {
          this.actionLoading.set(false);
          this.toaster.success('::ScheduleGenerated');
          this.load();
        },
        error: (err: any) => { this.actionLoading.set(false); this.toaster.error(err?.error?.error?.message || '::OperationFailed'); }
      });
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
