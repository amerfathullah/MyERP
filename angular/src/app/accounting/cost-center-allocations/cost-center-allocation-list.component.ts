import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { HttpClient } from '@angular/common/http';
import { ToasterService } from '@abp/ng.theme.shared';
import { CompanyContextService } from '../../shared/services/company-context.service';

interface AllocationEntry {
  id?: string;
  childCostCenterId: string;
  percentage: number;
}

interface Allocation {
  id: string;
  companyId: string;
  mainCostCenterId: string;
  validFrom: string;
  isActive: boolean;
  entries: AllocationEntry[];
}

interface CostCenter {
  id: string;
  name: string;
  isGroup: boolean;
}

@Component({
  selector: 'app-cost-center-allocation-list',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid">
      <div class="row mb-3">
        <div class="col">
          <h4>{{ '::CostCenterAllocations' | abpLocalization }}</h4>
        </div>
        <div class="col-auto">
          <button class="btn btn-primary btn-sm" (click)="showCreateForm = !showCreateForm">
            <i class="fa fa-plus me-1"></i>{{ '::NewAllocation' | abpLocalization }}
          </button>
        </div>
      </div>

      @if (showCreateForm) {
        <div class="card mb-4">
          <div class="card-body">
            <h6>{{ '::CreateAllocation' | abpLocalization }}</h6>
            <div class="row g-3 mb-3">
              <div class="col-md-4">
                <label class="form-label">{{ '::MainCostCenter' | abpLocalization }}</label>
                <select class="form-select form-select-sm" [(ngModel)]="newAllocation.mainCostCenterId">
                  <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                  @for (cc of costCenters(); track cc.id) {
                    @if (!cc.isGroup) {
                      <option [value]="cc.id">{{ cc.name }}</option>
                    }
                  }
                </select>
              </div>
              <div class="col-md-3">
                <label class="form-label">{{ '::ValidFrom' | abpLocalization }}</label>
                <input type="date" class="form-control form-control-sm" [(ngModel)]="newAllocation.validFrom">
              </div>
            </div>

            <h6 class="mt-3">{{ '::AllocationEntries' | abpLocalization }}</h6>
            <table class="table table-sm table-bordered">
              <thead>
                <tr>
                  <th>{{ '::ChildCostCenter' | abpLocalization }}</th>
                  <th style="width: 120px">{{ '::Percentage' | abpLocalization }}</th>
                  <th style="width: 60px"></th>
                </tr>
              </thead>
              <tbody>
                @for (entry of newAllocation.entries; track $index) {
                  <tr>
                    <td>
                      <select class="form-select form-select-sm" [(ngModel)]="entry.childCostCenterId">
                        <option value="">-- {{ '::Select' | abpLocalization }} --</option>
                        @for (cc of costCenters(); track cc.id) {
                          @if (!cc.isGroup && cc.id !== newAllocation.mainCostCenterId) {
                            <option [value]="cc.id">{{ cc.name }}</option>
                          }
                        }
                      </select>
                    </td>
                    <td><input type="number" class="form-control form-control-sm" [(ngModel)]="entry.percentage" min="0.01" max="100" step="0.01"></td>
                    <td><button class="btn btn-sm btn-outline-danger" (click)="removeEntry($index)"><i class="fa fa-times"></i></button></td>
                  </tr>
                }
              </tbody>
            </table>
            <div class="d-flex justify-content-between align-items-center">
              <button class="btn btn-sm btn-outline-secondary" (click)="addEntry()">
                <i class="fa fa-plus me-1"></i>{{ '::AddEntry' | abpLocalization }}
              </button>
              <span class="badge" [class.bg-success]="totalPercentage === 100" [class.bg-danger]="totalPercentage !== 100">
                {{ '::Total' | abpLocalization }}: {{ totalPercentage }}%
              </span>
            </div>
            <div class="mt-3">
              <button class="btn btn-primary btn-sm" (click)="createAllocation()" [disabled]="totalPercentage !== 100">
                {{ '::Save' | abpLocalization }}
              </button>
              <button class="btn btn-outline-secondary btn-sm ms-2" (click)="showCreateForm = false">
                {{ '::Cancel' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      }

      @if (allocations().length === 0) {
        <div class="text-center text-muted py-5">
          <i class="fa fa-sitemap fa-3x mb-3"></i>
          <p>{{ '::NoCostCenterAllocationsYet' | abpLocalization }}</p>
        </div>
      } @else {
        <div class="table-responsive">
          <table class="table table-hover table-sm">
            <thead>
              <tr>
                <th>{{ '::MainCostCenter' | abpLocalization }}</th>
                <th>{{ '::ValidFrom' | abpLocalization }}</th>
                <th>{{ '::Entries' | abpLocalization }}</th>
                <th>{{ '::Status' | abpLocalization }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (alloc of allocations(); track alloc.id) {
                <tr>
                  <td>{{ getCostCenterName(alloc.mainCostCenterId) }}</td>
                  <td>{{ alloc.validFrom | date:'dd/MM/yyyy' }}</td>
                  <td>{{ alloc.entries.length }} {{ '::Entries' | abpLocalization }}</td>
                  <td>
                    <span class="badge" [class.bg-success]="alloc.isActive" [class.bg-secondary]="!alloc.isActive">
                      {{ alloc.isActive ? 'Active' : 'Inactive' }}
                    </span>
                  </td>
                  <td>
                    <button class="btn btn-sm btn-outline-warning" (click)="toggleActive(alloc.id)">
                      <i class="fa" [class.fa-pause]="alloc.isActive" [class.fa-play]="!alloc.isActive"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger ms-1" (click)="deleteAllocation(alloc.id)">
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
  `
})
export class CostCenterAllocationListComponent implements OnInit {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);
  private companyContext = inject(CompanyContextService);

  allocations = signal<Allocation[]>([]);
  costCenters = signal<CostCenter[]>([]);
  showCreateForm = false;

  newAllocation = {
    mainCostCenterId: '',
    validFrom: new Date().toISOString().split('T')[0],
    entries: [{ childCostCenterId: '', percentage: 0 }] as AllocationEntry[]
  };

  get totalPercentage(): number {
    return this.newAllocation.entries.reduce((sum, e) => sum + (e.percentage || 0), 0);
  }

  ngOnInit() {
    this.loadCostCenters();
    this.loadAllocations();
  }

  loadCostCenters() {
    const companyId = this.companyContext.currentCompanyId();
    this.http.get<any>(`/api/app/cost-center?companyId=${companyId}&maxResultCount=500`).subscribe(res => {
      this.costCenters.set(res.items ?? []);
    });
  }

  loadAllocations() {
    const companyId = this.companyContext.currentCompanyId();
    this.http.get<any>(`/api/app/cost-center-allocation?companyId=${companyId}&maxResultCount=100`).subscribe(res => {
      this.allocations.set(res.items ?? []);
    });
  }

  addEntry() {
    this.newAllocation.entries.push({ childCostCenterId: '', percentage: 0 });
  }

  removeEntry(index: number) {
    this.newAllocation.entries.splice(index, 1);
  }

  createAllocation() {
    const companyId = this.companyContext.currentCompanyId();
    const dto = {
      companyId,
      mainCostCenterId: this.newAllocation.mainCostCenterId,
      validFrom: this.newAllocation.validFrom,
      entries: this.newAllocation.entries.filter(e => e.childCostCenterId)
    };
    this.http.post('/api/app/cost-center-allocation', dto).subscribe({
      next: () => {
        this.toaster.success('Cost center allocation created');
        this.showCreateForm = false;
        this.newAllocation = { mainCostCenterId: '', validFrom: new Date().toISOString().split('T')[0], entries: [{ childCostCenterId: '', percentage: 0 }] };
        this.loadAllocations();
      },
      error: () => {}
    });
  }

  toggleActive(id: string) {
    this.http.post(`/api/app/cost-center-allocation/${id}/toggle-active`, {}).subscribe({
      next: () => this.loadAllocations(),
      error: () => {}
    });
  }

  deleteAllocation(id: string) {
    if (confirm('Delete this allocation?')) {
      this.http.delete(`/api/app/cost-center-allocation/${id}`).subscribe({
        next: () => this.loadAllocations(),
        error: () => {}
      });
    }
  }

  getCostCenterName(id: string): string {
    return this.costCenters().find(cc => cc.id === id)?.name || id?.slice(0, 8) || '—';
  }
}
