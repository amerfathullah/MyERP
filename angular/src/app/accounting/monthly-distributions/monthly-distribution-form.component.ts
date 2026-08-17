import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MonthlyDistributionService } from '../../proxy/accounting/monthly-distribution.service';
import { FiscalYearService } from '../../proxy/accounting/fiscal-year.service';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

@Component({
  selector: 'app-monthly-distribution-form', standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="(isEdit() ? 'Edit' : 'NewMonthlyDistribution') | abpLocalization">
      <div class="card"><div class="card-body">
        <div class="row mb-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'DistributionName' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="form.distributionName" />
          </div>
          <div class="col-md-6">
            <label class="form-label">{{ 'FiscalYear' | abpLocalization }}</label>
            <select class="form-select" [(ngModel)]="form.fiscalYearId">
              <option value="">--</option>
              @for (y of fiscalYears(); track y.id) { <option [value]="y.id">{{ y.name }}</option> }
            </select>
          </div>
        </div>

        <div class="d-flex justify-content-between align-items-center mb-2">
          <h6 class="mb-0">{{ 'MonthlyPercentages' | abpLocalization }}</h6>
          <button class="btn btn-sm btn-outline-secondary" (click)="evenSplit()">{{ 'EvenSplit' | abpLocalization }}</button>
        </div>
        <table class="table table-sm">
          <thead><tr><th>{{ 'Month' | abpLocalization }}</th><th style="width:160px">{{ 'PercentageAllocation' | abpLocalization }}</th></tr></thead>
          <tbody>
            @for (row of form.percentages; track row.month) {
              <tr>
                <td>{{ monthName(row.month) | abpLocalization }}</td>
                <td><input type="number" class="form-control form-control-sm" [(ngModel)]="row.percentageAllocation" /></td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr>
              <td class="text-end fw-bold">{{ 'Total' | abpLocalization }}</td>
              <td class="fw-bold" [class.text-danger]="total() !== 100">{{ total() | number:'1.0-2' }}%</td>
            </tr>
          </tfoot>
        </table>

        <div class="d-flex justify-content-end gap-2">
          <a class="btn btn-secondary" routerLink="/accounting/monthly-distributions">{{ 'Cancel' | abpLocalization }}</a>
          <button class="btn btn-primary" (click)="save()" [disabled]="saving() || total() !== 100"><i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}</button>
        </div>
      </div></div>
    </abp-page>
  `,
})
export class MonthlyDistributionFormComponent implements OnInit {
  private service = inject(MonthlyDistributionService);
  private fiscalYearService = inject(FiscalYearService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toaster = inject(ToasterService);

  saving = signal(false);
  isEdit = signal(false);
  private distributionId: string | null = null;

  fiscalYears = signal<{ id: string; name: string }[]>([]);

  form: { distributionName: string; fiscalYearId: string; percentages: { month: number; percentageAllocation: number }[] } = {
    distributionName: '', fiscalYearId: '',
    percentages: Array.from({ length: 12 }, (_, i) => ({ month: i + 1, percentageAllocation: 100 / 12 })),
  };

  ngOnInit(): void {
    this.fiscalYearService.getList({ maxResultCount: 200 } as any).subscribe(r =>
      this.fiscalYears.set((r.items ?? []).map(y => ({ id: y.id!, name: y.name! }))));

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.distributionId = id;
      this.service.get(id).subscribe(d => {
        this.form = {
          distributionName: d.distributionName!, fiscalYearId: d.fiscalYearId ?? '',
          percentages: (d.percentages ?? []).slice().sort((a, b) => (a.month ?? 0) - (b.month ?? 0))
            .map(p => ({ month: p.month!, percentageAllocation: p.percentageAllocation! })),
        };
      });
    }
  }

  monthName(month: number): string { return MONTH_NAMES[month - 1]; }

  evenSplit(): void {
    this.form.percentages = Array.from({ length: 12 }, (_, i) => ({ month: i + 1, percentageAllocation: 100 / 12 }));
  }

  total(): number {
    return Math.round(this.form.percentages.reduce((s, p) => s + (p.percentageAllocation || 0), 0) * 100) / 100;
  }

  save(): void {
    this.saving.set(true);
    const dto = {
      distributionName: this.form.distributionName,
      fiscalYearId: this.form.fiscalYearId || null,
      percentages: this.form.percentages,
    };
    const req = this.distributionId ? this.service.update(this.distributionId, dto) : this.service.create(dto);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.toaster.success(this.distributionId ? '::SuccessfullyUpdated' : '::SuccessfullySaved');
        this.router.navigate(['/accounting/monthly-distributions']);
      },
      error: () => this.saving.set(false),
    });
  }
}
