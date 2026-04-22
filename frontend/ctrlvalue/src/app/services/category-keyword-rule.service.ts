import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CategoryKeywordRule,
  CreateCategoryKeywordRuleRequest,
  UpdateCategoryKeywordRuleRequest
} from '../models/api.models';

@Injectable({
  providedIn: 'root'
})
export class CategoryKeywordRuleService {
  private apiUrl = `${environment.apiUrl}/CategoryKeywordRules`;

  constructor(private http: HttpClient) { }

  getAll(): Observable<CategoryKeywordRule[]> {
    return this.http.get<CategoryKeywordRule[]>(this.apiUrl);
  }

  getByCategory(categoryId: string): Observable<CategoryKeywordRule[]> {
    return this.http.get<CategoryKeywordRule[]>(`${this.apiUrl}/category/${categoryId}`);
  }

  getById(id: string): Observable<CategoryKeywordRule> {
    return this.http.get<CategoryKeywordRule>(`${this.apiUrl}/${id}`);
  }

  create(request: CreateCategoryKeywordRuleRequest): Observable<CategoryKeywordRule> {
    return this.http.post<CategoryKeywordRule>(this.apiUrl, request);
  }

  update(id: string, request: UpdateCategoryKeywordRuleRequest): Observable<CategoryKeywordRule> {
    return this.http.put<CategoryKeywordRule>(`${this.apiUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
