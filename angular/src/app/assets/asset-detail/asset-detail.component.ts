import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { AssetService } from '../../proxy/assets/asset.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-asset-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="asset()?.assetName ?? 'Asset'">
      @if (!asset()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card">
              <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">{{ asset()!.assetName }}</h5>
                <app-status-badge [status]="asset()!.status + ''" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'AssetName' | abpLocalization }}</small><strong>{{ asset()!.assetName }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'AssetCode' | abpLocalization }}</small><strong>{{ asset()!.assetCode ?? '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'PurchaseDate' | abpLocalization }}</small><strong>{{ asset()!.purchaseDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'GrossAmount' | abpLocalization }}</small><strong>{{ asset()!.grossPurchaseAmount | number:'1.2-2' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'BookValue' | abpLocalization }}</small>
                    <strong class="fs-5" [class.text-danger]="(asset()!.valueAfterDepreciation ?? 0) <= 0">{{ asset()!.valueAfterDepreciation | number:'1.2-2' }}</strong>
                  </div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Location' | abpLocalization }}</small><strong>{{ asset()!.location ?? '—' }}</strong></div>
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
            @if (asset()!.status === 0) {
              <div class="mt-2">
                <a class="btn btn-outline-secondary btn-sm w-100" [routerLink]="['/assets', asset()!.id, 'edit']">
                  <i class="fa fa-edit me-1"></i>{{ 'Edit' | abpLocalization }}
                </a>
              </div>
            }
          </div>
        </div>

        @if ((asset()!.depreciationSchedule ?? []).length > 0) {
          <div class="card mb-3">
            <div class="card-header"><h6 class="mb-0">{{ 'DepreciationSchedule' | abpLocalization }}</h6></div>
            <div class="card-body p-0">
              <table class="table table-sm mb-0">
                <thead><tr>
                  <th>#</th>
                  <th>{{ 'Date' | abpLocalization }}</th>
                  <th class="text-end">{{ 'Amount' | abpLocalization }}</th>
                  <th>{{ 'Status' | abpLocalization }}</th>
                </tr></thead>
                <tbody>
                  @for (entry of asset()!.depreciationSchedule ?? []; track entry.id; let i = $index) {
                    <tr [class.table-success]="entry.isBooked">
                      <td>{{ i + 1 }}</td>
                      <td>{{ entry.scheduledDate | date:'dd/MM/yyyy' }}</td>
                      <td class="text-end">{{ entry.depreciationAmount | number:'1.2-2' }}</td>
                      <td>
                        @if (entry.isBooked) {
                          <span class="badge bg-success"><i class="fa fa-check me-1"></i>{{ 'Booked' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-secondary">{{ 'Pending' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        }

        <app-activity-log documentType="Asset" [documentId]="asset()!.id!" />
      }
    </abp-page>
  `,
})
export class AssetDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AssetService);
  private confirmation = inject(ConfirmationService);

  asset = signal<any>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.service.get(id).subscribe(a => this.asset.set(a));
  }

  getActions(): WorkflowAction[] {
    const s = this.asset()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'submit', label: 'Submit', icon: 'fa-paper-plane', color: 'btn-outline-primary' });
    if (s === 1 || s === 2) actions.push({ name: 'sell', label: 'Sell', icon: 'fa-hand-holding-dollar', color: 'btn-outline-success' });
    if (s === 1 || s === 2) actions.push({ name: 'scrap', label: 'Scrap', icon: 'fa-trash-can', color: 'btn-outline-warning' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.asset()!.id!;
    const reload = () => this.service.get(id).subscribe(a => this.asset.set(a));
    const today = new Date().toISOString().substring(0, 10);
    switch (name) {
      case 'submit': this.service.submit(id).subscribe(reload); break;
      case 'sell': this.service.sell(id, today, 0).subscribe(reload); break;
      case 'scrap': this.service.scrap(id, today).subscribe(reload); break;
    }
  }
}
