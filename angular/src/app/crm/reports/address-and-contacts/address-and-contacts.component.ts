import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { AddressAndContactsService } from '../../../proxy/crm/address-and-contacts.service';
import type {
  AddressAndContactsReportDto,
  AddressAndContactRowDto,
} from '../../../proxy/crm/models';
import { CompanyContextService } from '../../../shared/services/company-context.service';
import { exportToCsv } from '../../../shared/utils/csv-export';

@Component({
  selector: 'app-address-and-contacts',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  template: `
    <div class="container-fluid py-3">
      <!-- Header -->
      <div class="d-flex justify-content-between align-items-center mb-3">
        <div>
          <h4 class="mb-1">
            <i class="fa fa-address-book text-primary me-2"></i>
            {{ '::AddressAndContacts' | abpLocalization }}
          </h4>
          <p class="text-muted small mb-0">
            {{ '::AddressAndContactsDescription' | abpLocalization }}
          </p>
        </div>
        @if (report() && report()!.rows && report()!.rows.length > 0) {
          <button class="btn btn-sm btn-outline-success" (click)="exportCsv()">
            <i class="fa fa-file-csv me-1"></i>{{ '::ExportCSV' | abpLocalization }}
          </button>
        }
      </div>

      <!-- Filters -->
      <div class="card mb-3 shadow-sm border-0">
        <div class="card-body py-2">
          <div class="row g-2 align-items-end">
            <div class="col-md-3">
              <label class="form-label small mb-1">{{ '::PartyType' | abpLocalization }}</label>
              <select
                class="form-select form-select-sm"
                [(ngModel)]="selectedPartyType"
                (ngModelChange)="loadReport()"
              >
                <option value="Customer">{{ '::Customer' | abpLocalization }}</option>
                <option value="Supplier">{{ '::Supplier' | abpLocalization }}</option>
                <option value="SalesPartner">{{ '::SalesPartner' | abpLocalization }}</option>
                <option value="Lead">{{ '::Lead' | abpLocalization }}</option>
              </select>
            </div>
            <div class="col-md-4">
              <label class="form-label small mb-1">{{ '::Search' | abpLocalization }}</label>
              <input
                type="text"
                class="form-control form-control-sm"
                placeholder="{{ '::SearchAddressContactPlaceholder' | abpLocalization }}"
                [(ngModel)]="searchTerm"
                (keyup.enter)="loadReport()"
              />
            </div>
            <div class="col-md-2 d-flex align-items-end">
              <button
                class="btn btn-sm btn-primary w-100"
                (click)="loadReport()"
                [disabled]="loading()"
              >
                @if (loading()) {
                  <span class="spinner-border spinner-border-sm me-1"></span>
                } @else {
                  <i class="fa fa-sync me-1"></i>
                }
                {{ '::Refresh' | abpLocalization }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Highlights Summary -->
      @if (report()) {
        <div class="row g-2 mb-3">
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-primary-subtle text-primary py-2 px-3">
              <div class="small fw-bold">{{ '::TotalParties' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalParties }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-info-subtle text-info-emphasis py-2 px-3">
              <div class="small fw-bold">{{ '::TotalAddresses' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalAddresses }}</div>
            </div>
          </div>
          <div class="col-md-4">
            <div class="card border-0 shadow-sm bg-success-subtle text-success py-2 px-3">
              <div class="small fw-bold">{{ '::TotalContacts' | abpLocalization }}</div>
              <div class="h4 mb-0 fw-bold">{{ report()!.totalContacts }}</div>
            </div>
          </div>
        </div>
      }

      <!-- Table -->
      <div class="card shadow-sm border-0">
        <div class="card-body p-0">
          @if (loading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
            </div>
          } @else if (!report() || !report()!.rows || report()!.rows.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="fa fa-inbox fa-3x mb-3 text-secondary"></i>
              <p class="mb-0">{{ '::NoRecordsFound' | abpLocalization }}</p>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover table-striped align-middle mb-0" style="font-size: 0.85rem;">
                <thead class="table-light">
                  <tr>
                    <th style="min-width: 180px;">{{ '::Party' | abpLocalization }}</th>
                    <th style="min-width: 250px;">{{ '::Address' | abpLocalization }}</th>
                    <th style="min-width: 250px;">{{ '::Contact' | abpLocalization }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of report()!.rows; track $index) {
                    <tr>
                      <!-- Party info -->
                      <td>
                        <div class="fw-bold text-dark">{{ row.partyName }}</div>
                        <div class="d-flex gap-1 mt-1 flex-wrap">
                          <span class="badge bg-secondary-subtle text-dark border">
                            {{ row.partyType }}
                          </span>
                          @if (row.partyCode) {
                            <span class="badge bg-light text-muted border">
                              {{ row.partyCode }}
                            </span>
                          }
                          @if (row.partyGroup) {
                            <span class="badge bg-info-subtle text-info-emphasis">
                              {{ row.partyGroup }}
                            </span>
                          }
                        </div>
                      </td>

                      <!-- Address info -->
                      <td>
                        @if (row.addressTitle || row.addressLine1 || row.city) {
                          <div class="d-flex align-items-center gap-1 mb-1">
                            <span class="fw-semibold text-primary">
                              {{ row.addressTitle || '—' }}
                            </span>
                            @if (row.addressType) {
                              <span class="badge bg-light text-secondary border">
                                {{ row.addressType }}
                              </span>
                            }
                            @if (row.isPrimaryAddress) {
                              <span class="badge bg-success-subtle text-success">
                                {{ '::Primary' | abpLocalization }}
                              </span>
                            }
                          </div>
                          <div class="small text-muted">
                            @if (row.addressLine1) { <div>{{ row.addressLine1 }}</div> }
                            @if (row.addressLine2) { <div>{{ row.addressLine2 }}</div> }
                            <div>
                              {{ formatCityStateCountry(row) }}
                            </div>
                          </div>
                        } @else {
                          <span class="text-muted fst-italic">{{ '::NoAddress' | abpLocalization }}</span>
                        }
                      </td>

                      <!-- Contact info -->
                      <td>
                        @if (row.contactName || row.email || row.phone || row.mobileNo) {
                          <div class="d-flex align-items-center gap-1 mb-1">
                            <span class="fw-semibold text-dark">
                              {{ row.contactName || '—' }}
                            </span>
                            @if (row.isPrimaryContact) {
                              <span class="badge bg-success-subtle text-success">
                                {{ '::Primary' | abpLocalization }}
                              </span>
                            }
                          </div>
                          <div class="small text-muted">
                            @if (row.designation || row.department) {
                              <div>{{ formatContactRole(row) }}</div>
                            }
                            @if (row.email) {
                              <div>
                                <i class="fa fa-envelope me-1 text-secondary"></i>
                                <a [href]="'mailto:' + row.email" class="text-decoration-none">
                                  {{ row.email }}
                                </a>
                              </div>
                            }
                            @if (row.phone || row.mobileNo) {
                              <div>
                                <i class="fa fa-phone me-1 text-secondary"></i>
                                {{ row.mobileNo || row.phone }}
                              </div>
                            }
                          </div>
                        } @else {
                          <span class="text-muted fst-italic">{{ '::NoContact' | abpLocalization }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .table td, .table th {
      padding: 0.6rem 0.75rem;
    }
  `],
})
export class AddressAndContactsComponent implements OnInit {
  private addressAndContactsService = inject(AddressAndContactsService);
  private companyContext = inject(CompanyContextService);

