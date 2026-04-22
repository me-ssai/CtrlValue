import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AccountKeywordRule,
  CreateAccountKeywordRuleRequest,
  UpdateAccountKeywordRuleRequest
} from '../models/api.models';

@Injectable({
  providedIn: 'root'
})
export class AccountKeywordRuleService {
  private apiUrl = `${environment.apiUrl}/AccountKeywordRules`;

  constructor(private http: HttpClient) { }

  getAll(): Observable<AccountKeywordRule[]> {
    return this.http.get<AccountKeywordRule[]>(this.apiUrl);
  }

  getByAccount(accountId: string): Observable<AccountKeywordRule[]> {
    return this.http.get<AccountKeywordRule[]>(`${this.apiUrl}/account/${accountId}`);
  }

  getById(id: string): Observable<AccountKeywordRule> {
    return this.http.get<AccountKeywordRule>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateAccountKeywordRuleRequest): Observable<AccountKeywordRule> {
    return this.http.post<AccountKeywordRule>(this.apiUrl, request);
  }

  update(id: string, request: UpdateAccountKeywordRuleRequest): Observable<AccountKeywordRule> {
    return this.http.put<AccountKeywordRule>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
