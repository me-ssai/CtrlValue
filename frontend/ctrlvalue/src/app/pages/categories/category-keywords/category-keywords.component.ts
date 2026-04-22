import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CategoryKeywordRuleService } from '../../../services/category-keyword-rule.service';
import { Category, CategoryKeywordRule, KeywordMatchType } from '../../../models/api.models';

@Component({
  selector: 'app-category-keywords',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule
  ],
  templateUrl: './category-keywords.component.html',
  styleUrls: ['./category-keywords.component.scss']
})
export class CategoryKeywordsComponent implements OnInit {
  @Input() category!: Category;
  keywords: CategoryKeywordRule[] = [];
  newKeyword: string = '';
  loading = false;

  constructor(
    private keywordService: CategoryKeywordRuleService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadKeywords();
  }

  loadKeywords(): void {
    if (!this.category?.id) return;
    this.loading = true;
    this.keywordService.getByCategory(this.category.id).subscribe({
      next: (data) => {
        this.keywords = data;
        this.loading = false;
      },
      error: (err) => {
        console.error(err);
        this.loading = false;
      }
    });
  }

  addKeyword(): void {
    const trimmed = this.newKeyword.trim();
    if (!trimmed || !this.category?.id) return;

    this.keywordService.create({
      categoryId: this.category.id,
      keyword: trimmed,
      matchType: KeywordMatchType.Contains,
      isCaseSensitive: false
    }).subscribe({
      next: (rule) => {
        this.keywords.push(rule);
        this.newKeyword = '';
        this.snackBar.open(`Added keyword: ${rule.keyword}`, 'Close', { duration: 3000 });
      },
      error: (err) => {
        const errorMsg = err.error?.error || 'Failed to add keyword';
        this.snackBar.open(errorMsg, 'Close', { duration: 5000 });
      }
    });
  }

  deleteKeyword(ruleId: string): void {
    this.keywordService.delete(ruleId).subscribe({
      next: () => {
        this.keywords = this.keywords.filter(k => k.id !== ruleId);
      },
      error: (err) => {
        this.snackBar.open('Failed to delete keyword', 'Close', { duration: 3000 });
      }
    });
  }
}
