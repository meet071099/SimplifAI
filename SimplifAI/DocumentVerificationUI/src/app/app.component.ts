import { Component } from '@angular/core';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'Document Verification System';
  showNavigation = true;

  constructor(private router: Router) {
    // Listen to route changes to determine if navigation should be shown
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe((event) => {
        // Show navigation on all routes for now
        this.showNavigation = true;
      });
  }
}
