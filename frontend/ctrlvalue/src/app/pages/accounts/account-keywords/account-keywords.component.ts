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
import { AccountKeywordRuleService } from '../../../services/account-keyword-rule.service';
import { Account, AccountKeywordRule, KeywordMatchType } from '../../../models/api.models';

@Component({
  selector: 'app-account-keywords',
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
  templateUrl: './account-keywords.component.html',
  styleUrls: ['./account-keywords.component.scss']
})
export class AccountKeywordsComponent implements OnInit {
  @Input() account!: Account;
  keywords: AccountKeywordRule[] = [];
  newKeyword: string = '';
  loading = false;

  constructor(
    private keywordService: AccountKeywordRuleService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadKeywords();
  }

  loadKeywords(): void {
    if (!this.account?.id) return;
    this.loading = true;
    this.keywordService.getByAccount(this.account.id).subscribe({
      next: (data) => {
        this.keywords = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  addKeyword(): void {
    const trimmed = this.newKeyword.trim();
    if (!trimmed || !this.account?.id) return;

    this.keywordService.create({
      accountId: this.account.id,
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
      error: () => {
        this.snackBar.open('Failed to delete keyword', 'Close', { duration: 3000 });
      }
    });
  }
}
