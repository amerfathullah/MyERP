import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { PrintFormatService } from '../../proxy/settings/print-format.service';
import { PrintFormatDto } from '../../proxy/settings/models';

@Component({
  selector: 'app-print-format-list',
  standalone: true,
  imports: [CommonModule, RouterModule, LocalizationPipe],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="card-title">{{ '::PrintFormats' | abpLocalization }}</h5>
        <a routerLink="/settings/print-formats/new" class="btn btn-primary btn-sm">{{ '::NewPrintFormat' | abpLocalization }}</a>
      </div>
      <div class="card-body">
        <table class="table table-bordered">
          <thead>
            <tr>
              <th>{{ '::Name' | abpLocalization }}</th>
              <th>{{ '::DocumentType' | abpLocalization }}</th>
              <th>{{ '::IsDefault' | abpLocalization }}</th>
            </tr>
          </thead>
          <tbody>
            @for (format of formats; track format.id) {
              <tr>
                <td>
                  <a [routerLink]="['/settings/print-formats', format.id]">{{ format.name }}</a>
                </td>
                <td>{{ format.documentType }}</td>
                <td>
                  <span class="badge" [ngClass]="format.isDefault ? 'bg-success' : 'bg-secondary'">
                    {{ (format.isDefault ? '::Yes' : '::No') | abpLocalization }}
                  </span>
                </td>
              </tr>
            }
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
