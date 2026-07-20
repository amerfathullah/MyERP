import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ManufacturingService } from '../../proxy/controllers/manufacturing.service';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-workstation-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, PageModule, LocalizationPipe, BreadcrumbComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="ws?.name ?? 'Workstation'">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (ws) {
        <div class="row g-3 mb-4">
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Production Capacity</div>
              <div class="fs-3 fw-bold">{{ ws.productionCapacity }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">Hour Rate</div>
              <div class="fs-4 fw-bold text-primary">{{ ws.hourRate | number:'1.2-2' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Type' | abpLocalization }}</div>
              <div class="fs-5">{{ ws.workstationType ?? 'General' }}</div>
            </div></div>
          </div>
          <div class="col-md-3">
            <div class="card text-center h-100"><div class="card-body">
              <div class="text-muted small">{{ 'Status' | abpLocalization }}</div>
              <span class="badge fs-6" [class]="ws.isActive !== false ? 'bg-success' : 'bg-secondary'">
                {{ ws.isActive !== false ? 'Active' : 'Inactive' }}
              </span>
            </div></div>
          </div>
        </div>

        @if (ws.costs?.length) {
          <div class="card mb-4"><div class="card-header"><h6 class="mb-0">Cost Components</h6></div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead><tr><th>Component</th><th class="text-end">Amount / Hr</th></tr></thead>
                <tbody>
                  @for (c of ws.costs; track $index) {
                    <tr><td>{{ c.name }}</td><td class="text-end">{{ c.amount | number:'1.2-2' }}</td></tr>
                  }
                </tbody>
                <tfoot><tr class="fw-bold"><td>Total Hour Rate</td><td class="text-end">{{ ws.hourRate | number:'1.2-2' }}</td></tr></tfoot>
              </table>
            </div>
          </div>
        }

        @if (ws.workingHours?.length) {
          <div class="card"><div class="card-header"><h6 class="mb-0">Working Hours</h6></div>
            <div class="card-body p-0">
              <table class="table table-hover mb-0">
                <thead><tr><th>Day</th><th>Start</th><th>End</th></tr></thead>
                <tbody>
                  @for (h of ws.workingHours; track $index) {
                    <tr><td>{{ h.dayOfWeek }}</td><td>{{ h.startTime }}</td><td>{{ h.endTime }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }
      }
    </abp-page>
  `
})
export class WorkstationDetailComponent implements OnInit {
  private manufacturingService = inject(ManufacturingService);
  private route = inject(ActivatedRoute);
  ws: any = null;
  isLoading = false;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isLoading = true;
      this.manufacturingService.getWorkstation(id).subscribe({
        next: w => { this.ws = w; this.isLoading = false; },
        error: () => { this.isLoading = false; }
      });
    }
  }
}
