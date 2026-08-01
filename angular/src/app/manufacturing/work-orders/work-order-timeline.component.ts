import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { FormsModule } from '@angular/forms';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface WoTimelineItem {
  id: string;
  workOrderNumber: string;
  itemName: string;
  status: number;
  quantity: number;
  producedQuantity: number;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  actualStartDate: string | null;
  actualEndDate: string | null;
}

@Component({
  selector: 'app-work-order-timeline',
  standalone: true,
  imports: [CommonModule, RouterLink, LocalizationPipe, FormsModule, PageModule],
  templateUrl: './work-order-timeline.component.html',
  styleUrl: './work-order-timeline.component.scss',
})
export class WorkOrderTimelineComponent implements OnInit {
  private mfgService = inject(ManufacturingService);
  private companyContext = inject(CompanyContextService);

  orders = signal<WoTimelineItem[]>([]);
  isLoading = signal(false);
  viewStartDate = signal<string>(this.getMonthStart());
  viewEndDate = signal<string>(this.getMonthEnd());

  totalDays = computed(() => {
    const start = new Date(this.viewStartDate());
    const end = new Date(this.viewEndDate());
    return Math.max(1, Math.ceil((end.getTime() - start.getTime()) / 86400000));
  });

  dayHeaders = computed(() => {
    const days: { label: string; date: string; isWeekend: boolean; isToday: boolean }[] = [];
    const start = new Date(this.viewStartDate());
    const today = new Date().toISOString().slice(0, 10);
    for (let i = 0; i < this.totalDays(); i++) {
      const d = new Date(start.getTime() + i * 86400000);
      const dateStr = d.toISOString().slice(0, 10);
      days.push({
        label: d.getDate().toString(),
        date: dateStr,
        isWeekend: d.getDay() === 0 || d.getDay() === 6,
        isToday: dateStr === today,
      });
    }
    return days;
  });

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { maxResultCount: 200, skipCount: 0 };
    if (companyId) params.companyId = companyId;
    params.fromDate = this.viewStartDate();
    params.toDate = this.viewEndDate();

    this.mfgService.getWorkOrderList({ maxResultCount: 200, skipCount: 0, companyId, fromDate: this.viewStartDate(), toDate: this.viewEndDate() } as any).subscribe({
      next: (res: any) => {
        const items: WoTimelineItem[] = (res.items ?? [])
          .filter((wo: any) => wo.plannedStartDate || wo.actualStartDate)
          .map((wo: any) => ({
            id: wo.id,
            workOrderNumber: wo.workOrderNumber,
            itemName: wo.itemName || wo.workOrderNumber,
            status: wo.status,
            quantity: wo.quantity,
            producedQuantity: wo.producedQuantity ?? 0,
            plannedStartDate: wo.plannedStartDate?.slice(0, 10) ?? null,
            plannedEndDate: wo.plannedEndDate?.slice(0, 10) ?? null,
            actualStartDate: wo.actualStartDate?.slice(0, 10) ?? null,
            actualEndDate: wo.actualEndDate?.slice(0, 10) ?? null,
          }));
        this.orders.set(items);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  onDateRangeChange(): void {
    this.loadOrders();
  }

  getBarStyle(wo: WoTimelineItem): { left: string; width: string } | null {
    const start = wo.actualStartDate || wo.plannedStartDate;
    const end = wo.actualEndDate || wo.plannedEndDate || start;
    if (!start) return null;

    const viewStart = new Date(this.viewStartDate()).getTime();
    const viewEnd = new Date(this.viewEndDate()).getTime();
    const viewRange = viewEnd - viewStart;
    if (viewRange <= 0) return null;

    const barStart = Math.max(new Date(start).getTime(), viewStart);
    const barEnd = Math.min(new Date(end).getTime() + 86400000, viewEnd);

    const left = ((barStart - viewStart) / viewRange) * 100;
    const width = Math.max(1, ((barEnd - barStart) / viewRange) * 100);

    return { left: `${left}%`, width: `${width}%` };
  }

  getProgressWidth(wo: WoTimelineItem): string {
    if (wo.quantity <= 0) return '0%';
    return `${Math.min(100, (wo.producedQuantity / wo.quantity) * 100)}%`;
  }

  getStatusColor(status: number): string {
    switch (status) {
      case 0: return 'var(--bs-secondary)'; // Draft
      case 1: return 'var(--bs-info)'; // Submitted
      case 2: return 'var(--bs-danger)'; // Not Started
      case 3: return 'var(--bs-warning)'; // In Process
      case 4: return 'var(--bs-success)'; // Completed
      case 5: return 'var(--bs-danger)'; // Stopped
      default: return 'var(--bs-secondary)';
    }
  }

  getStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      0: 'Draft', 1: 'Submitted', 2: 'NotStarted',
      3: 'InProcess', 4: 'Completed', 5: 'Stopped',
    };
    return labels[status] ?? 'Unknown';
  }

  isOverdue(wo: WoTimelineItem): boolean {
    if (wo.status >= 4) return false;
    const end = wo.plannedEndDate;
    if (!end) return false;
    return new Date(end) < new Date();
  }

  shiftView(days: number): void {
    const start = new Date(this.viewStartDate());
    start.setDate(start.getDate() + days);
    const end = new Date(this.viewEndDate());
    end.setDate(end.getDate() + days);
    this.viewStartDate.set(start.toISOString().slice(0, 10));
    this.viewEndDate.set(end.toISOString().slice(0, 10));
    this.loadOrders();
  }

  goToToday(): void {
    this.viewStartDate.set(this.getMonthStart());
    this.viewEndDate.set(this.getMonthEnd());
    this.loadOrders();
  }

  private getMonthStart(): string {
    const d = new Date();
    d.setDate(1);
    return d.toISOString().slice(0, 10);
  }

  private getMonthEnd(): string {
    const d = new Date();
    d.setMonth(d.getMonth() + 1, 0);
    return d.toISOString().slice(0, 10);
  }
}
