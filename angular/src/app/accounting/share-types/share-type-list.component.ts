import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ShareTypeService } from '../../proxy/accounting/share-type.service';
import type { ShareTypeDto } from '../../proxy/accounting/models';

@Component({
  selector: 'app-share-type-list',
  standalone: true,
  imports: [CommonModule, FormsModule, PageModule, LocalizationPipe],
  template: `
    <abp-page [title]="'ShareTypes' | abpLocalization">
      <div class="card mb-3"><div class="card-body">
        <div class="row g-2 align-items-end">
          <div class="col-md-4">
            <label class="form-label">{{ 'Title' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="newTitle" />
          </div>
          <div class="col-md-5">
            <label class="form-label">{{ 'Description' | abpLocalization }}</label>
            <input class="form-control" [(ngModel)]="newDescription" />
          </div>
          <div class="col-md-3">
            <button class="btn btn-primary" (click)="add()" [disabled]="!newTitle"><i class="fa fa-plus me-1"></i>{{ 'AddItem' | abpLocalization }}</button>
          </div>
        </div>
      </div></div>

      <div class="card"><div class="card-body">
        <table class="table table-hover mb-0">
          <thead><tr><th>{{ 'Title' | abpLocalization }}</th><th>{{ 'Description' | abpLocalization }}</th><th></th></tr></thead>
          <tbody>
            @for (t of types; track t.id) {
              <tr>
                <td>{{ t.title }}</td>
                <td>{{ t.description }}</td>
                <td>
                  <button class="btn btn-sm btn-outline-danger" (click)="remove(t)"><i class="fa fa-trash"></i></button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div></div>
    </abp-page>
  `,
})
export class ShareTypeListComponent implements OnInit {
  private service = inject(ShareTypeService);
  private confirmation = inject(ConfirmationService);
  private toaster = inject(ToasterService);

  types: ShareTypeDto[] = [];
  newTitle = '';
  newDescription = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.service.getList().subscribe(r => this.types = r.items ?? []);
  }

  add(): void {
    this.service.create({ title: this.newTitle, description: this.newDescription || null }).subscribe(() => {
      this.toaster.success('::SuccessfullySaved');
      this.newTitle = '';
      this.newDescription = '';
      this.load();
    });
  }

  remove(t: ShareTypeDto): void {
    this.confirmation.warn('::AreYouSure', '::AreYouSure').subscribe((status) => {
      if (status !== Confirmation.Status.confirm) return;
      this.service.delete(t.id!).subscribe(() => { this.toaster.success('::SuccessfullyDeleted'); this.load(); });
    });
  }
}
