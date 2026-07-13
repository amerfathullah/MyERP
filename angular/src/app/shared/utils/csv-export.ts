/**
 * Generates a CSV file download from an array of objects.
 * Usage: exportToCsv('report.csv', data, ['col1', 'col2']);
 */
export function exportToCsv(filename: string, rows: any[], columns: string[]): void {
  if (!rows.length) return;

  const headers = columns.join(',');
  const csvRows = rows.map(row =>
    columns.map(col => {
      const val = row[col] ?? '';
      // Escape commas and quotes in values
      const str = String(val).replace(/"/g, '""');
      return str.includes(',') || str.includes('"') || str.includes('\n') ? `"${str}"` : str;
    }).join(',')
  );

  const csv = [headers, ...csvRows].join('\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
