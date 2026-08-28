import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { QualityInspectionDetailService } from '../../shared/services/detail-services';
import { QualityInspectionService } from '../../proxy/inventory/quality-inspection.service';
import type { QualityInspectionDto } from '../../proxy/dtos/models';

import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-qi-detail', standalone: true,
  imports: [BreadcrumbComponent, CommonModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'QualityInspections' | abpLocalization">
  <app-breadcrumb />
      @if (d) {
        <div class="card mb-3"><div class="card-body">
          <div class="row">
            <div class="col-md-3"><strong>{{ 'Item' | abpLocalization }}:</strong> {{ d.itemName }}</div>
            <div class="col-md-2"><strong>{{ 'InspectionType' | abpLocalization }}:</strong> {{ ['Incoming','Outgoing','In Process'][d.inspectionType ?? 0] }}</div>
            <div class="col-md-2"><strong>{{ 'InspectionDate' | abpLocalization }}:</strong> {{ d.inspectionDate | date:'dd/MM/yyyy' }}</div>
            <div class="col-md-2">
              <span class="badge" [ngClass]="{'bg-success': d.status===1, 'bg-danger': d.status===2, 'bg-secondary': d.status===0}">{{ ['Draft','Accepted','Rejected'][d.status ?? 0] }}</span>
            </div>
            <div class="col-md-3">
              <span class="badge" [ngClass]="{'bg-secondary': d.docStatus===0, 'bg-success': d.docStatus===1, 'bg-dark': d.docStatus===2}">{{ ['Draft','Submitted','Cancelled'][d.docStatus ?? 0] }}</span>
            </div>
          </div>
        </div></div>
        <div class="card mb-3"><div class="card-body">
          <h6>{{ 'Readings' | abpLocalization }}</h6>
          <table class="table table-sm">
            <thead><tr><th>{{ 'Specification' | abpLocalization }}</th><th>{{ 'Expected' | abpLocalization }}</th><th>{{ 'Reading' | abpLocalization }}</th><th>{{ 'Result' | abpLocalization }}</th></tr></thead>
            <tbody>
              @for (r of d.readings ?? []; track r.id) {
                <tr>
                  <td>{{ r.specification }}</td>
                  <td>{{ r.isNumeric ? (r.minValue + ' – ' + r.maxValue) : r.expectedValue }}</td>
                  <td>{{ r.readingValue }}</td>
                  <td><span class="badge" [ngClass]="{'bg-success': r.status===1, 'bg-danger': r.status===2}">{{ ['—','Pass','Fail'][r.status ?? 0] }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>

        <div class="d-flex gap-2">
          @if (d.docStatus === 0) {
            <button class="btn btn-success btn-sm" [disabled]="acting" (click)="submit()">
              <i class="fa fa-check-double me-1"></i>{{ 'Submit' | abpLocalization }}
            </button>
          }
          @if (d.docStatus === 1) {
            <button class="btn btn-outline-danger btn-sm" [disabled]="acting" (click)="cancel()">
              <i class="fa fa-ban me-1"></i>{{ 'Cancel' | abpLocalization }}
            </button>
          }
        </div>
      }
    </abp-page>
  `,
})
export class QualityInspectionDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private service = inject(QualityInspectionDetailService);
  private qiService = inject(QualityInspectionService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);
  d: QualityInspectionDto | null = null;
  acting = false;
  id = '';

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id')!;
    this.load();
  }

  load() {
    this.service.get(this.id).subscribe({ next: (r) => this.d = r, error: () => {} });
  }

  submit() {
    this.acting = true;
    this.qiService.submit(this.id).subscribe({
      next: () => { this.acting = false; this.toaster.success('::SuccessfullySubmitted'); this.load(); },
      error: (err: any) => { this.acting = false; this.toaster.error(err?.error?.error?.message || '::OperationFailed'); }
    });
  }

  cancel() {
    this.confirmation.warn('::CancelConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.acting = true;
      this.qiService.cancel(this.id).subscribe({
        next: () => { this.acting = false; this.toaster.success('::SuccessfullyCancelled'); this.load(); },
        error: (err: any) => { this.acting = false; this.toaster.error(err?.error?.error?.message || '::OperationFailed'); }
      });
    });
  }
}
