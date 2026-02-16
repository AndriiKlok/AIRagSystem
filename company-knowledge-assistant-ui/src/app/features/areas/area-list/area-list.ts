import { Component, OnInit } from '@angular/core';
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

  constructor(private apiService: ApiService) { }

  ngOnInit(): void {
    this.loadAreas();
  }

  loadAreas(): void {
    this.apiService.getAreas().subscribe({
      next: (areas) => this.areas = areas,
      error: (err) => console.error('Error loading areas:', err)
    });
  }

  createArea(): void {
    const name = prompt('Enter area name:');
    if (name) {
      const description = prompt('Enter description (optional):');
      this.apiService.createArea({ name, description: description || undefined }).subscribe({
        next: (area) => {
          this.areas.push(area);
        },
        error: (err) => console.error('Error creating area:', err)
      });
    }
  }

  deleteArea(id: number): void {
    if (confirm('Are you sure you want to delete this area?')) {
      this.apiService.deleteArea(id).subscribe({
        next: () => this.loadAreas(),
        error: (err) => console.error('Error deleting area:', err)
      });
    }
  }
}
