import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ManufacturingSettingsService } from '../../proxy/manufacturing/manufacturing-settings.service';
import { CompanyContextService } from '../../shared/services/company-context.service';

@Component({
  selector: 'app-manufacturing-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ManufacturingSettings' | abpLocalization">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      } @else {
        <form (ngSubmit)="save()">
          <div class="card mb-3"><div class="card-body">
            <h6 class="card-title">{{ 'Production' | abpLocalization }}</h6>
            <div class="row g-3">
              <div class="col-md-4">
                <label class="form-label">Overproduction %</label>
                <input type="number" class="form-control" [(ngModel)]="settings.overproductionPercentage" name="overproduction" step="0.5" min="0">
                <small class="text-muted">WO can produce up to this % extra</small>
              </div>
              <div class="col-md-4">
                <label class="form-label">Extra Materials %</label>
                <input type="number" class="form-control" [(ngModel)]="settings.transferExtraMaterialsPercentage" name="extraMaterials" step="1" min="0">
              </div>
              <div class="col-md-4">
                <label class="form-label">Backflush RM Based On</label>
                <select class="form-select" [(ngModel)]="settings.backflushRawMaterialsBasedOn" name="backflush">
                  <option value="BOM">BOM</option>
                  <option value="Material Transferred for Manufacture">Material Transferred</option>
                </select>
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.materialConsumption" name="matConsumption" id="matC">
                <label class="form-check-label" for="matC">Track Material Consumption</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.makeSerialNoBatchFromWorkOrder" name="serialBatch" id="snBatch">
                <label class="form-check-label" for="snBatch">Auto Serial/Batch from WO</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.updateBomCostsAutomatically" name="autoCost" id="autoCost">
                <label class="form-check-label" for="autoCost">Auto-Update BOM Costs</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.addCorrectiveOpCostInFGValuation" name="corrective" id="corr">
                <label class="form-check-label" for="corr">Include Corrective Ops Cost</label></div>
              </div>
            </div>
          </div></div>

          <div class="card mb-3"><div class="card-body">
            <h6 class="card-title">Scheduling & Capacity</h6>
            <div class="row g-3">
              <div class="col-md-3">
                <label class="form-label">Mins Between Ops</label>
                <input type="number" class="form-control" [(ngModel)]="settings.minsBetweenOperations" name="minsBetween" min="0">
              </div>
              <div class="col-md-3">
                <label class="form-label">Capacity Planning Days</label>
                <input type="number" class="form-control" [(ngModel)]="settings.capacityPlanningForDays" name="capDays" min="1">
              </div>
              <div class="col-md-3">
                <div class="form-check mt-4"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.allowOvertime" name="overtime" id="ot">
                <label class="form-check-label" for="ot">Allow Overtime</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check mt-4"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.allowProductionOnHolidays" name="holidays" id="hol">
                <label class="form-check-label" for="hol">Allow Holiday Production</label></div>
              </div>
            </div>
            <div class="row g-3 mt-2">
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.disableCapacityPlanning" name="disableCap" id="disCap">
                <label class="form-check-label" for="disCap">Disable Capacity Planning</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.jobCardExcessTransfer" name="jcExcess" id="jcEx">
                <label class="form-check-label" for="jcEx">Job Card Excess Transfer</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.enforceTimeLogs" name="timeLogs" id="tl">
                <label class="form-check-label" for="tl">Enforce Time Logs</label></div>
              </div>
              <div class="col-md-3">
                <div class="form-check"><input type="checkbox" class="form-check-input" [(ngModel)]="settings.validateComponentsQuantitiesPerBom" name="validateComp" id="vc">
                <label class="form-check-label" for="vc">Validate Component Qty per BOM</label></div>
              </div>
            </div>
          </div></div>

          <button type="submit" class="btn btn-primary" [disabled]="saving">
            <i class="fa fa-save me-1"></i>{{ 'Save' | abpLocalization }}
          </button>
        </form>
      }
    </abp-page>
  `
})
export class ManufacturingSettingsComponent implements OnInit {
  private settingsService = inject(ManufacturingSettingsService);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  isLoading = false;
  saving = false;
  settings: any = this.getDefaults();

  ngOnInit() { this.loadSettings(); }

  loadSettings() {
    const companyId = this.companyContext.currentCompanyId();
    if (!companyId) return;
    this.isLoading = true;
    this.settingsService.getForCompany(companyId).subscribe({
      next: res => { if (res) this.settings = { ...res }; else this.settings = { ...this.getDefaults(), companyId }; this.isLoading = false; },
      error: () => { this.settings = { ...this.getDefaults(), companyId }; this.isLoading = false; }
    });
  }

  save() {
    this.saving = true;
    const companyId = this.companyContext.currentCompanyId();
    this.settings.companyId = companyId;
    this.settingsService.save(this.settings).subscribe({
      next: () => { this.toaster.success('Manufacturing settings saved'); this.saving = false; },
      error: () => { this.saving = false; }
    });
  }

  private getDefaults() {
    return {
      overproductionPercentage: 5, backflushRawMaterialsBasedOn: 'BOM',
      materialConsumption: false, transferExtraMaterialsPercentage: 0,
      minsBetweenOperations: 10, capacityPlanningForDays: 30,
      makeSerialNoBatchFromWorkOrder: false, updateBomCostsAutomatically: false,
      allowOvertime: false, allowProductionOnHolidays: false,
      disableCapacityPlanning: false, jobCardExcessTransfer: false,
      enforceTimeLogs: false, addCorrectiveOpCostInFGValuation: false,
      validateComponentsQuantitiesPerBom: true
    };
  }
}
