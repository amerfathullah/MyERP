import { Injectable, inject, signal } from '@angular/core';
import { CompanyService } from '../../proxy/core/company.service';

export interface CompanyOption {
  id: string;
  name: string;
}

/**
 * Global company context service. Stores the currently selected company
 * and makes it available across all components.
 * Components can read `currentCompany()` to filter data.
 */
@Injectable({ providedIn: 'root' })
export class CompanyContextService {
  private companyService = inject(CompanyService);

  companies = signal<CompanyOption[]>([]);
  currentCompanyId = signal<string>('');
  currentCompanyName = signal<string>('');

  private loaded = false;

  load(): void {
    if (this.loaded) return;
    this.loaded = true;
    this.companyService.getList({ skipCount: 0, maxResultCount: 50, sorting: '' })
      .subscribe(res => {
        const items = (res.items ?? []).map(c => ({ id: c.id!, name: c.name ?? '' }));
        this.companies.set(items);
        // Auto-select first company if none set
        if (!this.currentCompanyId() && items.length > 0) {
          this.selectCompany(items[0].id, items[0].name);
        }
      });
  }

  selectCompany(id: string, name: string): void {
    this.currentCompanyId.set(id);
    this.currentCompanyName.set(name);
    // Persist in localStorage
    localStorage.setItem('myerp_company_id', id);
    localStorage.setItem('myerp_company_name', name);
  }

  constructor() {
    // Restore from localStorage
    const savedId = localStorage.getItem('myerp_company_id');
    const savedName = localStorage.getItem('myerp_company_name');
    if (savedId) {
      this.currentCompanyId.set(savedId);
      this.currentCompanyName.set(savedName ?? '');
    }
  }
}
