import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { LeaveAllocationService, LeaveAllocationDto, BulkLeaveAllocationDto } from '../../proxy/hr/leave-allocation.service';
import { LeaveService } from '../../proxy/hr/leave.service';
import { LoadingOverlayComponent } from '../../shared/components/loading-overlay/loading-overlay.component';

@Component({
  standalone: true,
  selector: 'app-leave-allocation-list',
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe, LoadingOverlayComponent],
  templateUrl: './leave-allocation-list.component.html',
})
export class LeaveAllocationListComponent implements OnInit {
  private service = inject(LeaveAllocationService);
  private leaveService = inject(LeaveService);

  items = signal<LeaveAllocationDto[]>([]);
  isLoading = signal(false);
  showBulkForm = signal(false);

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
    this.service.getList({ maxResultCount: 100 }).subscribe({
      next: res => {
        this.items.set(res.items ?? []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
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
