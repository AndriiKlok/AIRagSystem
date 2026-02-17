import { Component, OnInit, NgZone, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApiService, Area } from '../../../core/services/api.service';

@Component({
  selector: 'app-area-list',
  imports: [CommonModule, RouterModule],
  templateUrl: './area-list.html',
  styleUrl: './area-list.css',
})
export class AreaList implements OnInit {
  areas: Area[] = [];

  constructor(
    private apiService: ApiService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    this.loadAreas();
  }

  loadAreas(): void {
    this.apiService.getAreas().subscribe({
      next: (areas) => {
        this.ngZone.run(() => {
          this.areas = [...areas];
          this.cdr.detectChanges();
        });
      },
      error: (err) => console.error('Error loading areas:', err)
    });
  }

  createArea(): void {
    const name = prompt('Enter area name:');
    if (name) {
      const description = prompt('Enter description (optional):');
      this.apiService.createArea({ name, description: description || undefined }).subscribe({
        next: (area) => {
          this.ngZone.run(() => {
            this.areas = [area, ...this.areas];
            this.cdr.detectChanges();
          });
          this.loadAreas();
        },
        error: (err) => console.error('Error creating area:', err)
      });
    }
  }

  deleteArea(id: number): void {
    if (confirm('Are you sure you want to delete this area?')) {
      this.apiService.deleteArea(id).subscribe({
        next: () => {
          this.ngZone.run(() => {
            this.areas = this.areas.filter(area => area.id !== id);
            this.cdr.detectChanges();
          });
          this.loadAreas();
        },
        error: (err) => console.error('Error deleting area:', err)
      });
    }
  }
}
