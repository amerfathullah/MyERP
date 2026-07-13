import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CompanyContextService } from '../../services/company-context.service';

@Component({
  selector: 'app-company-selector',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dropdown d-inline-block">
      <button class="btn btn-sm btn-outline-secondary dropdown-toggle" data-bs-toggle="dropdown" data-bs-auto-close="true">
        <i class="fa fa-building me-1"></i>{{ ctx.currentCompanyName() || 'Select Company' }}
      </button>
      <ul class="dropdown-menu dropdown-menu-end">
        @for (c of ctx.companies(); track c.id) {
          <li>
            <button class="dropdown-item" [class.active]="c.id === ctx.currentCompanyId()" (click)="select(c)">
              {{ c.name }}
            </button>
          </li>
        }
      </ul>
    </div>
  `,
})
export class CompanySelectorComponent implements OnInit {
  ctx = inject(CompanyContextService);

  ngOnInit(): void { this.ctx.load(); }

  select(company: { id: string; name: string }): void {
    this.ctx.selectCompany(company.id, company.name);
  }
}
