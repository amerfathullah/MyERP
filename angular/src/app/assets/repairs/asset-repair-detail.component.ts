import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { DocumentWorkflowComponent, WorkflowAction } from '../../shared/components/document-workflow/document-workflow.component';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';
import { AssetRepairService } from '../../proxy/assets/asset-repair.service';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'app-asset-repair-detail',
  standalone: true,
  imports: [CommonModule, PageModule, LocalizationPipe, DocumentWorkflowComponent, BreadcrumbComponent, ActivityLogComponent, StatusBadgeComponent],
  template: `
    <app-breadcrumb />
    <abp-page [title]="'AssetRepairDetails' | abpLocalization">
      @if (!repair()) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <div class="row mb-3">
          <div class="col-md-8">
            <div class="card">
              <div class="card-header d-flex justify-content-between"><h5 class="mb-0">{{ repair()!.repairDescription ?? 'Repair' }}</h5>
                <app-status-badge [status]="repair()!.status + ''" />
              </div>
              <div class="card-body">
                <div class="row g-3">
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'FailureDate' | abpLocalization }}</small><strong>{{ repair()!.failureDate | date:'dd/MM/yyyy' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'CompletionDate' | abpLocalization }}</small><strong>{{ repair()!.completionDate ? (repair()!.completionDate | date:'dd/MM/yyyy') : '—' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'RepairCost' | abpLocalization }}</small><strong class="fs-5">{{ repair()!.repairCost | number:'1.2-2' }}</strong></div>
                  <div class="col-md-6"><small class="text-muted d-block">{{ 'Capitalize' | abpLocalization }}</small>
                    @if (repair()!.capitalizeRepairCost) {
                      <strong class="text-success">Yes (+{{ repair()!.increaseInAssetLife }} months)</strong>
                    } @else { <strong>No</strong> }
                  </div>
                  @if (repair()!.repairDescription) {
                    <div class="col-12"><small class="text-muted d-block">{{ 'Description' | abpLocalization }}</small><p>{{ repair()!.repairDescription }}</p></div>
                  }
                </div>
              </div>
            </div>
          </div>
          <div class="col-md-4">
            <app-document-workflow [actions]="getActions()" (actionClicked)="onAction($event)" />
          </div>
        </div>
        <app-activity-log documentType="AssetRepair" [documentId]="repair()!.id!" />
      }
    </abp-page>
  `,
})
export class AssetRepairDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private service = inject(AssetRepairService);
  private confirmation = inject(ConfirmationService);
  repair = signal<any>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.service.get(id).subscribe(r => this.repair.set(r));
  }

  getActions(): WorkflowAction[] {
    const s = this.repair()?.status;
    const actions: WorkflowAction[] = [];
    if (s === 0) actions.push({ name: 'complete', label: 'Complete', icon: 'fa-check', color: 'btn-outline-success' });
    if (s === 0 || s === 1) actions.push({ name: 'cancel', label: 'Cancel', icon: 'fa-ban', color: 'btn-outline-danger' });
    return actions;
  }

  onAction(name: string): void {
    const id = this.repair()!.id!;
    const reload = () => this.service.get(id).subscribe(r => this.repair.set(r));
    switch (name) {
      case 'complete': this.service.complete(id).subscribe(reload); break;
      case 'cancel': this.confirmation.warn('CancelConfirmationMessage', 'Confirm').subscribe(s => {
        if (s === Confirmation.Status.confirm) this.service.cancel(id).subscribe(reload);
      }); break;
    }
  }
}
