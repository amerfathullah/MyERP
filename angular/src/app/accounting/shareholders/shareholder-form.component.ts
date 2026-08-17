import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ShareholderService } from '../../proxy/accounting/shareholder.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import type { ShareholderDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-shareholder-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewShareholder') | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Title' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.title" [disabled]="detail?.isCompany" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'FolioNo' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.folioNo" />
          </div>
        </div>
        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/accounting/shareholders">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving()"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>

      @if (isEdit()) {
        <div class="card"><div class="card-body">
          <h6 class="mb-2">{{ 'ShareBalances' | abpLocalization }}</h6>
          @if (!detail?.shareBalances?.length) {
            <p class="text-muted">{{ 'NoSharesHeld' | abpLocalization }}</p>
          } @else {
            <table class="table table-sm">
              <thead><tr>
                <th>{{ 'FromNo' | abpLocalization }}</th><th>{{ 'ToNo' | abpLocalization }}</th>
                <th class="text-end">{{ 'Qty' | abpLocalization }}</th><th class="text-end">{{ 'Rate' | abpLocalization }}</th>
                <th class="text-end">{{ 'Amount' | abpLocalization }}</th><th>{{ 'CurrentState' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (b of detail?.shareBalances; track $index) {
                  <tr>
                    <td>{{ b.fromNo }}</td><td>{{ b.toNo }}</td>
                    <td class="text-end">{{ b.noOfShares }}</td>
                    <td class="text-end">{{ b.rate | number:'1.2-2' }}</td>
                    <td class="text-end">{{ b.amount | number:'1.2-2' }}</td>
                    <td>{{ b.currentState ?? '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div></div>
      }
    </abp-page>
  `,
})
export class ShareholderFormComponent implements OnInit {
  private service = inject(ShareholderService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  saving = signal(false);
  isEdit = signal(false);
  private shareholderId: string | null = null;
  detail: ShareholderDto | null = null;

  form: any = { title: '', folioNo: '' };

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.shareholderId = id;
      this.service.get(id).subscribe(s => {
        this.detail = s;
        this.form = { title: s.title, folioNo: s.folioNo ?? '' };
      });
    }
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      companyId: this.companyContext.currentCompanyId(),
      title: this.form.title,
      folioNo: this.form.folioNo || null,
    };
    const req = this.shareholderId ? this.service.update(this.shareholderId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.toaster.success(this.shareholderId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/accounting/shareholders']);
      },
      error: () => this.saving.set(false),
    });
  }
}
