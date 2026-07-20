import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveAllocationService } from '../../proxy/human-resources/leave-allocation.service';
import type { LeaveAllocationDto, BulkLeaveAllocationDto } from '../../proxy/human-resources/models';
import { LeaveService } from '../../proxy/human-resources/leave.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  standalone: true,
  selector: 'app-leave-allocation-list',
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent, PaginationComponent],
  templateUrl: './leave-allocation-list.component.html',
})
export class LeaveAllocationListComponent implements OnInit {
  private service = inject(LeaveAllocationService);
  private leaveService = inject(LeaveService);

  items = signal<LeaveAllocationDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  showBulkForm = signal(false);
  pageSize = 10;
  currentPage = 0;

  // Bulk allocation form
  bulkForm: BulkLeaveAllocationDto = {
    companyId: '',
    leaveTypeId: '',
    fromDate: new Date().getFullYear() + '-01-01',
    toDate: new Date().getFullYear() + '-12-31',
    totalLeavesPerEmployee: 12,
  };

  leaveTypes = signal<any[]>([]);

  ngOnInit() {
    this.loadData();
    this.leaveService.getLeaveTypes().subscribe(types => this.leaveTypes.set(types as any[]));
  }

  loadData() {
    this.isLoading.set(true);
    this.service.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: res => {
        this.items.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  toggleBulkForm() {
    this.showBulkForm.set(!this.showBulkForm());
  }

  bulkAllocate() {
    if (!this.bulkForm.companyId || !this.bulkForm.leaveTypeId) return;
    this.service.bulkAllocate(this.bulkForm).subscribe({
      next: count => {
        this.loadData();
        this.showBulkForm.set(false);
      },
    });
  }

  deleteAllocation(id: string) {
    if (!confirm('Delete this allocation?')) return;
    this.service.delete(id).subscribe(() => this.loadData());
  }
}
