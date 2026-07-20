import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { SalarySlipService } from '../../proxy/human-resources/salary-slip.service';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-salary-slip-detail',
  standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'SalarySlip' | abpLocalization">
      @if (slip(); as s) {
        <div class="card mb-3">
          <div class="card-body">
            <div class="row">
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Employee' | abpLocalization }}</small>
                <span class="fw-bold">{{ s.employeeName ?? '—' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Period' | abpLocalization }}</small>
                <span>{{ s.startDate | date:'dd/MM/yyyy' }} – {{ s.endDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'PostingDate' | abpLocalization }}</small>
                <span>{{ s.postingDate | date:'dd/MM/yyyy' }}</span>
              </div>
              <div class="col-md-3 mb-2">
                <small class="text-muted d-block">{{ 'Status' | abpLocalization }}</small>
                <span class="badge" [ngClass]="s.status === 1 ? 'bg-success' : 'bg-secondary'">
                  {{ s.status === 1 ? 'Submitted' : 'Draft' }}
                </span>
              </div>
            </div>
          </div>
        </div>

        @if (components().length > 0) {
          <div class="row">
            <div class="col-md-6">
              <div class="card">
                <div class="card-header fw-bold text-success"><i class="fa fa-plus-circle me-1"></i>{{ 'Earnings' | abpLocalization }}</div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      @for (c of earnings(); track c.componentName) {
                        <tr><td class="ps-3">{{ c.componentName }}</td><td class="text-end pe-3 font-monospace">{{ c.amount | number:'1.2-2' }}</td></tr>
                      }
                    </tbody>
                    <tfoot><tr class="fw-bold table-light"><td class="ps-3">{{ 'Total' | abpLocalization }}</td><td class="text-end pe-3 font-monospace">{{ s.grossAmount | number:'1.2-2' }}</td></tr></tfoot>
                  </table>
                </div>
              </div>
            </div>
            <div class="col-md-6">
              <div class="card">
                <div class="card-header fw-bold text-danger"><i class="fa fa-minus-circle me-1"></i>{{ 'Deductions' | abpLocalization }}</div>
                <div class="card-body p-0">
                  <table class="table table-sm mb-0">
                    <tbody>
                      @for (c of deductions(); track c.componentName) {
                        <tr><td class="ps-3">{{ c.componentName }}</td><td class="text-end pe-3 font-monospace">{{ c.amount | number:'1.2-2' }}</td></tr>
                      }
                    </tbody>
                    <tfoot><tr class="fw-bold table-light"><td class="ps-3">{{ 'Total' | abpLocalization }}</td><td class="text-end pe-3 font-monospace">{{ s.totalDeductions | number:'1.2-2' }}</td></tr></tfoot>
                  </table>
                </div>
              </div>
            </div>
          </div>

          <div class="card mt-3" style="max-width: 300px; margin-left: auto;">
            <div class="card-body text-end">
              <span class="fs-4 fw-bold text-primary">{{ 'NetPay' | abpLocalization }}: {{ s.netAmount | number:'1.2-2' }}</span>
            </div>
          </div>
        }
      } @else {
    <app-breadcrumb />
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
      }
    </abp-page>
  `,
})
export class SalarySlipDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private salarySlipService = inject(SalarySlipService);

  slip = signal<any>(null);
  components = signal<any[]>([]);

  get earnings() { return () => this.components().filter((c: any) => c.type === 'Earning'); }
  get deductions() { return () => this.components().filter((c: any) => c.type === 'Deduction'); }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.salarySlipService.get(id).subscribe(s => {
      this.slip.set(s);
      if ((s as any).components) this.components.set((s as any).components);
    });
  }
}
