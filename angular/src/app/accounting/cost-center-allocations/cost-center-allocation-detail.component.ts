import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { CostCenterAllocationService } from '../../proxy/accounting/cost-center-allocation.service';
import { CostCenterService } from '../../proxy/accounting/cost-center.service';
import type { CostCenterAllocationDto, CostCenterAllocationEntryDto } from '../../proxy/accounting/models';

interface CostCenterLookup {
  id: string;
  name: string;
}

@Component({
  selector: 'app-cost-center-allocation-detail',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  templateUrl: './cost-center-allocation-detail.component.html',
  styleUrls: ['./cost-center-allocation-detail.component.scss'],
})
export class CostCenterAllocationDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private confirmation = inject(ConfirmationService);
  private router = inject(Router);
  private service = inject(CostCenterAllocationService);
  private costCenterService = inject(CostCenterService);
  private toaster = inject(ToasterService);

  allocation = signal<CostCenterAllocationDto | null>(null);
  costCenters = signal<CostCenterLookup[]>([]);
  loading = signal(false);

  totalPercentage = computed(() => {
    const alloc = this.allocation();
    if (!alloc?.entries?.length) return 0;
    return alloc.entries.reduce((sum, e) => sum + (e.percentage ?? 0), 0);
  });

  is100Percent = computed(() => Math.abs(this.totalPercentage() - 100) < 0.01);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loadCostCenters();
    this.service.get(id).subscribe({ next: r => this.allocation.set(r), error: () => {} });
  }

  getCostCenterName(id?: string): string {
    if (!id) return '—';
    return this.costCenters().find(cc => cc.id === id)?.name ?? '—';
  }

  toggleActive() {
    const alloc = this.allocation();
    if (!alloc?.id) return;
    this.loading.set(true);
    this.service.toggleActive(alloc.id).subscribe({
      next: () => {
        this.toaster.success(alloc.isActive ? '::Deactivated' : '::Activated');
        this.reload();
      },
      error: () => this.loading.set(false),
    });
  }

  deleteAllocation() {
    const alloc = this.allocation();
    if (!alloc?.id) return;
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.loading.set(true);
      this.service.delete(alloc.id).subscribe({
        next: () => {
          this.toaster.success('::SuccessfullyDeleted');
          this.router.navigate(['/accounting/cost-center-allocations']);
        },
        error: () => this.loading.set(false),
      });
    });
  }

  private loadCostCenters() {
    this.costCenterService
      .getList({ maxResultCount: 500, skipCount: 0, sorting: '' } as any)
      .subscribe({ next: res => {
        const items = (res.items ?? []).map((cc: any) => ({ id: cc.id, name: cc.name }));
        this.costCenters.set(items);
      }, error: () => {} });
  }

  private reload() {
    this.loading.set(false);
    const alloc = this.allocation();
    if (alloc?.id) {
      this.service.get(alloc.id).subscribe({ next: r => this.allocation.set(r), error: () => {} });
    }
  }
}
