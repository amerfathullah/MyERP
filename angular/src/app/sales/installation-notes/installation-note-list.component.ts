import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { InstallationNoteService } from '../../proxy/sales/installation-note.service';
import { CustomerService } from '../../proxy/sales/customer.service';
import { CompanyContextService } from '../../shared/services/company-context.service';
import { PaginationComponent, type PageEvent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-installation-note-list',
  standalone: true,
  imports: [PaginationComponent, CommonModule, RouterModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'InstallationNotes' | abpLocalization">
      @if (isLoading) {
        <div class="text-center py-5"><i class="fa fa-spinner fa-spin fa-2x"></i></div>
      }
      @if (!isLoading && notes.length === 0) {
        <div class="text-center py-5">
          <i class="fa fa-screwdriver-wrench fa-3x text-muted mb-3 d-block"></i>
          <p class="text-muted">No installation notes yet.</p>
        </div>
      } @else if (!isLoading) {
        <div class="card"><div class="card-body">
          <table class="table table-hover mb-0">
            <thead><tr>
              <th>{{ 'Number' | abpLocalization }}</th>
              <th>{{ 'Date' | abpLocalization }}</th>
              <th>{{ 'Customer' | abpLocalization }}</th>
              <th>Items</th>
              <th>{{ 'Status' | abpLocalization }}</th>
            </tr></thead>
            <tbody>
              @for (n of notes; track n.id) {
                <tr>
                  <td>{{ n.installationNumber }}</td>
                  <td>{{ n.installationDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ customerNames()[n.customerId ?? ''] || n.customerId }}</td>
                  <td>{{ n.items?.length ?? 0 }}</td>
                  <td><span class="badge" [class]="getStatusClass(n.status)">{{ n.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div></div>
        <app-pagination [totalCount]="totalCount" [pageSize]="pageSize" [currentPage]="currentPage"
          (pageChange)="onPageChange($event)" />
      }
    </abp-page>
  `
})
export class InstallationNoteListComponent implements OnInit {
  private installationNoteService = inject(InstallationNoteService);
  private customerService = inject(CustomerService);
  private companyContext = inject(CompanyContextService);
  customerNames = signal<Record<string, string>>({});
  notes: any[] = [];
  isLoading = false;
  totalCount = 0;
  pageSize = 20;
  currentPage = 0;

  ngOnInit() {
    this.loadData();
    this.customerService.getList({ skipCount: 0, maxResultCount: 200, sorting: 'name asc' }).subscribe(r => {
      const map: Record<string, string> = {};
      (r.items ?? []).forEach((c: any) => { map[c.id] = c.name || c.customerCode || c.id; });
      this.customerNames.set(map);
    });
  }

  loadData() {
    this.isLoading = true;
    const companyId = this.companyContext.currentCompanyId();
    const params: any = { skipCount: String(this.currentPage * this.pageSize), maxResultCount: String(this.pageSize) };
    if (companyId) params.companyId = companyId;
    this.installationNoteService.getList({ skipCount: this.currentPage * this.pageSize, maxResultCount: this.pageSize, companyId: companyId || undefined, sorting: '' } as any).subscribe({
      next: res => { this.notes = res.items ?? []; this.totalCount = res.totalCount ?? 0; this.isLoading = false; },
      error: () => { this.isLoading = false; }
    });
  }

  getStatusClass(status: string): string {
    switch (status) { case 'Submitted': return 'bg-success'; case 'Cancelled': return 'bg-danger'; default: return 'bg-secondary'; }
  }

  onPageChange(e: PageEvent) { this.currentPage = e.pageIndex; this.loadData(); }
}
