import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe, LocalizationService } from '@abp/ng.core';
import { PageModule } from '@abp/ng.components/page';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { Confirmation } from '@abp/ng.theme.shared';
import { AutoRepeatService } from '../../proxy/core/auto-repeat.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { RepeatFrequency } from '../../proxy/core/repeat-frequency.enum';
import type { AutoRepeatDto, CreateAutoRepeatDto } from '../../proxy/core/models';

@Component({
  selector: 'app-auto-repeat-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe, PageModule, PaginationComponent],
  template: `
    <abp-page [title]="'::AutoRepeat' | abpLocalization">
      <div class="card mb-3">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h5 class="mb-0">{{ '::RecurringDocuments' | abpLocalization }}</h5>
          <button class="btn btn-sm btn-primary" (click)="showCreateForm = !showCreateForm">
            <i class="fa fa-plus me-1"></i>{{ '::NewAutoRepeat' | abpLocalization }}
          </button>
        </div>

        @if (showCreateForm) {
          <div class="card-body border-bottom bg-light">
            <div class="row g-3">
              <div class="col-md-3">
                <label class="form-label">{{ '::DocumentType' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newRepeat.referenceDocumentType">
                  <option value="">{{ '::Select' | abpLocalization }}</option>
                  <option value="SalesInvoice">{{ '::SalesInvoice' | abpLocalization }}</option>
                  <option value="PurchaseInvoice">{{ '::PurchaseInvoice' | abpLocalization }}</option>
                  <option value="JournalEntry">{{ '::JournalEntry' | abpLocalization }}</option>
                  <option value="SalesOrder">{{ '::SalesOrder' | abpLocalization }}</option>
                  <option value="PurchaseOrder">{{ '::PurchaseOrder' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::ReferenceDocument' | abpLocalization }}</label>
                <input type="text" class="form-control form-control-sm" [(ngModel)]="newRepeat.referenceDocumentNumber"
                  [placeholder]="'::Placeholder:DocumentNumber' | abpLocalization" />
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::Frequency' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newRepeat.frequency">
                  <option [ngValue]="0">{{ '::Daily' | abpLocalization }}</option>
                  <option [ngValue]="1">{{ '::Weekly' | abpLocalization }}</option>
                  <option [ngValue]="2">{{ '::Monthly' | abpLocalization }}</option>
                  <option [ngValue]="3">{{ '::Quarterly' | abpLocalization }}</option>
                  <option [ngValue]="4">{{ '::HalfYearly' | abpLocalization }}</option>
                  <option [ngValue]="5">{{ '::Yearly' | abpLocalization }}</option>
                </select>
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::StartDate' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newRepeat.startDate" />
              </div>
              <div class="col-md-2">
                <label class="form-label">{{ '::EndDate' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newRepeat.endDate" />
              </div>
            </div>
            <div class="row g-3 mt-1">
              <div class="col-md-3">
                <div class="form-check">
                  <input class="form-check-input" type="checkbox" [(ngModel)]="newRepeat.notifyByEmail" id="notifyEmail" />
                  <label class="form-check-label" for="notifyEmail">{{ '::NotifyByEmail' | abpLocalization }}</label>
                </div>
              </div>
              <div class="col-md-9 text-end">
                <button class="btn btn-sm btn-outline-secondary me-2" (click)="showCreateForm = false">{{ '::Cancel' | abpLocalization }}</button>
                <button class="btn btn-sm btn-success" (click)="create()" [disabled]="isSaving()">
                  <i class="fa fa-check me-1"></i>{{ '::Save' | abpLocalization }}
                </button>
              </div>
            </div>
          </div>
        }

        <div class="card-body p-0">
          @if (repeats().length === 0 && !isLoading()) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-repeat fa-3x mb-3 text-secondary"></i>
              <p>{{ '::NoAutoRepeatsYet' | abpLocalization }}</p>
              <p class="small">{{ '::AutoRepeatHelp' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover mb-0">
                <thead class="table-light">
                  <tr>
                    <th>{{ '::DocumentType' | abpLocalization }}</th>
                    <th>{{ '::ReferenceDocument' | abpLocalization }}</th>
                    <th>{{ '::Frequency' | abpLocalization }}</th>
                    <th>{{ '::NextScheduleDate' | abpLocalization }}</th>
                    <th>{{ '::Generated' | abpLocalization }}</th>
                    <th>{{ '::Status' | abpLocalization }}</th>
                    <th class="text-end">{{ '::Actions' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of repeats(); track item.id) {
                    <tr>
                      <td>
                        <span class="badge bg-info bg-opacity-10 text-info">{{ item.referenceDocumentType }}</span>
                      </td>
                      <td>{{ item.referenceDocumentNumber || '—' }}</td>
                      <td>{{ getFrequencyLabel(item.frequency) }}</td>
                      <td>
                        @if (item.nextScheduleDate) {
                          <span [class.text-danger]="isOverdue(item.nextScheduleDate)">
                            {{ item.nextScheduleDate | date:'dd/MM/yyyy' }}
                          </span>
                          @if (isOverdue(item.nextScheduleDate)) {
                            <span class="badge bg-danger ms-1">{{ '::Overdue' | abpLocalization }}</span>
                          }
                        } @else {
                          <span class="text-muted">—</span>
                        }
                      </td>
                      <td>
                        <span class="badge bg-secondary">{{ item.generatedCount ?? 0 }}</span>
                      </td>
                      <td>
                        @if (item.isEnabled) {
                          <span class="badge bg-success">{{ '::Active' | abpLocalization }}</span>
                        } @else {
                          <span class="badge bg-warning text-dark">{{ '::Disabled' | abpLocalization }}</span>
                        }
                      </td>
                      <td class="text-end">
                        @if (item.isEnabled) {
                          <button class="btn btn-sm btn-outline-warning me-1" (click)="disable(item)"
                            title="Disable">
                            <i class="fa fa-pause"></i>
                          </button>
                        } @else {
                          <button class="btn btn-sm btn-outline-success me-1" (click)="enable(item)"
                            title="Enable">
                            <i class="fa fa-play"></i>
                          </button>
                        }
                        <button class="btn btn-sm btn-outline-danger" (click)="delete(item)"
                          title="Delete">
                          <i class="fa fa-trash"></i>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>

      <app-pagination
        [totalCount]="totalCount()"
        [currentPage]="currentPage"
        [pageSize]="pageSize"
        (pageChange)="onPageChange($event)">
      </app-pagination>
    </abp-page>
  `,
  styles: [`
    .badge { font-weight: 500; }
  `]
})
export class AutoRepeatListComponent implements OnInit {
  private service = inject(AutoRepeatService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);
  private l = inject(LocalizationService);
  private route = inject(ActivatedRoute);

