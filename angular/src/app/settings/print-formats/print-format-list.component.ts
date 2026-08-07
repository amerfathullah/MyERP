import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { PrintFormatService } from '../../proxy/settings/print-format.service';
import { PrintFormatDto } from '../../proxy/settings/models';

@Component({
  selector: 'app-print-format-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title">Print Formats</h5>
        <a routerLink="/settings/print-formats/new" class="btn btn-primary btn-sm">New Print Format</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered">
          <thead>
            <tr>
              <th>Name</th>
              <th>Document Type</th>
              <th>Is Default</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let format of formats">
              <td>
                <a [routerLink]="['/settings/print-formats', format.id]">{{ format.name }}</a>
              </td>
              <td>{{ format.documentType }}</td>
              <td>
                <span class="badge" [ngClass]="format.isDefault ? 'bg-success' : 'bg-secondary'">
                  {{ format.isDefault ? 'Yes' : 'No' }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `
})
export class PrintFormatListComponent implements OnInit {
  private service = inject(PrintFormatService);
  formats: PrintFormatDto[] = [];

  ngOnInit() {
    this.service.getList({ maxResultCount: 100, skipCount: 0 }).subscribe(res => {
      this.formats = res.items;
    });
  }
}
