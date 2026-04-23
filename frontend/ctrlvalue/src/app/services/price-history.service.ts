import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

// ═══════════════════════════════════════════════════════════════════════════
// Price History Models
// ═══════════════════════════════════════════════════════════════════════════

export interface PriceHistory {
    id: string;
    instrumentId: string;
    instrumentSymbol: string;
    asOfDate: string;
    openPrice?: number;
    closePrice: number;
    highPrice?: number;
    lowPrice?: number;
    volume?: number;
    currency: string;
    source?: string;
    createdAt: string;
}

export interface CreatePriceHistoryRequest {
    instrumentId: string;
    asOfDate: string;
    openPrice?: number;
    closePrice: number;
    highPrice?: number;
    lowPrice?: number;
    volume?: number;
    currency: string;
    source?: string;
}

export interface BulkPriceImportRequest {
    instrumentId: string;
    prices: PriceDataPoint[];
}

export interface PriceDataPoint {
    date: string;
    price: number;
    open?: number;
    high?: number;
    low?: number;
    volume?: number;
}

// ═══════════════════════════════════════════════════════════════════════════
// Service
// ═══════════════════════════════════════════════════════════════════════════

@Injectable({ providedIn: 'root' })
export class PriceHistoryService {
    private http = inject(HttpClient);


    getPriceHistory(instrumentId: string, startDate?: string, endDate?: string): Observable<PriceHistory[]> {
        const params: Record<string, string> = {};
        if (startDate) params['startDate'] = startDate;
        if (endDate) params['endDate'] = endDate;
        return this.http.get<PriceHistory[]>(
            `${environment.apiUrl}/pricehistory/instrument/${instrumentId}`, { params });
    }

    getLatestPrice(instrumentId: string): Observable<PriceHistory> {
        return this.http.get<PriceHistory>(
            `${environment.apiUrl}/pricehistory/instrument/${instrumentId}/latest`);
    }

    createPriceHistory(request: CreatePriceHistoryRequest): Observable<PriceHistory> {
        return this.http.post<PriceHistory>(`${environment.apiUrl}/pricehistory`, request);
    }

    bulkImportPrices(request: BulkPriceImportRequest): Observable<{ imported: number; message: string }> {
        return this.http.post<{ imported: number; message: string }>(
            `${environment.apiUrl}/pricehistory/bulk-import`, request);
    }

    deletePriceHistory(id: string): Observable<void> {
        return this.http.delete<void>(`${environment.apiUrl}/pricehistory/${id}`);
    }
}