  repeats = signal<AutoRepeatDto[]>([]);
  totalCount = signal(0);
  isLoading = signal(false);
  isSaving = signal(false);
  showCreateForm = false;
  currentPage = 0;
  pageSize = 20;

  newRepeat: Partial<CreateAutoRepeatDto> = {
    referenceDocumentType: '',
    referenceDocumentNumber: '',
    frequency: RepeatFrequency.Monthly,
    startDate: new Date().toISOString().split('T')[0],
    endDate: null,
    notifyByEmail: false,
  };

  ngOnInit(): void {
    this.loadData();
    // Pre-fill from query params (when navigating from document detail "Set Recurring")
    const params = this.route.snapshot.queryParams;
    if (params['docType']) {
      this.newRepeat.referenceDocumentType = params['docType'];
      this.newRepeat.referenceDocumentNumber = params['docNumber'] || '';
      this.showCreateForm = true;
    }
  }

  loadData(): void {
    this.isLoading.set(true);
    const companyId = this.companyContext.currentCompanyId();
    this.service.getList({
      skipCount: this.currentPage * this.pageSize,
      maxResultCount: this.pageSize,
      ...(companyId ? { companyId } : {}),
    }).subscribe({
      next: (res) => {
        this.repeats.set(res.items ?? []);
        this.totalCount.set(res.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.toaster.error('::FailedToLoad');
      }
    });
  }

  create(): void {
    if (!this.newRepeat.referenceDocumentType) {
      this.toaster.warn('::PleaseSelectDocumentType');
      return;
    }
    this.isSaving.set(true);
    const companyId = this.companyContext.currentCompanyId();
    const dto: CreateAutoRepeatDto = {
      ...this.newRepeat as CreateAutoRepeatDto,
      companyId: companyId || undefined,
    };
    this.service.create(dto).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullyCreated');
        this.showCreateForm = false;
        this.resetForm();
        this.loadData();
        this.isSaving.set(false);
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::OperationFailed');
      }
    });
  }

  enable(item: AutoRepeatDto): void {
    this.service.enable(item.id!).subscribe({
      next: () => { this.toaster.success('::Activated'); this.loadData(); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  disable(item: AutoRepeatDto): void {
    this.service.disable(item.id!).subscribe({
      next: () => { this.toaster.success('::Deactivated'); this.loadData(); },
      error: () => this.toaster.error('::OperationFailed'),
    });
  }

  private confirmation = inject(ConfirmationService);

  delete(item: AutoRepeatDto): void {
    this.confirmation.warn('::DeleteConfirmation', '::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(item.id!).subscribe({
        next: () => { this.toaster.success('::SuccessfullyDeleted'); this.loadData(); },
        error: () => this.toaster.error('::OperationFailed'),
      });
    });
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.loadData();
  }

  getFrequencyLabel(freq: string | undefined): string {
    const map: Record<string, string> = {
      '0': 'Daily', '1': 'Weekly', '2': 'Monthly',
      '3': 'Quarterly', '4': 'HalfYearly', '5': 'Yearly',
      'Daily': 'Daily', 'Weekly': 'Weekly', 'Monthly': 'Monthly',
      'Quarterly': 'Quarterly', 'HalfYearly': 'Half-Yearly', 'Yearly': 'Yearly',
    };
    return this.l.instant('::' + (map[freq ?? '2'] || 'Monthly'));
  }

  isOverdue(dateStr: string): boolean {
    const d = new Date(dateStr);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return d < today;
  }

  private resetForm(): void {
    this.newRepeat = {
      referenceDocumentType: '',
      referenceDocumentNumber: '',
      frequency: RepeatFrequency.Monthly,
      startDate: new Date().toISOString().split('T')[0],
      endDate: null,
      notifyByEmail: false,
    };
  }
}
