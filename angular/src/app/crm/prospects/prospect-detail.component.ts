import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ProspectService } from '../../proxy/crm/prospect.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import type { ProspectDto } from '../../proxy/crm/models';
import type { CustomerDto } from '../../proxy/sales/models';
import { ToasterService } from '@abp/ng.theme.shared';
import { BreadcrumbComponent } from '../../shared/components/breadcrumb/breadcrumb.component';
import { ActivityLogComponent } from '../../shared/components/activity-log/activity-log.component';

@Component({
  standalone: true,
  selector: 'app-prospect-detail',
  imports: [CommonModule, FormsModule, LocalizationPipe, BreadcrumbComponent, ActivityLogComponent],
  template: `
    <app-breadcrumb />
    @if (loading()) {
      <div class="d-flex justify-content-center p-5"><div class="spinner-border text-primary"></div></div>
    } @else if (prospect(); as p) {
      <div class="row mb-3">
        <div class="col"><h4 class="mb-0">{{ p.prospectName }}</h4></div>
        @if (p.isConverted) {
          <div class="col-auto">
            <span class="badge bg-success">{{ '::Converted' | abpLocalization }}</span>
          </div>
        } @else {
          <div class="col-auto d-flex gap-2">
            <select class="form-select form-select-sm" style="width: 250px;" [(ngModel)]="selectedCustomerId">
              <option value="">— {{ '::SelectCustomer' | abpLocalization }} —</option>
              @for (c of customers(); track c.id) {
                <option [value]="c.id">{{ c.name }}</option>
              }
            </select>
            <button class="btn btn-success btn-sm" [disabled]="!selectedCustomerId" (click)="convertToCustomer()">
              <i class="fa fa-link me-1"></i>{{ '::CRM:ConvertToCustomer' | abpLocalization }}
            </button>
          </div>
        }
      </div>

      <div class="row mb-4 g-3">
        <div class="col-md-4">
          <div class="card">
            <div class="card-header"><h6 class="mb-0">{{ '::ProspectDetails' | abpLocalization }}</h6></div>
            <div class="card-body">
              <dl class="row mb-0">
                <dt class="col-sm-5">{{ '::Industry' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ p.industry || '—' }}</dd>
                <dt class="col-sm-5">{{ '::Territory' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ p.territory || '—' }}</dd>
                <dt class="col-sm-5">{{ '::Notes' | abpLocalization }}</dt>
                <dd class="col-sm-7">{{ p.notes || '—' }}</dd>
              </dl>
            </div>
          </div>
        </div>
        <div class="col-md-8">
          <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
              <h6 class="mb-0">{{ '::LinkedLeads' | abpLocalization }}</h6>
              <span class="badge bg-primary">{{ p.leadCount || 0 }}</span>
            </div>
            <div class="card-body">
              <p class="text-muted mb-0">
                {{ '::LinkedOpportunities' | abpLocalization }}: {{ p.opportunityCount || 0 }}
              </p>
            </div>
          </div>
        </div>
      </div>

      <app-activity-log documentType="Prospect" [documentId]="p.id!" />
    }
  `,
})
export class ProspectDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private prospectService = inject(ProspectService);
  private customerService = inject(CustomerService);
  private toaster = inject(ToasterService);

  prospect = signal<ProspectDto | null>(null);
  customers = signal<CustomerDto[]>([]);
  loading = signal(true);
  selectedCustomerId = '';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/crm/prospects']); return; }
    this.prospectService.get(id).subscribe({
      next: (p: any) => { this.prospect.set(p); this.loading.set(false); },
      error: () => { this.router.navigate(['/crm/prospects']); },
    });
    this.customerService.getList({ skipCount: 0, maxResultCount: 500, sorting: '' } as any).subscribe(r => {
      this.customers.set(r.items ?? []);
    });
  }

  convertToCustomer(): void {
    if (!this.selectedCustomerId) return;
    const id = this.prospect()!.id!;
    this.prospectService.convertToCustomer(id, this.selectedCustomerId).subscribe({
      next: (updated: any) => { this.prospect.set(updated); this.toaster.success('::SuccessfullyConverted'); },
      error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Conversion failed'),
    });
  }
}
