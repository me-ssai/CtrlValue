import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// ── DTOs ────────────────────────────────────────────────────────────────────

export interface ImportedTransactionsFileDto {
    id: string;
    entityId: string;
    accountId: string;
    accountName?: string;
    originalFilename: string;
    uploadedAt: string;
    processedAt?: string;
    status: string;
    allowDuplicates: boolean;
    totalRows: number;
    validRows: number;
    duplicateRows: number;
    alreadyImportedRows: number;
    errorRows: number;
}

export interface StagingRowDto {
    id: string;
    importedTransactionsFileId: string;
    accountId: string;
    counterAccountId?: string;
    counterAccountName?: string;
    categoryId?: string;
    categoryName?: string;
    transactionDate: string;
    description: string;
    notes?: string;
    amount: number;
    amountRaw: number;
    status: string;
    errorReason?: string;
}

export interface StagedImportReviewDto {
    file: ImportedTransactionsFileDto;
    validRows: StagingRowDto[];
    duplicateRows: StagingRowDto[];
    alreadyImportedRows: StagingRowDto[];
    errorRows: StagingRowDto[];
}

export interface UpdateStagingRowRequest {
    counterAccountId?: string;
    categoryId?: string;
    ignoreDuplicateWarning?: boolean;
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class QifImportService {
    private http = inject(HttpClient);

    private readonly baseUrl = `${environment.apiUrl}/qif-import`;

    /**
     * Upload a .qif file for staging. Returns the created import file record.
     */
    upload(
        file: File,
        accountId: string,
        allowDuplicates: boolean,
        dateFormat: string
    ): Observable<ImportedTransactionsFileDto> {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('accountId', accountId);
        formData.append('allowDuplicates', String(allowDuplicates));
        if (dateFormat) {
            formData.append('dateFormat', dateFormat);
        }
        return this.http.post<ImportedTransactionsFileDto>(`${this.baseUrl}/upload`, formData);
    }

    /**
     * Get the review data (all staged rows grouped by status).
     */
    getStagedImport(fileId: string): Observable<StagedImportReviewDto> {
        return this.http.get<StagedImportReviewDto>(`${this.baseUrl}/${fileId}/staged`);
    }

    /**
     * Update a single staging row's account/category selections.
     */
    updateStagingRow(
        fileId: string,
        rowId: string,
        request: UpdateStagingRowRequest
    ): Observable<StagingRowDto> {
        return this.http.put<StagingRowDto>(
            `${this.baseUrl}/${fileId}/staging/${rowId}`,
            request
        );
    }

    /**
     * Commit all valid staged rows into the transactions table.
     */
    commitImport(fileId: string): Observable<ImportedTransactionsFileDto> {
        return this.http.post<ImportedTransactionsFileDto>(
            `${this.baseUrl}/${fileId}/commit`,
            {}
        );
    }

    /**
     * List all import files for the current entity.
     */
    getImportFiles(): Observable<ImportedTransactionsFileDto[]> {
        return this.http.get<ImportedTransactionsFileDto[]>(this.baseUrl);
    }

    /**
     * Soft-delete an import file and all its staging rows.
     */
    deleteImportFile(fileId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/${fileId}`);
    }
}
