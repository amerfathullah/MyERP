import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { DunningService, type DunningDto } from '../../proxy/sales/sales-advanced.service';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-dunning-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
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
                {{ ['Draft','Submitted','','Resolved','Cancelled'][d.status] }}
              </span>
            </div>
          </div>
          <div class="row mt-2">
            <div class="col-md-4"><strong>{{ 'Outstanding' | abpLocalization }}:</strong> <span class="text-danger fw-bold">{{ d.totalOutstanding | number:'1.2-2' }}</span></div>
            <div class="col-md-4"><strong>Interest:</strong> {{ d.interestAmount | number:'1.2-2' }}</div>
            <div class="col-md-4"><strong>{{ 'GrandTotal' | abpLocalization }}:</strong> <span class="fw-bold">{{ d.grandTotal | number:'1.2-2' }}</span></div>
          </div>
          @if (d.status === 0) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-primary" (click)="action('submit')"><i class="fa fa-paper-plane me-1"></i>Submit</button>
            </div>
          }
          @if (d.status === 1) {
            <div class="mt-3 d-flex gap-2">
              <button class="btn btn-sm btn-success" (click)="action('resolve')"><i class="fa fa-check me-1"></i>Resolve</button>
              <button class="btn btn-sm btn-danger" (click)="action('cancel')"><i class="fa fa-times me-1"></i>{{ 'Cancel' | abpLocalization }}</button>
            </div>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class DunningDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(DunningService);
  d: DunningDto | null = null;

  ngOnInit() { this.load(); }

  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.service.get(id).subscribe((r) => this.d = r);
  }

  action(type: string) {
    const id = this.route.snapshot.paramMap.get('id')!;
    if (type === 'submit') this.service.submit(id).subscribe(() => this.load());
    else if (type === 'resolve') this.service.resolve(id).subscribe(() => this.load());
  }
}