  report = signal<AddressAndContactsReportDto | null>(null);
  loading = signal(false);

  selectedPartyType = 'Customer';
  searchTerm = '';

  ngOnInit(): void {
    this.loadReport();
  }

  loadReport(): void {
    const companyId = this.companyContext.selectedCompanyId();
    if (!companyId) return;

    this.loading.set(true);

    this.addressAndContactsService
      .getReport({
        companyId,
        partyType: this.selectedPartyType,
        search: this.searchTerm.trim() || undefined,
      })
      .subscribe({
        next: (res) => {
          this.report.set(res);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  formatCityStateCountry(row: AddressAndContactRowDto): string {
    return [row.city, row.state, row.postalCode, row.country]
      .filter((v): v is string => Boolean(v && v.trim()))
      .join(', ');
  }

  formatContactRole(row: AddressAndContactRowDto): string {
    return [row.designation, row.department]
      .filter((v): v is string => Boolean(v && v.trim()))
      .join(' - ');
  }

  exportCsv(): void {
    const rep = this.report();
    if (!rep || !rep.rows || rep.rows.length === 0) return;

    const headers = [
      'Party Type',
      'Party Name',
      'Party Code',
      'Party Group',
      'Address Title',
      'Address Type',
      'Address Line 1',
      'Address Line 2',
      'City',
      'State',
      'Postal Code',
      'Country',
      'Is Primary Address',
      'Contact Name',
      'Designation',
      'Department',
      'Email',
      'Phone',
      'Mobile No',
      'Is Primary Contact',
    ];

    const dataRows = rep.rows.map((r) => ({
      'Party Type': r.partyType,
      'Party Name': r.partyName,
      'Party Code': r.partyCode || '',
      'Party Group': r.partyGroup || '',
      'Address Title': r.addressTitle || '',
      'Address Type': r.addressType || '',
      'Address Line 1': r.addressLine1 || '',
      'Address Line 2': r.addressLine2 || '',
      'City': r.city || '',
      'State': r.state || '',
      'Postal Code': r.postalCode || '',
      'Country': r.country || '',
      'Is Primary Address': r.isPrimaryAddress ? 'Yes' : 'No',
      'Contact Name': r.contactName || '',
      'Designation': r.designation || '',
      'Department': r.department || '',
      'Email': r.email || '',
      'Phone': r.phone || '',
      'Mobile No': r.mobileNo || '',
      'Is Primary Contact': r.isPrimaryContact ? 'Yes' : 'No',
    }));

    exportToCsv(`Address_And_Contacts_${this.selectedPartyType}.csv`, dataRows, headers);
  }
}
