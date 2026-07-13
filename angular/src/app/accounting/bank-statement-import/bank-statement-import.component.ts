import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

@Component({
  selector: 'app-bank-statement-import',
  standalone: true,
  imports: [CommonModule, FormsModule, LocalizationPipe],
  templateUrl: './bank-statement-import.component.html',
})
export class BankStatementImportComponent {
  private http = inject(HttpClient);
  private toaster = inject(ToasterService);

  bankAccountId = signal<string>('');
  companyId = signal<string>('');
  csvContent = signal<string>('');
  fileName = signal<string>('');
  importing = signal(false);
  result = signal<{ importedCount: number; skippedCount: number; errors: string[] } | null>(null);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      this.fileName.set(file.name);

      const reader = new FileReader();
      reader.onload = () => {
        this.csvContent.set(reader.result as string);
      };
      reader.readAsText(file);
    }
  }

  importStatement(): void {
    if (!this.csvContent() || !this.bankAccountId()) return;

    this.importing.set(true);
    this.result.set(null);

    this.http.post<any>('/api/app/bank-statement-import/import-from-csv', {
      companyId: this.companyId(),
      bankAccountId: this.bankAccountId(),
      csvContent: this.csvContent(),
    }).subscribe({
      next: (res) => {
        this.result.set(res);
        this.importing.set(false);
        this.toaster.success(`Imported ${res.importedCount} transactions`);
      },
      error: (err) => {
        this.result.set({ importedCount: 0, skippedCount: 0, errors: [err?.error?.error?.message ?? 'Import failed'] });
        this.importing.set(false);
      },
    });
  }
}
