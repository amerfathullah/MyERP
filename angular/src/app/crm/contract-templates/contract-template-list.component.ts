import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ContractTemplateService } from '../../proxy/crm/contract-template.service';
import type { ContractTemplateDto } from '../../proxy/crm/models';

@Component({
  selector: 'app-contract-template-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'::ContractTemplates' | abpLocalization">
      <div class="d-flex justify-content-between gap-2 mb-3">
        <input type="text" class="form-control form-control-sm" style="width:200px"
          [(ngModel)]="searchTerm" (keyup.enter)="load()" [placeholder]="'::Placeholder:Search' | abpLocalization">
        <button class="btn btn-primary btn-sm" routerLink="/crm/contract-templates/new">
          <i class="fa fa-plus me-1"></i>{{ '::New' | abpLocalization }}
        </button>
      </div>
      <div class="table-responsive">
        <table class="table table-hover align-middle">
          <thead><tr><th>{{ '::Title' | abpLocalization }}</th><th>{{ '::RequiresFulfilment' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (item of items(); track item.id) {
              <tr>
                <td>{{ item.title }}</td>
                <td>@if (item.requiresFulfilment) { <i class="fa fa-check text-success"></i> }</td>
                <td>
                  <div class="btn-group btn-group-sm">
                    <a class="btn btn-outline-primary" [routerLink]="'/crm/contract-templates/' + item.id"><i class="fa fa-edit"></i></a>
                    <button class="btn btn-outline-danger" (click)="delete(item)"><i class="fa fa-trash"></i></button>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </abp-page>
  `,
})
export class ContractTemplateListComponent implements OnInit {
  private service = inject(ContractTemplateService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  items = signal<ContractTemplateDto[]>([]);
  searchTerm = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList({ skipCount: 0, maxResultCount: 200, sorting: 'title', filter: this.searchTerm } as any)
      .subscribe({ next: (r) => this.items.set(r.items ?? []), error: () => {} });
  }

  delete(item: ContractTemplateDto): void {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === 'confirm') {
        this.service.delete(item.id!).subscribe({
          next: () => { this.toaster.success('::SuccessfullyDeleted'); this.load(); },
          error: (err: any) => this.toaster.error(err?.error?.error?.message ?? 'Delete failed'),
        });
      }
    });
  }
}
