import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ImportedTransactionsFileDto, StagingRowDto, UpdateStagingRowRequest } from './qif-import.service';

// ── OFX-specific DTOs ────────────────────────────────────────────────────────

/** Staging row returned by the OFX import pipeline. Extends the QIF base row with OFX fields. */
export interface OfxStagingRowDto extends StagingRowDto {
    /** Raw FITID from the OFX file. Null if the file did not include one. */
    externalId?: string;
    /** Currency code from OFX CURDEF (e.g. AUD, USD). */
    currency?: string;
    /** OFX TRNTYPE (DEBIT, CREDIT, etc.) — diagnostic only. */
    ofxTrnType?: string;
    /**
     * Server-computed label indicating the transaction type this row will commit as.
     * Values: "Income" | "Expense" | "Transfer" | "Loan Repayment" | "Loan Disbursement"
     */
    inferredType?: string;
}

/** File-level DTO returned by the OFX import pipeline. */
export interface OfxImportedTransactionsFileDto extends ImportedTransactionsFileDto {
    /** Non-fatal warning (e.g. multiple statement blocks found). */
    importWarning?: string;
}

/** Review wrapper returned by GET api/ofx-import/{fileId}/staged. */
export interface OfxStagedImportReviewDto {
    file: OfxImportedTransactionsFileDto;
    validRows: OfxStagingRowDto[];
    duplicateRows: OfxStagingRowDto[];
    alreadyImportedRows: OfxStagingRowDto[];
    errorRows: OfxStagingRowDto[];
}

// ── Service ──────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class OfxImportService {
    private http = inject(HttpClient);

    private readonly baseUrl = `${environment.apiUrl}/ofx-import`;

    /**
     * Upload a .ofx file for staging. Returns the created import file record.
     */
    upload(
        file: File,
        accountId: string,
        allowDuplicates: boolean
    ): Observable<OfxImportedTransactionsFileDto> {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('accountId', accountId);
        formData.append('allowDuplicates', String(allowDuplicates));
        return this.http.post<OfxImportedTransactionsFileDto>(`${this.baseUrl}/upload`, formData);
    }

    /**
     * Get the review data (all staged rows grouped by status).
     */
    getStagedImport(fileId: string): Observable<OfxStagedImportReviewDto> {
        return this.http.get<OfxStagedImportReviewDto>(`${this.baseUrl}/${fileId}/staged`);
    }

    /**
     * Update a single staging row's account/category selections.
     */
    updateStagingRow(
        fileId: string,
        rowId: string,
        request: UpdateStagingRowRequest
    ): Observable<OfxStagingRowDto> {
        return this.http.put<OfxStagingRowDto>(
            `${this.baseUrl}/${fileId}/staging/${rowId}`,
            request
        );
    }

    /**
     * Commit all valid staged rows into the transactions table.
     */
    commitImport(fileId: string): Observable<OfxImportedTransactionsFileDto> {
        return this.http.post<OfxImportedTransactionsFileDto>(
            `${this.baseUrl}/${fileId}/commit`,
            {}
        );
    }

    /**
     * List all OFX import files for the current entity.
     */
    getImportFiles(): Observable<OfxImportedTransactionsFileDto[]> {
        return this.http.get<OfxImportedTransactionsFileDto[]>(this.baseUrl);
    }

    /**
     * Soft-delete an OFX import file and all its staging rows.
     */
    deleteImportFile(fileId: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/${fileId}`);
    }
}
