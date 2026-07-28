import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DunningService } from '../../proxy/sales/dunning.service';
import type { DunningDto } from '../../proxy/sales/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  selector: 'app-dunning-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe, ActivityLogComponent],
  template: `
    <abp-page [title]="'Dunnings' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'Customer' | abpLocalization }}:</strong> {{ d.customerName }}</div>
            <div class="col-md-2"><strong>{{ 'Level' | abpLocalization }}:</strong> <span class="badge bg-warning">Level {{ d.dunningLevel }}</span></div>
            <div class="col-md-2"><strong>{{ 'Date' | abpLocalization }}:</strong> {{ d.postingDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-2"><strong>{{ 'Fee' | abpLocalization }}:</strong> {{ d.dunningFee | number:'1.2-2' }}</div>
            <div class="col-md-3">
              <span class="badge" [ngClass]="{'bg-secondary':d.status===0,'bg-primary':d.status===1,'bg-success':d.status===3,'bg-danger':d.status===4}">
                {{ getStatusLabel(d.status) }}
              </span>
            </div>
          </div>
          <div class="row mt-2">
            <div class="col-md-4"><strong>{{ 'Outstanding' | abpLocalization }}:</strong> <span class="text-danger fw-bold">{{ d.totalOutstanding | number:'1.2-2' }}</span></div>
            <div class="col-md-4"><strong>{{ 'Interest' | abpLocalization }}:</strong> {{ d.interestAmount | number:'1.2-2' }}</div>
            <div class="col-md-4"><strong>{{ 'GrandTotal' | abpLocalization }}:</strong> <span class="fw-bold">{{ d.grandTotal | number:'1.2-2' }}</span></div>
          </div>
          @if (d.status === 0) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-primary" [disabled]="actionLoading()" (click)="action('submit')"><i class="fa fa-paper-plane me-1"></i>{{ 'Submit' | abpLocalization }}</button>
            </div>
          }
          @if (d.status === 1) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-success" [disabled]="actionLoading()" (click)="action('resolve')"><i class="fa fa-check me-1"></i>{{ 'Resolve' | abpLocalization }}</button>
              <button class="btn btn-sm btn-danger" [disabled]="actionLoading()" (click)="action('cancel')"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            </div>
          }
        </div></div>
        <app-activity-log documentType="Dunning" [documentId]="d.id!" />
      }
    </abp-page>
  `,
})
export class DunningDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(DunningService);
  private toaster = inject(ToasterService);
  private l = inject(LocalizationService);
  d: DunningDto | null = null;
  actionLoading = signal(false);

  ngOnInit() { this.load(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe({
      next: (r) => this.d = r,
      error: () => {}
    });
  }

  getStatusLabel(status: number | undefined): string {
    const keys = ['Draft', 'Submitted', '', 'Resolved', 'Cancelled'];
    return this.l.instant('::' + (keys[status ?? 0] || 'Draft'));
  }

  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.actionLoading.set(true);
    let call$;
    if (type === 'submit') call$ = this.service.submit(id);
    else if (type === 'resolve') call$ = this.service.resolve(id);
    else call$ = this.service.cancel(id);

    call$.subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySubmitted');
        this.actionLoading.set(false);
        this.load();
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message || '::OperationFailed');
        this.actionLoading.set(false);
      }
    });
  }
}
