import type { DocumentPrintResult } from '../../proxy/core/models';

/** Decodes a base64-encoded PDF from a DocumentPrintResult and triggers a browser download. */
export function downloadPdfFromResult(result: DocumentPrintResult, fallbackFileName: string): boolean {
  if (!result.pdfBytes) {
    return false;
  }
  const binary = atob(result.pdfBytes);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  const blob = new Blob([bytes], { type: 'application/pdf' });
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = result.fileName || fallbackFileName;
  link.click();
  window.URL.revokeObjectURL(url);
  return true;
}
