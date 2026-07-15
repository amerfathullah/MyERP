import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageModule } from '@abp/ng.components/page';
import { LocalizationPipe } from '@abp/ng.core';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImportExportService } from '../proxy/import-export/import-export.service';
import type { ImportJobDto } from '../proxy/import-export/dtos/models';

@Component({
  selector: 'app-import-export',
  standalone: true,
  imports: [
    CommonModule, PageModule, LocalizationPipe, FormsModule, ReactiveFormsModule],
  templateUrl: './import-export.component.html',
  styleUrls: ['./import-export.component.scss'],
})
export class ImportExportComponent implements OnInit {
  private fb = inject(FormBuilder);
  private service = inject(ImportExportService);
  private toaster = inject(ToasterService);

  importForm = this.fb.group({
    entityType: ['Customer', Validators.required],
    file: [null as File | null, Validators.required],
  });

  entityTypes = ['Customer', 'Item'];
  exportEntityType = 'Customer';
  importHistory = signal<ImportJobDto[]>([]);
  isImporting = signal(false);
  isExporting = signal(false);

  selectedFileName = '';

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      this.importForm.patchValue({ file: input.files[0] });
      this.selectedFileName = input.files[0].name;
    }
  }

  startImport(): void {
    if (this.importForm.invalid) return;

    const file = this.importForm.get('file')?.value as File;
    const entityType = this.importForm.get('entityType')?.value!;

    this.isImporting.set(true);

    const reader = new FileReader();
    reader.onload = () => {
      const base64 = (reader.result as string).split(',')[1] || btoa(reader.result as string);
      this.service.startImport({
        entityType,
        fileName: file.name,
        fileContent: base64,
      }).subscribe({
        next: (result) => {
          this.isImporting.set(false);
          if (result.status === 2) { // Completed
            this.toaster.success(`Import completed: ${result.successCount} rows imported`);
          } else if (result.status === 4) { // PartialSuccess
            this.toaster.warn(`Partial import: ${result.successCount} succeeded, ${result.failureCount} failed`);
          } else {
            this.toaster.error(`Import failed: ${result.errorDetails}`);
          }
          this.loadHistory();
        },
        error: (err) => {
          this.isImporting.set(false);
          this.toaster.error(err?.error?.error?.message ?? 'Import failed');
        },
      });
    };
    reader.readAsDataURL(file);
  }

  exportData(): void {
    this.isExporting.set(true);
    this.service.export({ entityType: this.exportEntityType, format: 0 }).subscribe({
      next: (result) => {
        this.isExporting.set(false);
        // Trigger download
        const byteArray = Uint8Array.from(atob(result.fileContent), c => c.charCodeAt(0));
        const blob = new Blob([byteArray], { type: result.contentType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = result.fileName;
        a.click();
        URL.revokeObjectURL(url);
        this.toaster.success('Export downloaded');
      },
      error: (err) => {
        this.isExporting.set(false);
        this.toaster.error(err?.error?.error?.message ?? 'Export failed');
      },
    });
  }

  loadHistory(): void {
    this.service.getImportHistory({ skipCount: 0, maxResultCount: 10, sorting: '' }).subscribe({
      next: (result) => {
        this.importHistory.set(result.items ?? []);
      },
    });
  }

  ngOnInit(): void {
    this.loadHistory();
  }

  getStatusLabel(status: number): string {
    const map: Record<number, string> = { 0: 'Pending', 1: 'Processing', 2: 'Completed', 3: 'Failed', 4: 'Partial' };
    return map[status] ?? 'Unknown';
  }

  getStatusColor(status: number): string {
    const map: Record<number, string> = { 0: '', 1: 'accent', 2: 'primary', 3: 'warn', 4: 'warn' };
    return map[status] ?? '';
  }
}
