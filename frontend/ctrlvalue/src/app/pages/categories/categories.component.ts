import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { FinanceService } from '../../services/finance.service';
import { Category } from '../../models/api.models';
import { CategoryFormComponent } from './category-form/category-form.component';
import { CategoryKeywordsComponent } from './category-keywords/category-keywords.component';
import { animate, state, style, transition, trigger } from '@angular/animations';

@Component({
    selector: 'app-categories',
    standalone: true,
    imports: [
        CommonModule,
        RouterModule,
        MatTableModule,
        MatPaginatorModule,
        MatSortModule,
        MatButtonModule,
        MatIconModule,
        MatChipsModule,
        MatSnackBarModule,
        MatTooltipModule,
        MatDialogModule,
        CategoryKeywordsComponent
    ],
    animations: [
        trigger('detailExpand', [
            state('collapsed, void', style({ height: '0px', minHeight: '0' })),
            state('expanded', style({ height: '*' })),
            transition('expanded <=> collapsed', animate('225ms cubic-bezier(0.4, 0.0, 0.2, 1)')),
        ]),
    ],
    templateUrl: './categories.component.html',
    styleUrl: './categories.component.scss'
})
export class CategoriesComponent implements OnInit {
    displayedColumns: string[] = ['name', 'categoryType', 'parentCategory', 'color', 'icon', 'actions'];
    dataSource = new MatTableDataSource<Category>();
    loading = false;
    filter: 'ALL' | 'INCOME' | 'EXPENSE' = 'ALL';
    expandedElement: Category | null = null;

    @ViewChild(MatPaginator) paginator!: MatPaginator;
    @ViewChild(MatSort) sort!: MatSort;

    constructor(
        private financeService: FinanceService,
        private snackBar: MatSnackBar,
        private dialog: MatDialog
    ) { }

    ngOnInit(): void {
        this.loadCategories();
    }

    ngAfterViewInit(): void {
        this.dataSource.paginator = this.paginator;
        this.dataSource.sort = this.sort;
    }

    loadCategories(): void {
        this.loading = true;
        this.financeService.getCategories().subscribe({
            next: (data) => {
                this.dataSource.data = data;
                this.applyFilter();
                this.loading = false;
            },
            error: (err) => {
                this.loading = false;
                console.error(err);
                this.snackBar.open('Failed to load categories', 'Close', { duration: 5000 });
            }
        });
    }

    applyFilter(): void {
        const allData = this.dataSource.data;
        if (this.filter !== 'ALL') {
            this.dataSource.data = allData.filter((c: Category) => c.categoryType === this.filter);
        }
    }

    setFilter(filter: 'ALL' | 'INCOME' | 'EXPENSE'): void {
        this.filter = filter;
        this.loadCategories();
    }

    createCategory(): void {
        const ref = this.dialog.open(CategoryFormComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: {}
        });
        ref.afterClosed().subscribe(result => {
            if (result) this.loadCategories();
        });
    }

    deleteCategory(id: string, name: string): void {
        if (!confirm(`Are you sure you want to delete "${name}"?`)) return;
        this.financeService.deleteCategory(id).subscribe({
            next: () => {
                this.snackBar.open('Category deleted successfully', 'Close', { duration: 3000 });
                this.loadCategories();
            },
            error: (err) => {
                console.error(err);
                this.snackBar.open('Failed to delete category', 'Close', { duration: 5000 });
            }
        });
    }
}
