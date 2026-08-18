import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AssetShiftAllocationService } from '../../proxy/assets/asset-shift-allocation.service';
import { AssetShiftFactorService } from '../../proxy/assets/asset-shift-factor.service';
import { AssetService } from '../../proxy/assets/asset.service';
import type { AssetDto, AssetShiftFactorDto, DepreciationScheduleDto } from '../../proxy/assets/models';

@Component({
  selector: 'app-asset-shift-allocation-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'NewAssetShiftAllocation' | abpLocalization">
      <div class="card mb-3">
        <div class="card-body row g-3">
          <div class="col-md-6">
            <label class="form-label">{{ 'Asset' | abpLocalization }} *</label>
            <select class="form-select" [(ngModel)]="selectedAssetId" (ngModelChange)="onAssetChange()">
              <option [ngValue]="null">—</option>
              @for (a of assets(); track a.id) {
                <option [ngValue]="a.id">{{ a.assetNumber }} — {{ a.assetName }}</option>
              }
            </select>
          </div>
        </div>
      </div>

      @if (isLoadingSchedule()) {
        <div class="text-center py-4"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else if (selectedAssetId && schedule().length === 0) {
        <div class="alert alert-info">{{ 'NoUnbookedPeriods' | abpLocalization }}</div>
      } @else if (schedule().length > 0) {
        <div class="card">
          <div class="card-header">{{ 'DepreciationSchedule' | abpLocalization }}</div>
          <div class="card-body">
            <table class="table table-sm mb-0">
              <thead><tr>
                <th>{{ 'ScheduleDate' | abpLocalization }}</th>
                <th>{{ 'DepreciationAmount' | abpLocalization }}</th>
                <th>{{ 'ShiftFactor' | abpLocalization }}</th>
              </tr></thead>
              <tbody>
                @for (row of schedule(); track row.id) {
                  <tr>
                    <td>{{ row.scheduleDate | date:'dd/MM/yyyy' }}</td>
                    <td>{{ row.depreciationAmount | number:'1.2-2' }}</td>
                    <td style="max-width:220px">
                      <select class="form-select form-select-sm" [(ngModel)]="assignments[row.id!]">
                        <option [ngValue]="null">{{ 'NoChange' | abpLocalization }}</option>
                        @for (f of shiftFactors(); track f.id) {
                          <option [ngValue]="f.id">{{ f.shiftName }} ({{ f.factor }}×)</option>
                        }
                      </select>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
        <div class="d-flex gap-2 mt-3">
          <button class="btn btn-primary" [disabled]="isSaving() || !hasAnyAssignment()" (click)="save()">
            @if (isSaving()) { <i class="fa fa-spinner fa-spin me-1"></i> }
            {{ 'Save' | abpLocalization }}
          </button>
          <a class="btn btn-secondary" routerLink="/assets/shift-allocations">{{ 'Cancel' | abpLocalization }}</a>
        </div>
      }
    </abp-page>
  `,
})
export class AssetShiftAllocationFormComponent implements OnInit {
  private allocationService = inject(AssetShiftAllocationService);
  private shiftFactorService = inject(AssetShiftFactorService);
  private assetService = inject(AssetService);
  private router = inject(Router);
  private toaster = inject(ToasterService);

  assets = signal<AssetDto[]>([]);
  shiftFactors = signal<AssetShiftFactorDto[]>([]);
  schedule = signal<DepreciationScheduleDto[]>([]);
  isLoadingSchedule = signal(false);
  isSaving = signal(false);

  selectedAssetId: string | null = null;
  assignments: Record<string, string | null> = {};

  ngOnInit(): void {
    this.assetService.getList({ maxResultCount: 1000 } as any).subscribe(r => this.assets.set(r.items ?? []));
    this.shiftFactorService.getList({ maxResultCount: 200 } as any).subscribe(r => this.shiftFactors.set(r.items ?? []));
  }

  onAssetChange(): void {
    this.schedule.set([]);
    this.assignments = {};
    if (!this.selectedAssetId) return;

    this.isLoadingSchedule.set(true);
    this.allocationService.getUnbookedSchedule(this.selectedAssetId, undefined).subscribe({
      next: r => { this.schedule.set(r.items ?? []); this.isLoadingSchedule.set(false); },
      error: () => this.isLoadingSchedule.set(false),
    });
  }

  hasAnyAssignment(): boolean {
    return Object.values(this.assignments).some(v => !!v);
  }

  save(): void {
    if (!this.selectedAssetId) return;
    const lines = Object.entries(this.assignments)
      .filter(([, shiftFactorId]) => !!shiftFactorId)
      .map(([scheduleEntryId, shiftFactorId]) => ({ scheduleEntryId, shiftFactorId: shiftFactorId! }));

    if (lines.length === 0) return;

    this.isSaving.set(true);
    this.allocationService.create({ assetId: this.selectedAssetId, lines }).subscribe({
      next: () => {
        this.toaster.success('::SuccessfullySaved');
        this.router.navigate(['/assets/shift-allocations']);
      },
      error: (err: any) => {
        this.toaster.error(err?.error?.error?.message ?? '::SaveFailed');
        this.isSaving.set(false);
      },
    });
  }
}
