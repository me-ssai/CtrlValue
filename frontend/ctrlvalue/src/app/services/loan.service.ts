import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Client } from './api.generated';
import { EntityService } from './entity.service';
import { environment } from '@env/environment';

// ── Interfaces (will be replaced by generated types after next refresh-api-dev) ──

export interface LoanRateHistoryDto {
  id: string;
  rate: number;
  effectiveFrom: Date;
  notes?: string;
  createdAt: Date;
}

export interface LoanDetailsDto {
  id: string;
  accountId: string;
  accountName: string;
  entityId: string;
  propertyAccountId?: string;
  propertyAccountName?: string;
  offsetAccountId?: string;
  offsetAccountName?: string;
  loanAmount: number;
  interestRate: number;
  rateType: 'Fixed' | 'Variable';
  fixedRateExpiresAt?: Date;
  paymentFrequency: 'Weekly' | 'Fortnightly' | 'Monthly';
  repaymentAmount: number;
  loanTermMonths: number;
  startDate: Date;
  nextPaymentDate: Date;
  isInterestOnly: boolean;
  redrawAvailable: number;
  notes?: string;
  rateHistory: LoanRateHistoryDto[];
  createdAt: Date;
  updatedAt?: Date;
}

export interface LoanSummaryDto {
  accountId: string;
  accountName: string;
  remainingBalance: number;
  currentInterestRate: number;
  rateType: string;
  fixedRateExpiresAt?: Date;
  daysUntilFixedRateExpiry?: number;
  nextPaymentAmount: number;
  nextPaymentDate: Date;
  daysUntilNextPayment: number;
  redrawAvailable: number;
  lvr?: number;
  propertyValue?: number;
  monthsRemaining: number;
  totalInterestPayable: number;
  offsetAccountId?: string;
  offsetAccountName?: string;
  offsetBalance: number;
}

export interface AmortisationRowDto {
  paymentNumber: number;
  paymentDate: Date;
  paymentAmount: number;
  principal: number;
  interest: number;
  cumulativeInterest: number;
  balance: number;
}

export interface AmortisationScheduleDto {
  standard: AmortisationRowDto[];
  accelerated: AmortisationRowDto[];
  extraPaymentPerPeriod: number;
  monthsSaved: number;
  interestSaved: number;
}

export interface CreateLoanDetailsRequest {
  accountId: string;
  propertyAccountId?: string;
  offsetAccountId?: string;
  loanAmount: number;
  interestRate: number;
  rateType: string;
  fixedRateExpiresAt?: Date;
  paymentFrequency: string;
  repaymentAmount: number;
  loanTermMonths: number;
  startDate: Date;
  isInterestOnly: boolean;
  notes?: string;
}

export interface UpdateLoanDetailsRequest {
  propertyAccountId?: string;
  offsetAccountId?: string;
  loanAmount: number;
  interestRate: number;
  rateType: string;
  fixedRateExpiresAt?: Date;
  paymentFrequency: string;
  repaymentAmount: number;
  loanTermMonths: number;
  startDate: Date;
  nextPaymentDate: Date;
  isInterestOnly: boolean;
  notes?: string;
}

export interface LoanRateChangeRequest {
  rate: number;
  effectiveFrom: Date;
  notes?: string;
}


// ── Service ───────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class LoanService {
  private client = inject(Client);
  private http = inject(HttpClient);
  private entityService = inject(EntityService);


  getAllLoans(): Observable<LoanDetailsDto[]> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.get<LoanDetailsDto[]>(`${environment.apiUrl}/entities/${entityId}/loans`);
  }

  getLoanByAccount(accountId: string): Observable<LoanDetailsDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.get<LoanDetailsDto>(`${environment.apiUrl}/entities/${entityId}/loans/${accountId}`);
  }

  createLoan(request: CreateLoanDetailsRequest): Observable<LoanDetailsDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.post<LoanDetailsDto>(`${environment.apiUrl}/entities/${entityId}/loans`, request);
  }

  updateLoan(loanId: string, request: UpdateLoanDetailsRequest): Observable<LoanDetailsDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.put<LoanDetailsDto>(`${environment.apiUrl}/entities/${entityId}/loans/${loanId}`, request);
  }

  getLoanSummary(accountId: string): Observable<LoanSummaryDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.get<LoanSummaryDto>(`${environment.apiUrl}/entities/${entityId}/loans/${accountId}/summary`);
  }

  getAmortisationSchedule(accountId: string, extraPayment = 0): Observable<AmortisationScheduleDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.get<AmortisationScheduleDto>(
      `${environment.apiUrl}/entities/${entityId}/loans/${accountId}/schedule?extraPayment=${extraPayment}`
    );
  }

  addRateChange(loanId: string, request: LoanRateChangeRequest): Observable<LoanDetailsDto> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.post<LoanDetailsDto>(`${environment.apiUrl}/entities/${entityId}/loans/${loanId}/rate-change`, request);
  }

  recalculateRedraw(accountId: string): Observable<void> {
    const entityId = this.entityService.currentEntityId!;
    return this.http.post<void>(`${environment.apiUrl}/entities/${entityId}/loans/${accountId}/recalculate-redraw`, {});
  }
}
